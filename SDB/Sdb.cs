using System;
using System.IO;
using System.Text;

namespace SDB;

public class Sdb
{
    public static SdbFile LoadFile(string sdbFile)
    {
        var raw = File.ReadAllBytes(sdbFile);

        const string sigGood = "sdbf";

        var sig = CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetString(raw, 0x8, 4);

        if (sig != sigGood)
        {
            throw new Exception($"Invalid signature at 0x08. Should be '{sigGood}'");
        }

        return new SdbFile(raw, sdbFile);
    }
}