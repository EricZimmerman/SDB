using System.IO;
using System.Text;

namespace SDB;

public class Sdb
{
    /// <summary>Major version (Int32), minor version (Int32), then the 'sdbf' signature</summary>
    internal const int HeaderSize = 0xC;

    /// <exception cref="SdbParseException">The file is smaller than the header or has no 'sdbf' signature</exception>
    public static SdbFile LoadFile(string sdbFile)
    {
        var raw = File.ReadAllBytes(sdbFile);

        CheckHeader(raw);

        return new SdbFile(raw, sdbFile);
    }

    internal static void CheckHeader(byte[] raw)
    {
        const string sigGood = "sdbf";

        if (raw.Length < HeaderSize)
        {
            throw new SdbParseException($"File is too small ({raw.Length} bytes) to be an SDB file", 0, "header");
        }

        var sig = Encoding.ASCII.GetString(raw, 0x8, 4);

        if (sig != sigGood)
        {
            throw new SdbParseException($"Invalid signature at 0x08. Should be '{sigGood}'", 0x8, "signature");
        }
    }
}
