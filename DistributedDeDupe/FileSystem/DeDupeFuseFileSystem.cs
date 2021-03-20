using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SharpFileSystem;
using SharpFileSystem.IO;
using StackOverflow;
using Tmds.Fuse;
using Tmds.Linux;
using File = System.IO.File;
using static Tmds.Linux.LibC;

namespace DistributedDeDupe
{
    class FileData
    {
        public string FileName { get; set; }
        public TempFile TFile { get; set; }
        public Stream FileStream { get; set; }
    }
    public class DeDupeFuseFileSystem : FuseFileSystemBase
    {

        private string key;
        private EncryptedTempFile dbfile;
        private SQLiteDatabase db;
        private DeDupeFileSystem fs;
        private Dictionary<ulong, FileData> _openFiles = new Dictionary<ulong, FileData>();
        
        private ulong _nextFd;
        
        public DeDupeFuseFileSystem(string key, SettingsData data)
        {
            this.key = key;
            dbfile = new EncryptedTempFile("data.sqlite.enc", key);
            db = new SQLiteDatabase(dbfile.Path);
            
            
            
            fs = new DeDupeFileSystem(dbfile.Path, key, true);
            foreach (KeyValuePair<string, string> kv in data.locations)
            {
                fs.AddFileSystem(new DeDupeLocalFileSystem(kv.Value, kv.Key));
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            dbfile.Flush();
            dbfile.Dispose();
        }

        /*public override int Truncate(ReadOnlySpan<byte> path, ulong length, FuseFileInfoRef fiRef)
        {
            //return base.Truncate(path, length, fiRef);
            Console.WriteLine("Truncate -> " + Encoding.UTF8.GetString(path));
            if (!fiRef.IsNull)
            {
                _openFiles[fiRef.Value.fh].SetLength(0);
                return 0;
            }
            return -ENOENT;
        }*/

        public override int FAllocate(ReadOnlySpan<byte> path, int mode, ulong offset, long length, ref FuseFileInfo fi)
        {
            Console.WriteLine("FAllocate -> " + Encoding.UTF8.GetString(path));
            //return base.FAllocate(path, mode, offset, length, ref fi);
            return 0;
        }

        public override int FSync(ReadOnlySpan<byte> path, ref FuseFileInfo fi)
        {
            Console.WriteLine("FSync -> " + Encoding.UTF8.GetString(path));
            //return base.FSync(path, ref fi);
            return 0;
        }

        public override int Chown(ReadOnlySpan<byte> path, uint uid, uint gid, FuseFileInfoRef fiRef)
        {
            Console.WriteLine("Chown -> " + Encoding.UTF8.GetString(path));
            return 0;
        }

        public override int ChMod(ReadOnlySpan<byte> path, mode_t mode, FuseFileInfoRef fiRef)
        {
            Console.WriteLine("ChMod -> " + Encoding.UTF8.GetString(path));
            return 0;
        }


        public override int GetAttr(ReadOnlySpan<byte> path, ref stat stat, FuseFileInfoRef fiRef)
        {
            Console.WriteLine("GetAttr -> " + Encoding.UTF8.GetString(path));
            Console.WriteLine("GetAttr -> " + fiRef.IsNull);
            //Console.WriteLine("GetAttr -> " + _openFiles.ContainsKey(fiRef.Value.fh));
            if (path.SequenceEqual(RootPath))
            {
                stat.st_mode = S_IFDIR | 0b111_111_111;
                stat.st_nlink = 2; // 2 + nr of subdirectories
                return 0;
            }

            if (!fiRef.IsNull)
            {
                Console.WriteLine("GetAttr -> " + "fiRef not null");
                stat.st_mode = S_IFREG | 0b111_111_111;
                stat.st_size = 0;
                stat.st_atim = DateTime.Now.ToTimespec();
                stat.st_ctim = DateTime.Now.ToTimespec();
                stat.st_mtim = DateTime.Now.ToTimespec();
                stat.st_nlink = 1;
                return 0;
            }

            string strPath = Encoding.UTF8.GetString(path);
            if (strPath == "/stats")
            {
                Console.WriteLine("GetAttr -> " + "stats file");
                stat.st_mode = S_IFREG | 0b111_111_111;
                stat.st_size = GetStats().Length;
                stat.st_atim = DateTime.Now.ToTimespec();
                stat.st_ctim = DateTime.Now.ToTimespec();
                stat.st_mtim = DateTime.Now.ToTimespec();
                stat.st_nlink = 1;
                return 0;
            }
            
            var ents = fs.GetExtendedEntities(FileSystemPath.Parse(strPath).ParentPath);
            if (ents.Count == 0)
            {
                return -ENOENT;
            }

            foreach (var e in ents)
            {
                //Console.WriteLine("fullname -> " + e.FullName);
                if (e.FullName == Encoding.UTF8.GetString(path))
                {
                    if (e.IsDirectory)
                    {
                        stat.st_mode = S_IFDIR | 0b111_111_111;
                        stat.st_atim = e.LastAccessTime.ToTimespec();
                        stat.st_ctim = e.CreationTime.ToTimespec();
                        stat.st_mtim = e.LastWriteTime.ToTimespec();
                        stat.st_nlink = 2;
                        return 0;
                    }
                    else
                    {
                        stat.st_mode = S_IFREG | 0b111_111_111;
                        stat.st_size = e.Size;
                        stat.st_atim = e.LastAccessTime.ToTimespec();
                        stat.st_ctim = e.CreationTime.ToTimespec();
                        stat.st_mtim = e.LastWriteTime.ToTimespec();
                        stat.st_nlink = 1;
                        return 0;
                    }
                }
            }
            
            Console.WriteLine("GetAttr -> " + "-ENOENT");
            return -ENOENT;
            
        }

        public override int ReadDir(ReadOnlySpan<byte> path, ulong offset, ReadDirFlags flags, DirectoryContent content, ref FuseFileInfo fi)
        {
            
            Console.WriteLine("ReadDir -> " + Encoding.UTF8.GetString(path));
            string convPath = Encoding.UTF8.GetString(path);
            try
            {
                FileSystemPath fsPath;
                bool exists = false;
                ICollection<VirtualFileSystemInfo> ents;
                if (convPath == "/")
                {
                    fsPath = FileSystemPath.Parse("/");
                    exists = true;
                    content.AddEntry("stats");
                }
                else
                {
                    fsPath = FileSystemPath.Parse(convPath + "/");
                    exists = fs.Exists(FileSystemPath.Parse(Encoding.UTF8.GetString(path)));
                }

                ents = fs.GetExtendedEntities(fsPath);

                if (ents.Count == 0 && !exists)
                {
                    Console.WriteLine("no files!!!");
                    return -ENOENT;
                }

                content.AddEntry(".");
                content.AddEntry("..");
                foreach (var e in ents)
                {
                    content.AddEntry(e.Name);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return 0;
        }

        public override int UpdateTimestamps(ReadOnlySpan<byte> path, ref timespec atime, ref timespec mtime, FuseFileInfoRef fiRef)
        {
            Console.WriteLine("UpdateTimestamps -> " + Encoding.UTF8.GetString(path));
            return 0;
        }

        public override int Rename(ReadOnlySpan<byte> path, ReadOnlySpan<byte> newPath, int flags)
        {
            Console.WriteLine("Rename -> " + Encoding.UTF8.GetString(path));
            return 0;
        }

        public override int Create(ReadOnlySpan<byte> path, mode_t mode, ref FuseFileInfo fi)
        {
            
            string name = Encoding.UTF8.GetString(path);
            Console.WriteLine($"Create -> {name}");
            ulong fd = FindFreeFd();
            fi.fh = fd;
            TempFile f = new TempFile();
            FileData file = new FileData()
            {
                FileName = name,
                TFile = f,
                FileStream = new FileStream(f.Path, FileMode.OpenOrCreate)
            };
            _openFiles.Add(fd, file);
            fi.direct_io = true;
            return 0;
        }

        protected string GetStats()
        {
            long entitySpaceUsed = long.Parse(db.ExecuteScalar("SELECT SUM(size) FROM entities"));
            long blockSpaceUsed = long.Parse(db.ExecuteScalar("SELECT SUM(size) FROM blocks"));
            long chunks = long.Parse(db.ExecuteScalar("SELECT count(id) from blocks"));
            long diffSpace = entitySpaceUsed - blockSpaceUsed;
            string stats = "";
            stats += $"Space used for entites => {entitySpaceUsed.GetBytesReadable()}\n";
            stats += $"Space used for blocks => {blockSpaceUsed.GetBytesReadable()}\n";
            stats += $"Space saved => {diffSpace.GetBytesReadable()}\n";
            stats += $"Blocks used => {chunks}\n";
            stats += "\n";
            return stats;
        }

        public override int Read(ReadOnlySpan<byte> path, ulong offset, Span<byte> buffer, ref FuseFileInfo fi)
        {
            string name = Encoding.UTF8.GetString(path);
            Console.WriteLine($"Read -> {name}");
            if (name == "/stats")
            {
                
                byte[] data = Encoding.UTF8.GetBytes(GetStats());
                data.CopyTo(buffer);
                return data.Length;
            }
            else
            {
                _openFiles[fi.fh].FileStream.Position = (long) offset;
                return _openFiles[fi.fh].FileStream.Read(buffer);
            }
        }

        public override int Write(ReadOnlySpan<byte> path, ulong off, ReadOnlySpan<byte> span, ref FuseFileInfo fi)
        {
            //return base.Write(path, off, span, ref fi);
            string name = Encoding.UTF8.GetString(path);
            Console.WriteLine($"Write -> {name}");
            try
            {
                //_openFiles[fi.fh].Position = (long) off;
                FileData f = _openFiles[fi.fh];
                if(f.TFile != null)
                    f.FileStream.Write(span);
                //fi.direct_io = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return span.Length;
        }

        public override int Open(ReadOnlySpan<byte> path, ref FuseFileInfo fi)
        {
            string name = Encoding.UTF8.GetString(path);
            Console.WriteLine($"Open -> {name}");
            if (name == "")
                return -ENOENT;

            if ((fi.flags & O_ACCMODE) == O_RDONLY)
            {
                if (name == "/stats")
                {
                    return 0;
                }
                ulong fd = FindFreeFd();
                fi.fh = fd;
                fi.direct_io = true;
                FileData f = new FileData()
                {
                    FileName = name,
                    FileStream = fs.OpenFile(FileSystemPath.Parse(name),
                        FileAccess.Read)
                };
                _openFiles.Add(fd, f);

                return 0;
            }
            else
            {
                return -EACCES;
            }
        }

        private ulong FindFreeFd()
        {
            while (true)
            {
                ulong fd = unchecked(_nextFd++);
                if (!_openFiles.ContainsKey(fd))
                {
                    return fd;
                }
            }
        }

        public override int MkDir(ReadOnlySpan<byte> path, mode_t mode)
        {
            string name = Encoding.UTF8.GetString(path);
            fs.CreateDirectory(FileSystemPath.Parse(name + "/"));
            dbfile.Flush();
            return 0;
        }

        /*public override int Flush(ReadOnlySpan<byte> path, ref FuseFileInfo fi)
        {
            //return base.Flush(path, ref fi);
            
            _openFiles[fi.fh].Flush();
            if (_openFiles[fi.fh].TFile != null)
                _openFiles[fi.fh].TFile.Dispose();
            return 0;
        }*/

        public override void Release(ReadOnlySpan<byte> path, ref FuseFileInfo fi)
        {
            string name = Encoding.UTF8.GetString(path);
            Console.WriteLine("Release -> " + name);
            FileData f = _openFiles[fi.fh];
            if (f.TFile != null)
            {
                f.FileStream.Position = 0;
                byte[] data = f.FileStream.ReadAllBytes();
                using (Stream fstream = fs.CreateFile(FileSystemPath.Parse(f.FileName)))
                {
                    fstream.Write(data, 0, data.Length);
                }

                fs.FlushTempFile();
                dbfile.Flush();
                f.TFile.Dispose();
            }

            _openFiles.Remove(fi.fh);

        }
    }
}