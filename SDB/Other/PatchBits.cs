using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDB.Other;

public class PatchBits
{

    public enum OpCode_
    {
        END = 0x0,
        PATCH_REPLACE = 0x2,
        PATCH_MATCH = 0x4
    }

    public List<PatchBitEntry> Entries { get; }

    public PatchBits(byte [] rawBytes)
    {
        Entries = new List<PatchBitEntry>();

        var index = 0x00;

        while (index<rawBytes.Length)
        {
            var opCode = (OpCode_) BitConverter.ToInt32(rawBytes, index);
            if (opCode == OpCode_.END)
            {
                   
                break;
            }

            var size = BitConverter.ToUInt32(rawBytes, index + 4);
            
            var buff = new byte[size];
            Buffer.BlockCopy(rawBytes,index,buff,0,(int) size);

            var pbe = new PatchBitEntry(buff);

            Entries.Add(pbe);
            index += (int)size;
        }

    }
  
    public override string ToString()
    {
        return $"Entries count: {Entries.Count:N0}";
    }
}