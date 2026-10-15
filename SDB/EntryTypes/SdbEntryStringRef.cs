using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SDB.EntryTypes;

public class SdbEntryStringRef : ISdbEntry
{
    public SdbEntryStringRef(SdbFile.TagValue typeId, byte[] bytes, int offset)
    {
        TypeId = typeId;
        Bytes = bytes;
        Offset = offset;

        Children = new List<ISdbEntry>();
    }

    internal SdbEntryStringRef(SdbFile.TagValue typeId, byte[] bytes, int offset,
        Dictionary<int, StringTableEntry> stringTable) : this(typeId, bytes, offset)
    {
        _stringTable = stringTable;
    }

    // The string table of the file this entry came from (filled in once the whole file is parsed)
    private readonly Dictionary<int, StringTableEntry> _stringTable;

    public List<ISdbEntry> Children { get; }

    public SdbFile.TagValue TypeId { get; }

    [IgnoreDataMember] public byte[] Bytes { get; }

    public object Value => GetValue();

    public int Offset { get; set; }

    /// <summary>Offset of the referenced item, relative to the start of TAG_STRINGTABLE</summary>
    public int StringOffset => BitConverter.ToInt32(Bytes, 0);

    private object GetValue()
    {
        // Entries created outside the parser fall back to the last loaded file, as before
        var table = _stringTable ?? SdbFile.StringTableEntries;

        return table.TryGetValue(StringOffset, out var entry)
            ? entry.Value
            : $"(string not found at string table offset 0x{StringOffset:X})";
    }

    public override string ToString()
    {
        return $"Type: {TypeId} (0x{TypeId:X}) --> {Value} Children count: {Children.Count:N0}";
    }
}
