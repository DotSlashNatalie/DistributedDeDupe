using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using SharpFileSystem.IO;

namespace DistributedDeDupe
{
    // SecureString is not or guaranteed to be encrypted on non Windows platforms
    // So....we could obfuscate the key in memory but what is the point?
    // It would be a game of cat and mouse each release changing the obfuscation algorithm
    // Src: https://github.com/dotnet/platform-compat/blob/master/docs/DE0001.md
    // Src: https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Security/SecureString.Unix.cs
    
    // We could probably create our own SecureString ....
    // Generate random key
    // Create temp file with key
    // Would that be good enough?
    // For now we will use regular string until a consensus can be made

    // Src: https://security.stackexchange.com/questions/3959/recommended-of-iterations-when-using-pkbdf2-sha256
    // Src: https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes?view=net-5.0
    // Src: https://gist.github.com/mark-adams/87aa34da3a5ed48ed0c7
    // Src: https://stackoverflow.com/questions/11418236/in-aes-encryption-does-the-number-of-iterations-really-add-more-security?rq=1
    // Src: https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes?view=net-5.0
    public class AESWrapper
    {
        // Src: https://stackoverflow.com/a/43858011/195722
        public static byte[] GenerateKeyByte(SecureString key, string salt, int iterations = 10000, int keysize = 256)
        {
            IntPtr ptr = Marshal.SecureStringToBSTR(key);
            byte[] passwordByteArray = null;
            try
            {
                int length = Marshal.ReadInt32(ptr, -4);
                passwordByteArray = new byte[length];
                GCHandle handle = GCHandle.Alloc(passwordByteArray, GCHandleType.Pinned);
                try
                {
                    for (int i = 0; i < length; i++)
                    {
                        passwordByteArray[i] = Marshal.ReadByte(ptr, i);
                    }

                    using (var rfc2898 = new Rfc2898DeriveBytes(passwordByteArray, Convert.FromBase64String(salt), iterations))
                    {
                        return rfc2898.GetBytes(keysize / 8);
                    }
                }
                finally
                {
                    Array.Clear(passwordByteArray, 0, passwordByteArray.Length);  
                    handle.Free();
                }
            }
            finally
            {
                Marshal.ZeroFreeBSTR(ptr);
            }
        }
        
        public static SecureString GenerateKeyString(SecureString key, string salt, int iterations = 10000, int keysize = 256)
        {
            IntPtr ptr = Marshal.SecureStringToBSTR(key);
            byte[] passwordByteArray = null;
            try
            {
                int length = Marshal.ReadInt32(ptr, -4);
                passwordByteArray = new byte[length];
                GCHandle handle = GCHandle.Alloc(passwordByteArray, GCHandleType.Pinned);
                try
                {
                    for (int i = 0; i < length; i++)
                    {
                        passwordByteArray[i] = Marshal.ReadByte(ptr, i);
                    }

                    using (var rfc2898 = new Rfc2898DeriveBytes(passwordByteArray, Convert.FromBase64String(salt), iterations))
                    {
                        SecureString returnSecureString = new SecureString();
                        foreach (char c in Convert.ToBase64String(rfc2898.GetBytes(keysize / 8)))
                        {
                            returnSecureString.AppendChar(c);
                        }

                        return returnSecureString;
                    }
                }
                finally
                {
                    Array.Clear(passwordByteArray, 0, passwordByteArray.Length);  
                    handle.Free();
                }
            }
            finally
            {
                Marshal.ZeroFreeBSTR(ptr);
            }
        }
        
        // So....I'm sure I'm going to get all sorts of email and issues about iterations
        // It boils down to - how paranoid are you?
        // If you are worried about state governments - probably should set this to a high number
        // If you are worried about your data in the cloud - 10k is probably fine
        // I set the keysize to 256 - the max size you can use with AES
        //
        // Reminder: After settings the keysize and iterations you cannot change it
        // I will not write code to "convert" between different keys
        // However, if you want to fork or create a pull request I would be happy to consider adding it
        //
        // Src: https://stackoverflow.com/questions/21145982/rfc2898derivebytes-iterationcount-purpose-and-best-practices
        public static byte[] GenerateKeyByte(string key, string salt, int iterations = 10000, int keysize = 256)
        {
            Rfc2898DeriveBytes k = new Rfc2898DeriveBytes(key, Convert.FromBase64String(salt), iterations);
            // 1 byte = 8 bit
            return k.GetBytes(keysize / 8);
        }

        public static string GenerateKeyString(string key, string salt, int iterations = 10000, int keysize = 256)
        {
            return Convert.ToBase64String(GenerateKeyByte(key, salt, iterations, keysize));
        }

        public static byte[] GenerateSaltByte(int size = 512)
        {
            byte[] salt = new byte[size / 8];
            using RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
            rngCsp.GetBytes(salt);

            return salt;
        }

        public static string GenerateSaltString(int size = 512)
        {
            return Convert.ToBase64String(GenerateSaltByte(size));
        }
        
        public static void EncryptFileToFile(string srcFile, string dstFile, string key)
        {
            EncryptFileToFile(srcFile, dstFile, Convert.FromBase64String(key));
        }

        public static void EncryptFileToFile(string srcFile, string dstFile, byte[] key)
        {
            byte[] srcFileData; //= System.IO.File.ReadAllBytes(srcFile);
            using (FileStream fs = new FileStream(srcFile, FileMode.Open, FileAccess.ReadWrite))
            {
                srcFileData = fs.ReadAllBytes();
            }
            byte[] enc = EncryptToByte(srcFileData, key);
            System.IO.File.WriteAllBytes(dstFile, enc);
            
        }
        
        public static void DecryptFileToFile(string srcFile, string dstFile, string key)
        {
            DecryptFileToFile(srcFile, dstFile, Convert.FromBase64String(key));
        }
        
        public static void DecryptFileToFile(string srcFile, string dstFile, byte[] key)
        {
            byte[] srcFileData = System.IO.File.ReadAllBytes(srcFile);
            byte[] dec = DecryptToByte(srcFileData, key);
            System.IO.File.WriteAllBytes(dstFile, dec);
        }

        public static byte[] EncryptToByte(string plainText, string key)
        {
            return EncryptToByte(Encoding.UTF8.GetBytes(plainText), key);
        }
        
        public static byte[] EncryptToByte(string plainText, byte[] key)
        {
            //frell it - let's just create methods that have every variation to make it easier to use
            return EncryptToByte(Encoding.UTF8.GetBytes(plainText), key);
        }

        public static byte[] EncryptToByte(byte[] plainText, byte[] key)
        {
            return EncryptToByte(plainText, key, 0, plainText.Length);
        }

        public static byte[] EncryptToByte(byte[] plainText, string key)
        {
            return EncryptToByte(plainText, Convert.FromBase64String(key), 0, plainText.Length);
        }

        public static byte[] EncryptToByte(byte[] plainText, string key, int offset, int size)
        {
            return EncryptToByte(plainText, Convert.FromBase64String(key), offset, size);
        }
        
        // Encryption crash course:
        // IVs are similar to the concept of salts
        // They make sure 2 messages encrypted with the same key
        // produce different ciphertexts
        // IE E(IV1, P) = C1 and E(IV2, P) = C2
        // IV is ok to be known by the advisory - it will not reveal
        // anything about the key or the ciphertext/plaintext itself
        // think of an IV as a public key
        // However, an IV:
        // - Must not be reused (at least sequentially - doing the best we can with PRNG)
        // - Must be random - again usual disclaimer with PRNG
        // GenerateIV should do both for us - per Microsoft's implementation
        // Src: https://security.stackexchange.com/questions/17044/when-using-aes-and-cbc-is-it-necessary-to-keep-the-iv-secret
        public static byte[] EncryptToByte(byte[] plainText, byte[] key, int offset, int size)
        {
            byte[] encrypted = null;
            byte[] IV;
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.GenerateIV();
                IV = aesAlg.IV;
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (BinaryWriter bw = new BinaryWriter(csEncrypt))
                        {
                            bw.Write(plainText, offset, size);
                            csEncrypt.FlushFinalBlock();
                        }
                    }

                    encrypted = msEncrypt.ToArray();
                }
            }
            
            byte[] cipherWithIV = new byte[IV.Length + encrypted.Length];
            Array.Copy(IV, 0, cipherWithIV, 0, IV.Length);
            Array.Copy(encrypted, 0, cipherWithIV, IV.Length, encrypted.Length);
            return cipherWithIV;
        }

        public static string EncryptToString(string plainText, string key)
        {
            return Convert.ToBase64String(EncryptToByte(plainText, Convert.FromBase64String(key)));
        }
        
        public static string EncryptToString(byte[] plainText, string key)
        {
            return Convert.ToBase64String(EncryptToByte(plainText, Convert.FromBase64String(key)));
        }
        
        public static string EncryptToString(string plainText, byte[] key)
        {
            return Convert.ToBase64String(EncryptToByte(plainText, key));
        }

        public static byte[] DecryptToByte(byte[] cipherText, string key)
        {
            return DecryptToByte(cipherText, Convert.FromBase64String(key));
        }

        public static byte[] DecryptToByte(byte[] cipherText, byte[] key)
        {
            byte[] decrypted = null;
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                byte[] iv = new byte[aesAlg.IV.Length];
                Array.Copy(cipherText, 0, iv, 0, iv.Length);
                aesAlg.IV = iv;
                try
                {
                    using (MemoryStream msDecrypt = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, iv),
                            CryptoStreamMode.Write))
                        {
                            using (BinaryWriter bw = new BinaryWriter(cs))
                            {
                                bw.Write(cipherText, iv.Length, cipherText.Length - iv.Length);
                            }
                        }

                        decrypted = msDecrypt.ToArray();
                    }
                }
                catch (CryptographicException ce)
                {
                    Console.WriteLine("[Error]: Wrong key or invalid ciphertext");
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error]: Please create an issue in the issue tracker. Include the following:");
                    Console.WriteLine(e.ToString());
                    
                }
            }

            return decrypted;
        }

        public static string DecryptToString(string cipherText, string key)
        {
            return Encoding.UTF8.GetString(DecryptToByte(Convert.FromBase64String(cipherText), Convert.FromBase64String(key)));
        }
        
        public static string DecryptToString(byte[] cipherText, string key)
        {
            return Encoding.UTF8.GetString(DecryptToByte(cipherText, Convert.FromBase64String(key)));
        }
        
        public static string DecryptToString(byte[] cipherText, byte[] key)
        {
            return Encoding.UTF8.GetString(DecryptToByte(cipherText, key));
        }
    }
}