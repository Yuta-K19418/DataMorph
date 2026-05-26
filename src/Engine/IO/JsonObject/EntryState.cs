namespace DataMorph.Engine.IO.JsonObject;

internal ref struct EntryState()
{
    private string _currentKey = string.Empty;
    private long _valueStart = -1L;

    public string CurrentKey => _currentKey;
    public long ValueStart => _valueStart;
    public bool IsNested => _valueStart >= 0;

    public void StartEntry(string key)
    {
        _currentKey = key;
        _valueStart = -1L;
    }

    public void ClearEntry()
    {
        _currentKey = string.Empty;
        _valueStart = -1L;
    }

    public void RecordValueStart(long newValueStart)
    {
        _valueStart = newValueStart;
    }
}
