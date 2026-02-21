using System.Globalization;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Wraps an <see cref="ITableSource"/> and applies an ordered Action Stack of
/// <see cref="MorphAction"/>s lazily — only to the cells currently requested by the TableView.
/// Constructs the output column mapping on initialization,
/// then delegates cell access to the underlying source on demand.
/// When one or more <see cref="FilterAction"/>s are present, uses an
/// <see cref="IFilterRowIndexer"/> (provided via a factory in the constructor)
/// to map filtered row indices to source rows.
/// </summary>
internal sealed class LazyTransformer : ITableSource
{
    private readonly ITableSource _source;
    private readonly IReadOnlyList<int> _sourceColumnIndices;
    private readonly string[] _columnNames;
    private readonly IReadOnlyList<ColumnType> _columnTypes;
    private readonly IFilterRowIndexer? _filterRowIndexer;

    /// <summary>
    /// Initializes a new instance of <see cref="LazyTransformer"/>.
    /// Applies the action stack to derive the output column mapping on construction.
    /// </summary>
    /// <param name="source">The underlying data source providing raw cell values.</param>
    /// <param name="originalSchema">The schema of the source before any actions are applied.</param>
    /// <param name="actions">The ordered list of transformation actions to apply.</param>
    /// <param name="filterRowIndexerFactory">
    /// Optional factory that receives resolved <see cref="FilterSpec"/>s and returns an
    /// <see cref="IFilterRowIndexer"/>. Pass <see langword="null"/> to disable row filtering.
    /// The caller is responsible for invoking <see cref="IFilterRowIndexer.BuildIndexAsync"/>
    /// on a background task after construction.
    /// </param>
    public LazyTransformer(
        ITableSource source,
        TableSchema originalSchema,
        IReadOnlyList<MorphAction> actions,
        Func<IReadOnlyList<FilterSpec>, IFilterRowIndexer?>? filterRowIndexerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(originalSchema);
        ArgumentNullException.ThrowIfNull(actions);

        _source = source;
        (_columnNames, _columnTypes, _sourceColumnIndices, var filterSpecs) =
            BuildTransformedSchema(originalSchema, actions);

        _filterRowIndexer =
            filterSpecs.Count > 0 ? filterRowIndexerFactory?.Invoke(filterSpecs) : null;
    }

    /// <summary>
    /// Gets the filter row indexer created by the factory, if any.
    /// The caller must invoke <see cref="IFilterRowIndexer.BuildIndexAsync"/> on a background task.
    /// </summary>
    internal IFilterRowIndexer? FilterRowIndexer => _filterRowIndexer;

    /// <inheritdoc/>
    public int Rows =>
        _filterRowIndexer is not null ? _filterRowIndexer.TotalMatchedRows : _source.Rows;

    /// <inheritdoc/>
    public int Columns => _columnNames.Length;

    /// <inheritdoc/>
    public string[] ColumnNames => _columnNames;

    /// <inheritdoc/>
    public object this[int row, int col]
    {
        get
        {
            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (col < 0 || col >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }

            var sourceRow = _filterRowIndexer is not null
                ? _filterRowIndexer.GetSourceRow(row)
                : row;

            if (sourceRow < 0)
            {
                return string.Empty;
            }

            var sourceCol = _sourceColumnIndices[col];
            var rawValue = _source[sourceRow, sourceCol]?.ToString() ?? string.Empty;
            return FormatCellValue(rawValue, _columnTypes[col]);
        }
    }

    /// <summary>
    /// Applies the action stack sequentially to build the output column names, types,
    /// a mapping array from output column index to source column index, and a list of
    /// resolved <see cref="FilterSpec"/>s for any <see cref="FilterAction"/>s encountered.
    /// A <see cref="Dictionary{TKey,TValue}"/> keyed by column name provides O(1) lookups per action.
    /// Actions targeting a non-existent column name are silently skipped.
    /// </summary>
    private static (
        string[] columnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<FilterSpec> filterSpecs
    ) BuildTransformedSchema(TableSchema originalSchema, IReadOnlyList<MorphAction> actions)
    {
        var working = originalSchema
            .Columns.Select(c => new WorkingColumn(
                SourceIndex: c.ColumnIndex,
                Name: c.Name,
                Type: c.Type,
                IsNullable: c.IsNullable
            ))
            .ToList();

        var nameToIndex = working.Select((w, i) => (w.Name, i)).ToDictionary(t => t.Name, t => t.i);
        var filterSpecs = new List<FilterSpec>();

        foreach (var action in actions)
        {
            if (action is RenameColumnAction rename)
            {
                if (!nameToIndex.TryGetValue(rename.OldName, out var renameIdx))
                {
                    continue;
                }

                working[renameIdx] = working[renameIdx] with { Name = rename.NewName };
                nameToIndex.Remove(rename.OldName);
                nameToIndex[rename.NewName] = renameIdx;
                continue;
            }

            if (action is DeleteColumnAction delete)
            {
                nameToIndex.Remove(delete.ColumnName);
                continue;
            }

            if (action is CastColumnAction cast)
            {
                if (!nameToIndex.TryGetValue(cast.ColumnName, out var castIdx))
                {
                    continue;
                }

                working[castIdx] = working[castIdx] with { Type = cast.TargetType };
                continue;
            }

            if (action is FilterAction filter)
            {
                // Row-level filter: does not modify column schema.
                // Resolve column name to source index and record FilterSpec.
                throw new NotImplementedException();
            }
        }

        var remaining = new List<WorkingColumn>(nameToIndex.Count);
        foreach (var idx in nameToIndex.Values.Order())
        {
            remaining.Add(working[idx]);
        }

        return (
            remaining.ConvertAll(workingColumn => workingColumn.Name).ToArray(),
            remaining.ConvertAll(workingColumn => workingColumn.Type),
            remaining.ConvertAll(workingColumn => workingColumn.SourceIndex),
            filterSpecs
        );
    }

    /// <summary>
    /// Evaluates a single filter condition against a raw cell string value.
    /// Numeric and timestamp operators parse <paramref name="rawValue"/> and
    /// <see cref="FilterSpec.Value"/>; on parse failure the row is excluded.
    /// Applying a numeric operator to a <see cref="ColumnType.Text"/> column
    /// falls back to case-insensitive string equality / inequality.
    /// </summary>
    internal static bool EvaluateFilter(string rawValue, FilterSpec spec)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats a raw cell string value according to the target column type.
    /// Returns the raw value for <see cref="ColumnType.Text"/>, <see cref="ColumnType.JsonObject"/>,
    /// and <see cref="ColumnType.JsonArray"/>. Returns <c>"&lt;invalid&gt;"</c> if parsing fails.
    /// </summary>
    private static string FormatCellValue(string rawValue, ColumnType targetType) =>
        targetType switch
        {
            ColumnType.WholeNumber => long.TryParse(rawValue, out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : "<invalid>",
            ColumnType.FloatingPoint => double.TryParse(
                rawValue,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var d
            )
                ? d.ToString(CultureInfo.InvariantCulture)
                : "<invalid>",
            ColumnType.Boolean => bool.TryParse(rawValue, out var b)
                ? (b ? "true" : "false")
                : "<invalid>",
            ColumnType.Timestamp => DateTime.TryParse(rawValue, out var dt)
                ? dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "<invalid>",
            _ => rawValue,
        };

    private sealed record WorkingColumn(
        int SourceIndex,
        string Name,
        ColumnType Type,
        bool IsNullable
    );
}
