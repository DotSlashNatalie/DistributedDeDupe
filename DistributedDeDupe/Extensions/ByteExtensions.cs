using System;

namespace DistributedDeDupe
{
    // Algorithm copied from:
    // Src: https://en.wikipedia.org/wiki/Jenkins_hash_function
    public static class ByteExtensions
    {
        public static string JenkinsOneAtATime(this byte[] input)
        {
            uint hash = 0;
            foreach (byte b in input)
            {
                hash += b;
                hash += hash << 10;
                hash ^= hash >> 6;
            }

            hash += hash << 3;
            hash ^= hash >> 11;
            hash += hash << 15;
            return hash.ToString("x");
        }

        public static string GetSHA256(this byte[] input, int offset, int count)
        {
            using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
            {
                return Convert.ToHexString(sha256.ComputeHash(input, offset, count)).ToLower();
            }
        }
        
        public static string GetSHA512(this byte[] input, int offset, int count)
        {
            using (System.Security.Cryptography.SHA512 sha512 = System.Security.Cryptography.SHA512.Create())
            {
                return Convert.ToHexString(sha512.ComputeHash(input, offset, count)).ToLower();
            }
        }
    }
}