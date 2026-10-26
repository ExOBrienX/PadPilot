namespace PadPilot.Core.Protocol;

/// <summary>
/// CRC32 estándar (IEEE 802.3). Por Bluetooth, los reportes de salida del DualSense y del DS4
/// llevan este CRC calculado sobre un byte "semilla" (0xA2 para salida) + el reporte.
/// Si el CRC no coincide, el mando ignora el reporte en silencio.
/// </summary>
public static class Crc32
{
    public const byte OutputSeed = 0xA2;

    private static readonly uint[] Table = BuildTable();

    public static uint Compute(byte seed, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        crc = Table[(crc ^ seed) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    /// <summary>Escribe el CRC en little-endian en los 4 bytes finales del reporte.</summary>
    public static void Append(Span<byte> report)
    {
        int payload = report.Length - 4;
        uint crc = Compute(OutputSeed, report[..payload]);
        report[payload] = (byte)crc;
        report[payload + 1] = (byte)(crc >> 8);
        report[payload + 2] = (byte)(crc >> 16);
        report[payload + 3] = (byte)(crc >> 24);
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
