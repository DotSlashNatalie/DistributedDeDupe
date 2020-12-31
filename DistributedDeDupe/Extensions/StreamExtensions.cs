using System;
using System.IO;

public static class StreamExtensions
{
    public static string GetSHA256(this Stream s)
    {
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
        {
            return Convert.ToHexString(sha256.ComputeHash(s)).ToLower();
        }
    }
    
    public static string GetSHA512(this Stream s)
    {
        using (System.Security.Cryptography.SHA512 sha512 = System.Security.Cryptography.SHA512.Create())
        {
            return Convert.ToHexString(sha512.ComputeHash(s)).ToLower();
        }
    }
}