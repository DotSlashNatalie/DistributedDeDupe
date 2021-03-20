using System;
using System.IO;
using StackOverflow;

namespace DistributedDeDupe
{
    // I designed this so that I could store the database file encrypted
    // I suppose I could have decrypted into memory to make it more
    // "secure" . But that's really security through obscurity
    // 
    public class EncryptedTempFile : TempFile
    {
        protected string _encfile;
        private byte[] _key;
        public EncryptedTempFile(string encFile, byte[] key) : base()
        {
            _encfile = encFile;
            this._key = key;
            if (System.IO.File.Exists(encFile))
                AESWrapper.DecryptFileToFile(encFile, Path, key);
        }

        public EncryptedTempFile(string encFile, string key) : base()
        {
            _encfile = encFile;
            this._key = Convert.FromBase64String(key);
            if (System.IO.File.Exists(encFile))
                AESWrapper.DecryptFileToFile(encFile, Path, key);
        }
        
        public void Flush()
        {
            AESWrapper.EncryptFileToFile(Path, _encfile, _key);
        }
        
        
    }
}