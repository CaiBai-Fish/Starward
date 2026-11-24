using System.Security.Cryptography;
using System.Text;

namespace Starward.Core.Manjuu;

/// <summary>
/// 蛮啾版本索引解析。索引文件为 Base64 文本，Base64 解码后按 AES-192-CBC 解密，
/// 明文为逐行 CSV：目录 <c>序号,0,0,相对路径</c>，文件 <c>序号,大小,MD5,相对路径,CDN 相对地址</c>，
/// 分片整包 <c>分片数,总大小,MD5</c>。
/// </summary>
public static class ManjuuGameIndexParser
{

    public static ManjuuGameIndex Parse(byte[] encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        string base64 = Encoding.UTF8.GetString(encrypted).Trim();
        byte[] cipher = Convert.FromBase64String(base64);
        byte[] plain = Decrypt(cipher);
        string text = Encoding.UTF8.GetString(plain);
        return ParsePlainText(text);
    }


    public static ManjuuGameIndex ParsePlainText(string text)
    {
        var index = new ManjuuGameIndex();
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            if (line.EndsWith(",", StringComparison.Ordinal))
            {
                line = line[..^1];
            }
            string[] parts = line.Split(',');
            if (parts.Length == 3)
            {
                if (long.TryParse(parts[1], out long packageSize))
                {
                    index.PackageSize = packageSize;
                    index.PackageMD5 = parts[2];
                }
            }
            else if (parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]))
            {
                index.Files.Add(new ManjuuIndexEntry
                {
                    Size = long.TryParse(parts[1], out long size) ? size : 0,
                    MD5 = parts[2],
                    Name = parts[3],
                    RemotePath = parts[4],
                });
            }
            else if (parts.Length >= 4)
            {
                index.Directories.Add(parts[3]);
            }
        }
        return index;
    }


    private static byte[] Decrypt(byte[] cipher)
    {
        using Aes aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(ManjuuGameConstants.IndexAesKey);
        aes.IV = Encoding.UTF8.GetBytes(ManjuuGameConstants.IndexAesIV);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform transform = aes.CreateDecryptor();
        return transform.TransformFinalBlock(cipher, 0, cipher.Length);
    }

}
