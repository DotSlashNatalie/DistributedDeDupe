using System;
using System.Text;
using NUnit.Framework;
using DistributedDeDupe;
using Microsoft.VisualStudio.TestPlatform.Common.Utilities;


namespace DistributedDeDupeTesting
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            
        }

        // Test cases copied from:
        // Src: https://en.wikipedia.org/wiki/Jenkins_hash_function
        [Test]
        public void JenkinsOneAtATime()
        {
            Assert.AreEqual("ca2e9442", "a".JenkinsOneAtATime());
            Assert.AreEqual("519e91f5", "The quick brown fox jumps over the lazy dog".JenkinsOneAtATime());
        }

        [Test]
        public void AESEncryption1()
        {
            string keyString = "unicorn";
            string plainText = "ATTACK AT DAWN";
            byte[] key = AESWrapper.GenerateKeyByte(keyString, AESWrapper.GenerateSaltString());
            byte[] enc = AESWrapper.EncryptToByte(plainText, key);
            Assert.AreNotEqual(plainText, Encoding.UTF8.GetString(enc));
            // Just making sure...
            Assert.AreNotEqual(Encoding.UTF8.GetBytes(plainText), enc);
        }
        
        [Test]
        public void AESEncryption2()
        {
            string keyString = "unicorn";
            string plainText = "ATTACK AT DAWN";
            byte[] key = AESWrapper.GenerateKeyByte(keyString, AESWrapper.GenerateSaltString(), 20000);
            byte[] enc = AESWrapper.EncryptToByte(plainText, key);
            Assert.AreNotEqual(plainText, Encoding.UTF8.GetString(enc));
            // Just making sure...
            Assert.AreNotEqual(Encoding.UTF8.GetBytes(plainText), enc);
        }
        
        [Test]
        public void AESFileTest1()
        {
            string keyString = "unicorn";
            string plainText = "ATTACK AT DAWN";
            string fileName = "test.txt";
            byte[] key = AESWrapper.GenerateKeyByte(keyString, AESWrapper.GenerateSaltString(), 20000);
            byte[] enc = AESWrapper.EncryptToByte(plainText, key);
            System.IO.File.WriteAllBytes(fileName, enc);
            using (EncryptedTempFile f = new EncryptedTempFile(fileName, key))
            {
                string fileContents = System.IO.File.ReadAllText(f.Path);
                Assert.AreEqual(fileContents, plainText);
            }
            System.IO.File.Delete(fileName);
        }
        
        [Test]
        public void AESFileTest2()
        {
            string keyString = "unicorn";
            string bogusKeyString = "horsies";
            string plainText = "ATTACK AT DAWN";
            string fileName = "test.txt";
            string encFileName = "text.enc";
            string salt = AESWrapper.GenerateSaltString();
            byte[] key = AESWrapper.GenerateKeyByte(keyString, salt, 20000);
            byte[] invalidKey = AESWrapper.GenerateKeyByte(bogusKeyString, salt, 20000);
            System.IO.File.WriteAllText(fileName, plainText);
            AESWrapper.EncryptFileToFile(fileName, encFileName, key);
            byte[] enc = System.IO.File.ReadAllBytes(encFileName);
            Assert.AreEqual(AESWrapper.DecryptToString(enc, key), plainText);
            bool decryptionFailed = false;
            try
            {
                Assert.AreEqual(AESWrapper.DecryptToString(enc, invalidKey), plainText);
            }
            catch (Exception e)
            {
                decryptionFailed = true;
            }
            Assert.IsTrue(decryptionFailed);
            
            System.IO.File.Delete(fileName);
            System.IO.File.Delete(encFileName);
        }
        
        [Test]
        public void AESDecryption1()
        {
            string keyString = "unicorn";
            string plainText = "ATTACK AT DAWN";
            byte[] key = AESWrapper.GenerateKeyByte(keyString, AESWrapper.GenerateSaltString(), 20000);
            byte[] enc = AESWrapper.EncryptToByte(plainText, key);
            string dec = AESWrapper.DecryptToString(enc, key);
            Assert.AreEqual(plainText, dec);
        }
        
        [Test]
        public void AESDecryption2()
        {
            string keyString = "unicorn";
            string plainText = "ATTACK AT DAWN";
            string salt = AESWrapper.GenerateSaltString();
            byte[] key = AESWrapper.GenerateKeyByte(keyString, salt, 20000);
            byte[] key2 = AESWrapper.GenerateKeyByte(keyString, salt);
            byte[] enc = AESWrapper.EncryptToByte(plainText, key);
            bool decryptionFailed = false;
            try
            {
                string dec = AESWrapper.DecryptToString(enc, key2);
            }
            catch (Exception e)
            {
                decryptionFailed = true;
            }
            Assert.IsTrue(decryptionFailed);
        }
        
        [Test]
        public void AESDecryption3()
        {
            string keyString = "unicorn";
            string plainText = "ATTACK AT DAWN";
            string salt = AESWrapper.GenerateSaltString();
            string salt2 = AESWrapper.GenerateSaltString();
            byte[] key = AESWrapper.GenerateKeyByte(keyString, salt);
            byte[] key2 = AESWrapper.GenerateKeyByte(keyString, salt2);
            byte[] enc = AESWrapper.EncryptToByte(plainText, key);
            bool decryptionFailed = false;
            try
            {
                string dec = AESWrapper.DecryptToString(enc, key2);
            }
            catch (Exception e)
            {
                decryptionFailed = true;
            }
            Assert.IsTrue(decryptionFailed);
        }
        
        
    }
}