using System.Security.Cryptography;
using System.Text;

namespace BellTower.IO;

public static class ContentHash
{
    public static string Compute(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
