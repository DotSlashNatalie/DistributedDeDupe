using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DistributedDeDupe;
using SharpFileSystem;
using SharpFileSystem.IO;
using StackOverflow;
using File = SharpFileSystem.File;

public class DeDupeTempFile : TempFile
{
    protected IFileSystem fsdst;
    protected FileSystemPath vsrc;
    protected SQLiteDatabase db;
    protected string key;

    // fsdst will be the dest file system - in this case google drive
    // vsrc will be the virtual file that we want to store - ie /test/test.txt
    public DeDupeTempFile(IFileSystem fsdst, FileSystemPath vsrc, SQLiteDatabase db, string key)
    {
        this.fsdst = fsdst;
        this.vsrc = vsrc;
        this.db = db;
        this.key = key;
    }

    public void Download()
    {
        DataTable fileInfo;
        string directoryID = db.ExecuteScalar("SELECT id FROM directories WHERE fullpath = @path",
            new Dictionary<string, object>()
            {
                {"@path", vsrc.ParentPath.Path}
            });
        fileInfo =
            db.GetDataTable("SELECT * from entities where isdir = @isdir and fname = @name and dir = @dir", new Dictionary<string, object>()
            {
                
                {"@name", vsrc.EntityName},
                {"@isdir", 0},
                {"@dir", directoryID}
            });
        DataTable blocks = db.GetDataTable("SELECT * FROM fileblocks WHERE file_id = @fileid order by block_order asc", new Dictionary<string, object>()
        {
            {"@fileid", fileInfo.Rows[0]["id"]}
        });
        foreach (DataRow r in blocks.Rows)
        {
            string remotename = db.ExecuteScalar("SELECT name FROM blocks where id = @blockID",
                new Dictionary<string, object>()
                {
                    {"@blockID", r["block_id"]}
                });
            remotename = "/" + remotename;
            remotename = remotename.Replace("//", "/");
            using (Stream s = fsdst.OpenFile(FileSystemPath.Parse(remotename), FileAccess.Read))
            {
                using (FileStream f = System.IO.File.Open(this.Path, FileMode.Append, FileAccess.Write))
                {
                    byte[] buffer = s.ReadAllBytes();
                    byte[] plainText = AESWrapper.DecryptToByte(buffer, key);
                    if (plainText == null)
                    {
                        throw new Exception("[Error]: Could not decrypt file");
                    }
                    string fileHash = fileInfo.Rows[0]["filehash"].ToString(); 
                    if (fileHash != "")
                    {
                        using (SHA256 sha256 = SHA256.Create())
                        {
                            string newHash = plainText.GetSHA512(0, plainText.Length);
                            if (fileHash != newHash)
                            {
                                Console.WriteLine("[Warning]: File hashs do not match - data corruption possible!");
                            }
                        }
                    }
                    f.Write(plainText);
                }
            }
        }
        
    }

    public void Flush()
    {
        byte[] buffer = new byte[128];
        int bytesRead = 0;
        int blockCount = 0;
        string fileId = "";
        string directoryID = db.ExecuteScalar("SELECT id FROM directories WHERE fullpath = @path",
            new Dictionary<string, object>()
            {
                {"@path", vsrc.ParentPath.Path}
            });
        DataTable fileInfo =
            db.GetDataTable("SELECT * from entities where isdir = @isdir and fname = @name and dir = @dir", new Dictionary<string, object>()
            {
                
                {"@name", vsrc.EntityName},
                {"@isdir", 0},
                {"@dir", directoryID}
            });
        
        // If file does not exist - create file record
        if (fileInfo.Rows.Count == 0)
        {
            string insertEntityQuery = "INSERT INTO entities (fname, dir, size, cdate, mdate, isdir, accessdate, filehash) VALUES (@fname, @dir, @size, @cdate, @mdate, @isdir, @access, @filehash)";
            double ctime = DateTime.Now.UnixTimeStamp();
            string fileHash = "";
            using (FileStream fs = new FileStream(this.Path, FileMode.Open, FileAccess.Read))
            {
                fileHash = fs.GetSHA512();
            }
            db.ExecuteNonQuery(insertEntityQuery, new Dictionary<string, object>()
            {
                { "@fname", vsrc.EntityName },
                {"@dir", directoryID},
                {"@size", new FileInfo(Path).Length},
                {"@cdate", ctime},
                {"@mdate", ctime},
                {"@access", ctime},
                {"@isdir", 0},
                {"@filehash", fileHash}
            });
            
            fileInfo =
                db.GetDataTable("SELECT * from entities where isdir = @isdir and fname = @name and dir = @dir", new Dictionary<string, object>()
                {
                
                    {"@name", vsrc.EntityName},
                    {"@isdir", 0},
                    {"@dir", directoryID}
                });
        }
        
        fileId = fileInfo.Rows[0]["id"].ToString();
        using (FileStream f = System.IO.File.Open(this.Path, FileMode.Open, FileAccess.Read))
        {
            while ((bytesRead = f.Read(buffer, 0, buffer.Length)) > 0)
            {
                
                string hash1 = buffer.JenkinsOneAtATime();
                string id = "";
                string hash2 = "";
                string hash2Check = "";
                string hash1Check = db.ExecuteScalar("SELECT id FROM blocks WHERE hash1 = @hash1",
                    new Dictionary<string, object>()
                    {
                        {"@hash1", hash1}
                    });
                /*using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(buffer, 0, bytesRead);
                    hash2 = Convert.ToHexString(hash).ToLower();
                }*/

                hash2 = buffer.GetSHA512(0, bytesRead);
                if (hash1Check != "")
                {
                    
                    
                    hash2Check = db.ExecuteScalar("SELECT id FROM blocks WHERE hash2 = @hash2 and hash1 = @hash1",
                        new Dictionary<string, object>()
                        {
                            {"@hash2", hash2},
                            {"@hash1", hash1}
                        });
                    id = hash2Check;
                }

                if (id == "")
                {
                    // need to create block
                    Guid g = Guid.NewGuid();
                    string name = g.ToString();
                    string encName = AESWrapper.EncryptToString(name, key);
                    encName = encName.Replace("/", "\\/");
                    encName = "/" + encName;
                    encName = encName.Replace("//", "/");
                    
                    /*byte[] compressed;
                    using (System.IO.MemoryStream instream = new MemoryStream(buffer))
                    {
                        using (System.IO.MemoryStream outstream = new MemoryStream())
                        {
                            using (GZipStream s = new GZipStream(outstream, CompressionMode.Compress))
                            {
                                instream.CopyTo(s);
                            }

                            compressed = outstream.ToArray();
                        }
                    }*/
                    
                    using (Stream s = fsdst.CreateFile(FileSystemPath.Parse($"{encName}")))
                    {
                        byte[] cipher = AESWrapper.EncryptToByte(buffer, key, 0, bytesRead);
                        s.Write(cipher);
                    }

                    string blockInsertSQL =
                        "INSERT INTO blocks (hash1, size, name, location, hash2) VALUES (@hash1, @size, @name, @location, @hash2)";

                    db.ExecuteNonQuery(blockInsertSQL, new Dictionary<string, object>()
                    {
                        {"@hash1", hash1},
                        {"@size", bytesRead},
                        {"@name", encName},
                        {"@location", fsdst.ToString()},
                        {"@hash2", hash2}
                    });
                    
                    hash2Check = db.ExecuteScalar("SELECT id FROM blocks WHERE hash2 = @hash2 and hash1 = @hash1",
                        new Dictionary<string, object>()
                        {
                            {"@hash2", hash2},
                            {"@hash1", hash1}
                        });
                    id = hash2Check;

                }

                string fileBlockInsertSQL =
                    "INSERT INTO fileblocks (file_id, block_id, block_order) VALUES (@fileId, @blockId, @blockOrder)";
                db.ExecuteNonQuery(fileBlockInsertSQL, new Dictionary<string, object>()
                {
                    {"@fileId", fileId},
                    {"@blockId", id},
                    {"@blockOrder", blockCount}
                });

                blockCount++;
            }
        }
    }
}