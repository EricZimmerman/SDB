namespace SDB;

public class StringTableEntry
{
    public StringTableEntry(int offset, string value)
    {
        Offset = offset;
        Value = value;
    }

    public int Offset { get; }
    public string Value { get; }
}