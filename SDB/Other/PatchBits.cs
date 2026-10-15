using System;
using System.Collections.Generic;

namespace SDB.Other;

/// <summary>The contents of TAG_PATCH_BITS: patch entries until an END opcode</summary>
public class PatchBits
{
    public enum OpCode_
    {
        END = 0x0,
        PATCH_REPLACE = 0x2,
        PATCH_MATCH = 0x4
    }

    public List<PatchBitEntry> Entries { get; }

    /// <summary>Problems found while parsing (bad sizes); the entries before the problem are kept</summary>
    public List<string> ParseErrors { get; } = new();

    public PatchBits(byte[] rawBytes)
    {
        Entries = new List<PatchBitEntry>();

        var index = 0x00;

        while (index < rawBytes.Length)
        {
            if (rawBytes.Length - index < 4)
            {
                ParseErrors.Add($"Patch bits: {rawBytes.Length - index} trailing byte(s) at 0x{index:X}");
                break;
            }

            var opCode = (OpCode_)BitConverter.ToInt32(rawBytes, index);

            if (opCode == OpCode_.END)
            {
                break;
            }

            if (rawBytes.Length - index < 8)
            {
                ParseErrors.Add($"Patch bits: entry at 0x{index:X} has no size");
                break;
            }

            var size = BitConverter.ToUInt32(rawBytes, index + 4);

            // a size smaller than the fixed part would never move forward (or read past the entry)
            if (size < PatchBitEntry.HeaderSize || size > rawBytes.Length - index)
            {
                ParseErrors.Add(
                    $"Patch bits: entry at 0x{index:X} has size 0x{size:X} (needs 0x{PatchBitEntry.HeaderSize:X} to 0x{rawBytes.Length - index:X})");
                break;
            }

            var buff = new byte[size];
            Buffer.BlockCopy(rawBytes, index, buff, 0, (int)size);

            var pbe = new PatchBitEntry(buff);

            if (pbe.Pattern.Length < pbe.PatternSize)
            {
                ParseErrors.Add(
                    $"Patch bits: entry at 0x{index:X} pattern size 0x{pbe.PatternSize:X} is larger than the entry; pattern truncated to 0x{pbe.Pattern.Length:X}");
            }

            Entries.Add(pbe);

            index += (int)size;
        }
    }

    public override string ToString()
    {
        return $"Entries count: {Entries.Count:N0}";
    }
}
