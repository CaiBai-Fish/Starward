using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Core.Yostar;

/// <summary>
/// CRC-64/XZ（多项式 0x42F0E1EBA9EA3693，反射，init/xorout 全 1）。
/// 明日方舟国际服 manifest 中的 hash 即该算法的十进制无符号值。
/// </summary>
public static class Crc64Xz
{
    private const ulong Polynomial = 0xC96C5795D7870F42UL;

    private static readonly ulong[] Table = CreateTable();


    private static ulong[] CreateTable()
    {
        ulong[] table = new ulong[256];
        for (ulong i = 0; i < 256; i++)
        {
            ulong crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }


    public static ulong Compute(ReadOnlySpan<byte> data)
    {
        ulong crc = ulong.MaxValue;
        foreach (byte b in data)
        {
            crc = Table[(byte)(crc ^ b)] ^ (crc >> 8);
        }
        return crc ^ ulong.MaxValue;
    }


    public static async Task<string> ComputeFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20, useAsync: true);
        ulong crc = ulong.MaxValue;
        byte[] buffer = new byte[1 << 20];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                crc = Table[(byte)(crc ^ buffer[i])] ^ (crc >> 8);
            }
        }
        return (crc ^ ulong.MaxValue).ToString();
    }
}
