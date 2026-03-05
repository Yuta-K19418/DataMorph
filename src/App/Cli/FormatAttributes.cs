using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class RecordReaderAttribute : Attribute
{
    public DataFormat Format { get; }

    public RecordReaderAttribute(DataFormat format)
    {
        Format = format;
    }
}

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class RecordWriterAttribute : Attribute
{
    public DataFormat Format { get; }

    public RecordWriterAttribute(DataFormat format)
    {
        Format = format;
    }
}
