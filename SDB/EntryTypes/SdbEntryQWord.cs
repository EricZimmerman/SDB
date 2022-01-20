using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SDB.EntryTypes;

public class SdbEntryQWord : ISdbEntry
{
    public SdbEntryQWord(SdbFile.TagValue typeId, byte[] bytes, int offset)
    {
        TypeId = typeId;
        Bytes = bytes;
        Offset = offset;

        Children = new List<ISdbEntry>();
    }


    public List<ISdbEntry> Children { get; }

    public SdbFile.TagValue TypeId { get; }

    [IgnoreDataMember] public byte[] Bytes { get; }

    public object Value => BitConverter.ToInt64(Bytes, 0);

    public int Offset { get; set; }

    public override string ToString()
    {
        return $"Type: {TypeId} (0x{TypeId:X}) --> {Value} Children count: {Children.Count:N0}";
    }
}