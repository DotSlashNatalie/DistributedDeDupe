using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DistributedDeDupe;
using SharpFileSystem;
using SharpFileSystem.IO;
using StackOverflow;
using File = SharpFileSystem.File;

public class DeDupeTempFile : TempFile
{
    protected List<IFileSystem> fsdst;
    protected FileSystemPath vsrc;
    protected SQLiteDatabase db;
    protected string key;
    protected readonly string DATAFILE = "data.dedupe";
    protected bool disableProgress;

    // fsdst will be the dest file system - in this case google drive
    // vsrc will be the virtual file that we want to store - ie /test/test.txt
    public DeDupeTempFile(List<IFileSystem> fsdst, FileSystemPath vsrc, SQLiteDatabase db, string key, bool disableProgress = false)
    {
        this.disableProgress = disableProgress;
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
        
        // While we write the blocks to multiple -  
        // We only need data from one file system -
        // I want to test the file system to make sure it is working before attempting access
        int fsToUse = 0;
        bool continueSearch = false;
        for (int i = 0; i < fsdst.Count; i++)
        {
            try
            {
                using (Stream s = fsdst[fsToUse].CreateFile(FileSystemPath.Parse("/test")))
                {

                }

                continueSearch = false;
            }
            catch (Exception)
            {
                Console.WriteLine("[Error]: file system - " + fsdst[fsToUse].ToString() + " missing - trying a backup");
                continueSearch = true;
                fsToUse++;
            }

            if (!continueSearch)
                break;
        }
        
        DataTable blocks = db.GetDataTable(@"SELECT * FROM fileblocks 
        inner join blocks on blocks.id = fileblocks.block_id
        WHERE file_id = @fileid 
        and blocks.location = @location
        order by block_order asc"
            , new Dictionary<string, object>()
        {
            {"@fileid", fileInfo.Rows[0]["id"]},
            {"@location", fsdst[fsToUse].ToString()}
        });
        using (ProgressBar pb = new ProgressBar(disableProgress))
        {
            Int64 rowCount = 0;
            foreach (DataRow r in blocks.Rows)
            {
                pb.Report((((double)rowCount)/blocks.Rows.Count));
                rowCount += 1;
                
                
                
                /*string remotename = db.ExecuteScalar("SELECT name FROM blocks where id = @blockID and location = @name",
                    new Dictionary<string, object>()
                    {
                        {"@blockID", r["block_id"]},
                        {"@name", fsdst[fsToUse].ToString()}
                    });*/
                string remotename = r["name"].ToString();
                remotename = "/" + remotename;
                remotename = remotename.Replace("//", "/");

                
                using (var fstream = fsdst[fsToUse].OpenFile(FileSystemPath.Parse($"/{DATAFILE}"), FileAccess.Read))
                {
                    DeDupeStorage storage = new DeDupeStorage(fstream, db);
                    byte[] buffer = storage.GetFile(remotename);
                    byte[] plainText = AESWrapper.DecryptToByte(buffer, key);
                    using (FileStream f = System.IO.File.Open(this.Path, FileMode.Append, FileAccess.Write))
                    {
                        f.Write(plainText);
                    }
                }
                
                /*using (var fstream = fsdst[fsToUse].OpenFile(FileSystemPath.Parse($"/{DATAFILE}"), FileAccess.Read))
                {
                    DeDupeStorage storage = new DeDupeStorage(fstream, db);
                    byte[] buffer = storage.GetFile(remotename);
                    byte[] plainText = AESWrapper.DecryptToByte(buffer, key);
                    using (var zippedStream = new MemoryStream(plainText))
                    {
                        using (var archive = new ZipArchive(zippedStream, ZipArchiveMode.Read))
                        {
                            var ent = archive.GetEntry("data.bin");
                            using (var zipent = ent.Open())
                            {
                                byte[] bufferinner = zipent.ReadAllBytes();
                                using (FileStream f = System.IO.File.Open(this.Path, FileMode.Append, FileAccess.Write))
                                {
                                    f.Write(bufferinner);
                                }
                                //fstream.Write(bufferinner);
                            }
                        }
                    }
                }*/

                /*using (var fstream = fsdst[fsToUse].OpenFile(FileSystemPath.Parse($"/{DATAFILE}"), FileAccess.Read))
                {
                    using (var zipfile = new ZipArchive(fstream, ZipArchiveMode.Read))
                    {
                        var ent = zipfile.GetEntry(remotename);
                        using (var zipent = ent.Open())
                        {
                            byte[] buffer = zipent.ReadAllBytes();
                            byte[] plainText = AESWrapper.DecryptToByte(buffer, key);
                            
                            if (plainText == null)
                            {
                                throw new Exception("[Error]: Could not decrypt file");
                            }

                            using (FileStream f = System.IO.File.Open(this.Path, FileMode.Append, FileAccess.Write))
                            {
                                f.Write(plainText);
                            }
                        }
                    }
                }*/
                
                
                
                /*using (Stream s = fsdst[fsToUse].OpenFile(FileSystemPath.Parse(remotename), FileAccess.Read))
                {
                    
                    using (FileStream f = System.IO.File.Open(this.Path, FileMode.Append, FileAccess.Write))
                    {
                        byte[] buffer = s.ReadAllBytes();
                        byte[] plainText = AESWrapper.DecryptToByte(buffer, key);
                        if (plainText == null)
                        {
                            throw new Exception("[Error]: Could not decrypt file");
                        }

                        f.Write(plainText);
                    }
                }*/
            }
            string fileHash = fileInfo.Rows[0]["filehash"].ToString();
            if (fileHash != "")
            {

                string newHash = "";
                using (FileStream fs = System.IO.File.Open(this.Path, FileMode.Open, FileAccess.Read))
                {
                    newHash = fs.GetSHA512();
                }
                if (fileHash != newHash)
                {
                    Console.WriteLine("[Warning]: File hashs do not match - data corruption possible!");
                }
            }
        }

    }

    public void Flush()
    {
        byte[] buffer = new byte[4096];
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
            Int64 lenthOfFile = f.Length;
            Int64 totalRead = 0;
            using (ProgressBar pb = new ProgressBar(disableProgress))
            {
                while ((bytesRead = f.Read(buffer, 0, buffer.Length)) > 0)
                {
                    pb.Report((((double)totalRead)/lenthOfFile));
                    totalRead = totalRead + bytesRead;
                    string hash1 = buffer.JenkinsOneAtATime();
                    string id = "";
                    string hash2 = "";
                    string hash2Check = "";
                    hash2 = buffer.GetSHA512(0, bytesRead);
                    
                    hash2Check = db.ExecuteScalar("SELECT id FROM blocks WHERE hash2 = @hash2 and hash1 = @hash1",
                        new Dictionary<string, object>()
                        {
                            {"@hash2", hash2},
                            {"@hash1", hash1}
                        });
                    id = hash2Check;
                    

                    if (id == "")
                    {
                        // need to create block
                        Guid g = Guid.NewGuid();
                        string name = g.ToString();
                        string encName = AESWrapper.EncryptToString(name, key);
                        encName = encName.Replace("/", "_");
                        encName = "/" + encName;
                        encName = encName.Replace("//", "__");

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


                        

                        foreach (IFileSystem fs in fsdst)
                        {
                            /*try
                            {
                                using (Stream s = fs.CreateFile(FileSystemPath.Parse("/test")))
                                {

                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("[Error]: file system - " + fs.ToString() + " is unreachable.");
                                continue;
                            }*/
                            //fs.
                            
                            using (var fstream = fs.OpenOrCreate(FileSystemPath.Parse($"/{DATAFILE}"),
                                FileAccess.ReadWrite))
                            {
                                //byte[] cipher = AESWrapper.EncryptToByte(buffer, key, 0, bytesRead);
                                DeDupeStorage storage = new DeDupeStorage(fstream, db);

                                byte[] cipher = AESWrapper.EncryptToByte(buffer, key, 0, bytesRead);
                                storage.AddFile(encName, cipher);
                                    
                            }

                            /*using (var fstream = fs.OpenOrCreate(FileSystemPath.Parse($"/{DATAFILE}"),
                                FileAccess.ReadWrite))
                            {
                                //byte[] cipher = AESWrapper.EncryptToByte(buffer, key, 0, bytesRead);
                                DeDupeStorage storage = new DeDupeStorage(fstream, db);
                                using (var zippedStream = new MemoryStream())
                                {
                                    using (var archive = new ZipArchive(zippedStream, ZipArchiveMode.Update))
                                    {
                                        var ent = archive.CreateEntry("data.bin", CompressionLevel.Optimal);
                                        using (var zipent = ent.Open())
                                        {
                                            zipent.Write(buffer, 0, bytesRead);
                                        }
                                    }

                                    byte[] zipData = zippedStream.ToArray();
                                    byte[] cipher = AESWrapper.EncryptToByte(zipData, key, 0, zipData.Length);
                                    storage.AddFile(encName, cipher);
                                    
                                }
                            }*/

                            /*using (var fstream = fs.OpenOrCreate(FileSystemPath.Parse($"/{DATAFILE}"), FileAccess.ReadWrite))
                            {
                                using (var zip = new ZipArchive(fstream, ZipArchiveMode.Update))
                                {
                                    var ent = zip.CreateEntry(encName, CompressionLevel.Optimal);
                                    using (var entfs = ent.Open())
                                    {
                                        byte[] cipher = AESWrapper.EncryptToByte(buffer, key, 0, bytesRead);
                                        entfs.Write(cipher);
                                    }
                                }
                            }*/
                            
                            /*using (Stream s = fs.CreateFile(FileSystemPath.Parse($"{encName}")))
                            {
                                byte[] cipher = AESWrapper.EncryptToByte(buffer, key, 0, bytesRead);
                                s.Write(cipher);
                            }*/

                            string blockInsertSQL =
                                "INSERT INTO blocks (hash1, size, name, location, hash2) VALUES (@hash1, @size, @name, @location, @hash2)";

                            db.ExecuteNonQuery(blockInsertSQL, new Dictionary<string, object>()
                            {
                                {"@hash1", hash1},
                                {"@size", bytesRead},
                                {"@name", encName},
                                {"@location", fs.ToString()},
                                {"@hash2", hash2}
                            });

                            hash2Check = db.ExecuteScalar(
                                "SELECT id FROM blocks WHERE hash2 = @hash2 and hash1 = @hash1 and location = @location",
                                new Dictionary<string, object>()
                                {
                                    {"@hash2", hash2},
                                    {"@hash1", hash1},
                                    {"@location", fs.ToString()}
                                });
                            id = hash2Check;

                            string fileBlockInsertSQL =
                                "INSERT INTO fileblocks (file_id, block_id, block_order) VALUES (@fileId, @blockId, @blockOrder)";
                            db.ExecuteNonQuery(fileBlockInsertSQL, new Dictionary<string, object>()
                            {
                                {"@fileId", fileId},
                                {"@blockId", id},
                                {"@blockOrder", blockCount}
                            });
                        }
                    


                    }
                    else
                    {
                        string fileBlockInsertSQL =
                            "INSERT INTO fileblocks (file_id, block_id, block_order) VALUES (@fileId, @blockId, @blockOrder)";
                        db.ExecuteNonQuery(fileBlockInsertSQL, new Dictionary<string, object>()
                        {
                            {"@fileId", fileId},
                            {"@blockId", id},
                            {"@blockOrder", blockCount}
                        });
                    }

                    blockCount++;
                }
            }
        }
    }
}