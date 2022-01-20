using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDB.Other;

public    class PatchBitEntry
{

    public PatchBitEntry(byte [] rawBytes)
    {
        var index = 0x00;
        OpCode = (PatchBits.OpCode_) BitConverter.ToInt32(rawBytes, index);
        index += 4;
        ActionSize = BitConverter.ToInt32(rawBytes, index);
        index += 4;
        PatternSize = BitConverter.ToInt32(rawBytes, index);
        index += 4;
        Rva = BitConverter.ToInt32(rawBytes, index);
        index += 4;
        UnknownInt=BitConverter.ToInt32(rawBytes, index);
        index += 4;
        ModuleName = Encoding.Unicode.GetString(rawBytes, index, 0x20);

        var nullPos = ModuleName.IndexOf('\0');
        if (nullPos > -1)
        {
            ModuleName = ModuleName.Substring(0, nullPos);
        }

        index += 0x20; // module size

        var buff = new byte[0x20];
        Buffer.BlockCopy(rawBytes,index,buff,0,0x20);

        UnknownBytes = buff;
            
        index += 0x20; //unknown, seen all 0x0

        Pattern = new byte[PatternSize];
        Buffer.BlockCopy(rawBytes,index,Pattern,0,PatternSize);
        index += PatternSize;
            
        //Trace.Assert(index==rawBytes.Length,"Remaining bytes in rawBytes");
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
        return $"OpCode: {OpCode} Action Size: 0x{ActionSize:X} Pattern size: 0x{PatternSize} Rva: 0x{Rva:X} Module: {ModuleName}";
    }
}