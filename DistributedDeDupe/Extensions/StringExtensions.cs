
using System;
using System.Security.Cryptography;
using System.Text;

namespace DistributedDeDupe
{

    // Algorithm copied from:
    // Src: https://en.wikipedia.org/wiki/Jenkins_hash_function
    public static class StringExtensions
    {
        public static string JenkinsOneAtATime(this string input)
        {
            uint hash = 0;
            foreach (char c in input)
            {
                hash += c;
                hash += hash << 10;
                hash ^= hash >> 6;
            }

            hash += hash << 3;
            hash ^= hash >> 11;
            hash += hash << 15;
            return hash.ToString("x");
        }

        public static string GetSHA256(this string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLower();
            }
        }
        
        public static string GetSHA512(this string input)
        {
            using (SHA512 sha512 = SHA512.Create())
            {
                return Convert.ToHexString(sha512.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLower();
            }
        }
    }
}