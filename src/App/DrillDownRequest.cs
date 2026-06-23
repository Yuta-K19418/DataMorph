using DataMorph.Engine.Types;

namespace DataMorph.App;

/// <param name="Format">Source file format.</param>
/// <param name="NodeBytes">Raw bytes of the selected node's JSON value.</param>
/// <param name="KeyName">
///   Property name this node represents (e.g. "tags").
///   Null when the selected node is a root-level node (JSON Lines line or JSON Array element).
/// </param>
/// <param name="RecordPosition">
///   1-based line number (JSON Lines) or 0-based element index (JSON Array) of the ancestor
///   root record. Null for JSON Object format.
/// </param>
internal sealed record DrillDownRequest(
    DataFormat Format,
    JsonRawBytes NodeBytes,
    string? KeyName = null,
    long? RecordPosition = null);
