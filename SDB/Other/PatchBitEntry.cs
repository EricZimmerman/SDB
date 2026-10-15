using System;
using System.Text;

namespace SDB.Other;

public class PatchBitEntry
{
    /// <summary>OpCode, ActionSize, PatternSize, Rva, UnknownInt (Int32 each), module name and unknown bytes (0x20 each)</summary>
    public const int HeaderSize = 4 * 5 + 0x20 + 0x20;

    /// <exception cref="SdbParseException"><paramref name="rawBytes" /> is shorter than <see cref="HeaderSize" /></exception>
    public PatchBitEntry(byte[] rawBytes)
    {
        if (rawBytes.Length < HeaderSize)
        {
            throw new SdbParseException(
                $"Patch bit entry is 0x{rawBytes.Length:X} bytes; needs at least 0x{HeaderSize:X}", 0, "PatchBitEntry");
        }

        var index = 0x00;

        OpCode = (PatchBits.OpCode_)BitConverter.ToInt32(rawBytes, index);
        index += 4;
        ActionSize = BitConverter.ToInt32(rawBytes, index);
        index += 4;
        PatternSize = BitConverter.ToInt32(rawBytes, index);
        index += 4;
        Rva = BitConverter.ToInt32(rawBytes, index);
        index += 4;
        UnknownInt = BitConverter.ToInt32(rawBytes, index);
        index += 4;

        ModuleName = Encoding.Unicode.GetString(rawBytes, index, 0x20);

        var nullPos = ModuleName.IndexOf('\0');
        if (nullPos > -1)
        {
            ModuleName = ModuleName.Substring(0, nullPos);
        }

        index += 0x20; // module size

        var buff = new byte[0x20];
        Buffer.BlockCopy(rawBytes, index, buff, 0, 0x20);
        UnknownBytes = buff;

        index += 0x20; //unknown, seen all 0x0

        // a pattern size past the end of the entry is cut to what is there (PatchBits reports it)
        var patternLength = Math.Max(0, Math.Min(PatternSize, rawBytes.Length - index));
        Pattern = new byte[patternLength];
        Buffer.BlockCopy(rawBytes, index, Pattern, 0, patternLength);
    }

    public PatchBits.OpCode_ OpCode { get; }
    public int ActionSize { get; }
    public int PatternSize { get; }
    public int Rva { get; }
    public int UnknownInt { get; }

    public byte[] UnknownBytes { get; }
    public string ModuleName { get; }

    public byte[] Pattern { get; }

    public override string ToString()
    {
        return $"OpCode: {OpCode} Action Size: 0x{ActionSize:X} Pattern size: 0x{PatternSize:X} Rva: 0x{Rva:X} Module: {ModuleName}";
    }
}
