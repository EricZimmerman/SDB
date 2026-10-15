using System;
using System.Collections.Generic;
using System.Text;
using SDB.EntryTypes;
using SDB.Other;
using Serilog;

namespace SDB;

/// <summary>
///     Parses the tags in one list (or the top level of the file). Every tag is a UInt16 whose high nibble is the
///     type: NULL (no data), WORD/DWORD/QWORD (2/4/8 bytes), STRINGREF (4 byte offset into TAG_STRINGTABLE), or
///     LIST/STRING/BINARY (Int32 size, then that many bytes).
/// </summary>
internal static class Chunk
{
    /// <summary>Real files nest about 6 deep; this keeps hostile input from overflowing the stack</summary>
    internal const int MaxDepth = 32;

    internal sealed class Context
    {
        public List<string> ParseErrors { get; } = new();

        /// <summary>TAG_STRINGTABLE items, keyed by their offset from the TAG_STRINGTABLE tag</summary>
        public Dictionary<int, StringTableEntry> Strings { get; } = new();

        public Dictionary<SdbFile.TagValue, int> Metrics { get; } = new();

        public List<SdbEntryStringRef> StringRefs { get; } = new();
    }

    /// <summary>
    ///     Parses the tags in <paramref name="data" />[<paramref name="start" />..<paramref name="end" />).
    ///     <paramref name="parent" /> is the list being parsed, or null for the top level of the file (only lists
    ///     allowed there); <paramref name="depth" /> is its nesting level. Problems go to <paramref name="context" />.
    /// </summary>
    internal static List<ISdbEntry> Parse(byte[] data, int start, int end, SdbFile.TagValue? parent, int depth,
        Context context)
    {
        var children = new List<ISdbEntry>();

        var index = start;

        while (index < end)
        {
            if (end - index < 2)
            {
                if (parent != null)
                {
                    // seen in real files at the end of a list; not worth reporting
                    break;
                }

                context.ParseErrors.Add($"{end - index} trailing byte(s) at 0x{index:X}");
                break;
            }

            var tagOffset = index;
            var id = (SdbFile.TagValue)BitConverter.ToUInt16(data, index);
            var tagType = (SdbFile.TagType)((int)id & 0xF000);

            Log.Debug("Tag {Id} (type {TagType}) at 0x{Offset:X}", id, tagType, tagOffset);

            index += 2;

            if (parent == null && tagType != SdbFile.TagType.TAG_TYPE_LIST)
            {
                context.ParseErrors.Add(
                    $"Unexpected tag type 0x{(int)tagType:X4} (tag 0x{(int)id:X4}) at 0x{tagOffset:X}; only lists are expected at the top level. Please send this sdb file to saericzimmerman@gmail.com so support can be added");
                break;
            }

            // a special case seen in real files: skip one byte and carry on
            if ((int)id == 0x53db)
            {
                index -= 1;
                continue;
            }

            if (parent != null)
            {
                context.Metrics[id] = context.Metrics.TryGetValue(id, out var count) ? count + 1 : 1;
            }

            switch (tagType)
            {
                case SdbFile.TagType.TAG_TYPE_NULL:
                    children.Add(new SdbEntryNull(id, Array.Empty<byte>(), tagOffset));
                    continue;

                case SdbFile.TagType.TAG_TYPE_WORD:
                case SdbFile.TagType.TAG_TYPE_DWORD:
                case SdbFile.TagType.TAG_TYPE_QWORD:
                case SdbFile.TagType.TAG_TYPE_STRINGREF:
                {
                    var length = tagType switch
                    {
                        SdbFile.TagType.TAG_TYPE_WORD => 2,
                        SdbFile.TagType.TAG_TYPE_QWORD => 8,
                        _ => 4
                    };

                    if (end - index < length)
                    {
                        context.ParseErrors.Add(
                            $"Tag {id} at 0x{tagOffset:X} needs {length} bytes of data, only {end - index} left");
                        return children;
                    }

                    var buff = new byte[length];
                    Buffer.BlockCopy(data, index, buff, 0, length);
                    index += length;

                    switch (tagType)
                    {
                        case SdbFile.TagType.TAG_TYPE_WORD:
                            children.Add(new SdbEntryWord(id, buff, tagOffset));
                            break;
                        case SdbFile.TagType.TAG_TYPE_DWORD:
                            children.Add(new SdbEntryDWord(id, buff, tagOffset));
                            break;
                        case SdbFile.TagType.TAG_TYPE_QWORD:
                            children.Add(new SdbEntryQWord(id, buff, tagOffset));
                            break;
                        default:
                            var sr = new SdbEntryStringRef(id, buff, tagOffset, context.Strings);
                            context.StringRefs.Add(sr);
                            children.Add(sr);
                            break;
                    }

                    continue;
                }

                case SdbFile.TagType.TAG_TYPE_LIST:
                case SdbFile.TagType.TAG_TYPE_STRING:
                case SdbFile.TagType.TAG_TYPE_BINARY:
                {
                    if (end - index < 4)
                    {
                        context.ParseErrors.Add($"Tag {id} at 0x{tagOffset:X} has no size");
                        return children;
                    }

                    var size = BitConverter.ToInt32(data, index);
                    index += 4;

                    if (size < 0)
                    {
                        context.ParseErrors.Add($"Tag {id} at 0x{tagOffset:X} has a negative size ({size})");
                        return children;
                    }

                    var truncated = size > end - index;
                    if (truncated)
                    {
                        context.ParseErrors.Add(
                            $"Tag {id} at 0x{tagOffset:X} has size 0x{size:X} but only 0x{end - index:X} bytes are left; using those");
                        size = end - index;
                    }

                    var buff = new byte[size];
                    Buffer.BlockCopy(data, index, buff, 0, size);

                    switch (tagType)
                    {
                        case SdbFile.TagType.TAG_TYPE_LIST:
                        {
                            var list = new SdbEntryList(id, buff, tagOffset);

                            if (depth + 1 > MaxDepth)
                            {
                                context.ParseErrors.Add(
                                    $"List {id} at 0x{tagOffset:X} is nested more than {MaxDepth} deep; its contents are skipped");
                            }
                            else
                            {
                                list.Children.AddRange(Parse(data, index, index + size, id, depth + 1, context));
                            }

                            children.Add(list);
                            break;
                        }
                        case SdbFile.TagType.TAG_TYPE_STRING:
                            // TAG_TYPE_STRINGREF values are offsets from the TAG_STRINGTABLE tag, which is 6 bytes before
                            // the list's data; that is the same as this item's data offset from the start of the data
                            if (parent == SdbFile.TagValue.TAG_STRINGTABLE)
                            {
                                var key = index - start;
                                context.Strings[key] = new StringTableEntry(key,
                                    Encoding.Unicode.GetString(buff, 0, size - size % 2).Trim('\0'));
                            }

                            children.Add(new SdbEntryStringTableItem(id, buff, tagOffset));
                            break;
                        default:
                            var bin = new SdbEntryBinary(id, buff, tagOffset);

                            if (id == SdbFile.TagValue.TAG_PATCH_BITS)
                            {
                                bin.PatchBits = new PatchBits(buff);
                                foreach (var error in bin.PatchBits.ParseErrors)
                                {
                                    context.ParseErrors.Add($"{error} (TAG_PATCH_BITS at 0x{tagOffset:X})");
                                }
                            }

                            children.Add(bin);
                            break;
                    }

                    index += size;

                    if (truncated)
                    {
                        return children;
                    }

                    continue;
                }

                default:
                    // TAG_TYPE_BYTE has no known tags; the rest are not defined
                    context.ParseErrors.Add(
                        $"Unsupported tag type 0x{(int)tagType:X4} (tag 0x{(int)id:X4}) at 0x{tagOffset:X}; the rest of {(parent == null ? "the file" : parent.ToString())} is skipped. Please send this sdb file to saericzimmerman@gmail.com so support can be added");
                    return children;
            }
        }

        return children;
    }
}
