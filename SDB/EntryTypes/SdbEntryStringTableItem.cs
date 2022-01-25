using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace SDB.EntryTypes;

public class SdbEntryStringTableItem : ISdbEntry
{
    public SdbEntryStringTableItem(SdbFile.TagValue typeId, byte[] bytes, int offset)
    {
        TypeId = typeId;
        Bytes = bytes;
        Offset = offset;

        Children = new List<ISdbEntry>();
    }

    public List<ISdbEntry> Children { get; }

    public SdbFile.TagValue TypeId { get; }

    [IgnoreDataMember] public byte[] Bytes { get; }

    public object Value => Encoding.Unicode.GetString(Bytes).Trim('\0');

    public int Offset { get; set; }


    public override string ToString()
    {
        return $"Type: {TypeId} (0x{TypeId:X}) Bytes length: {Bytes.Length:N0} value: {Value}";
    }
}