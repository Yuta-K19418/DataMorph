namespace DataMorph.Engine.IO.DrillDown;

/// <summary>A single row in the FocusedTable: child object bytes and its pre-computed # value.</summary>
public readonly record struct FocusedTableRow(JsonRawBytes Bytes, string HashValue);
