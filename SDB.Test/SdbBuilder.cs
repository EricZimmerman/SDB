using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SDB.Test;

/// <summary>
///     Writes SDB files tag by tag: the 12 byte header, then lists of WORD/DWORD/QWORD/STRINGREF/STRING/BINARY tags.
///     <see cref="Offsets" /> records the file offset of each tag, by the name given to it.
/// </summary>
public sealed class SdbBuilder
{
    private readonly MemoryStream _ms = new();
    private readonly BinaryWriter _bw;
    private readonly Stack<long> _openLists = new();

    public Dictionary<string, int> Offsets { get; } = new();

    public SdbBuilder(int major = 2, int minor = 1)
    {
        _bw = new BinaryWriter(_ms);
        _bw.Write(major);
        _bw.Write(minor);
        _bw.Write(Encoding.ASCII.GetBytes("sdbf"));
    }

    private void Tag(SdbFile.TagValue tag, string name)
    {
        if (name != null)
        {
            Offsets[name] = (int)_ms.Position;
        }

        _bw.Write((ushort)tag);
    }

    public SdbBuilder BeginList(SdbFile.TagValue tag, string name = null)
    {
        Tag(tag, name);
        _openLists.Push(_ms.Position);
        _bw.Write(0); // size, filled in by EndList
        return this;
    }

    public SdbBuilder EndList()
    {
        var sizePos = _openLists.Pop();
        var end = _ms.Position;
        _ms.Position = sizePos;
        _bw.Write((int)(end - sizePos - 4));
        _ms.Position = end;
        return this;
    }

    public SdbBuilder Null(SdbFile.TagValue tag, string name = null)
    {
        Tag(tag, name);
        return this;
    }

    public SdbBuilder Word(SdbFile.TagValue tag, ushort value, string name = null)
    {
        Tag(tag, name);
        _bw.Write(value);
        return this;
    }

    public SdbBuilder DWord(SdbFile.TagValue tag, int value, string name = null)
    {
        Tag(tag, name);
        _bw.Write(value);
        return this;
    }

    public SdbBuilder QWord(SdbFile.TagValue tag, long value, string name = null)
    {
        Tag(tag, name);
        _bw.Write(value);
        return this;
    }

    public SdbBuilder StringRef(SdbFile.TagValue tag, int stringTableOffset, string name = null)
    {
        Tag(tag, name);
        _bw.Write(stringTableOffset);
        return this;
    }

    /// <summary>A TAG_STRINGTABLE_ITEM (or other STRING tag): Int32 size, UTF-16 with a terminating NUL</summary>
    public SdbBuilder String(SdbFile.TagValue tag, string value, string name = null)
    {
        Tag(tag, name);
        var bytes = Encoding.Unicode.GetBytes(value + "\0");
        _bw.Write(bytes.Length);
        _bw.Write(bytes);
        return this;
    }

    public SdbBuilder Binary(SdbFile.TagValue tag, byte[] value, string name = null)
    {
        Tag(tag, name);
        _bw.Write(value.Length);
        _bw.Write(value);
        return this;
    }

    /// <summary>Raw bytes, for malformed data</summary>
    public SdbBuilder Raw(params byte[] bytes)
    {
        _bw.Write(bytes);
        return this;
    }

    public byte[] ToArray()
    {
        if (_openLists.Count > 0)
        {
            throw new InvalidOperationException("A list is still open");
        }

        return _ms.ToArray();
    }

    /// <summary>One PATCH_BITS entry: OpCode, size of the whole entry, pattern size, RVA, unknown, module, 0x20 unknown bytes, pattern</summary>
    public static byte[] PatchBitEntry(PatchBitsOp opCode, int rva, string module, byte[] pattern, int? sizeOverride = null,
        int? patternSizeOverride = null)
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write((int)opCode);
        bw.Write(sizeOverride ?? 4 * 5 + 0x20 + 0x20 + pattern.Length);
        bw.Write(patternSizeOverride ?? pattern.Length);
        bw.Write(rva);
        bw.Write(0);
        var moduleBytes = new byte[0x20];
        var name = Encoding.Unicode.GetBytes(module);
        Buffer.BlockCopy(name, 0, moduleBytes, 0, Math.Min(name.Length, 0x20));
        bw.Write(moduleBytes);
        bw.Write(new byte[0x20]);
        bw.Write(pattern);
        return ms.ToArray();
    }

    public enum PatchBitsOp
    {
        End = 0,
        Replace = 2,
        Match = 4
    }
}
