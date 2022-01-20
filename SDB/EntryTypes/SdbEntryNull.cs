using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SDB.EntryTypes;

public class SdbEntryNull : ISdbEntry
{
    public SdbEntryNull(SdbFile.TagValue typeId, byte[] bytes, int offset)
    {
        TypeId = typeId;
        Bytes = bytes;
        Offset = offset;

        Children = new List<ISdbEntry>();
    }


    public List<ISdbEntry> Children { get; }

    public SdbFile.TagValue TypeId { get; }

    [IgnoreDataMember] public byte[] Bytes { get; }

    public object Value => true;

    public int Offset { get; set; }

    public override string ToString()
    {
        return $"Type: {TypeId} (0x{TypeId:X}) Children count: {Children.Count:N0}";
    }
}