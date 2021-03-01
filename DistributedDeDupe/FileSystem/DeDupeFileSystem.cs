using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using SharpFileSystem;

namespace DistributedDeDupe
{
    public class DeDupeFileSystem : IFileSystem
    {
        //Dictionary<string, IFileSystem> _fileSystems = new Dictionary<string, IFileSystem>();
        List<IFileSystem> _fileSystems = new List<IFileSystem>();
        protected SQLiteDatabase db;
        protected string key;
        protected DeDupeTempFile tmpfile;

        public DeDupeFileSystem(string dbfile, string key)
        {
            db = new SQLiteDatabase(dbfile);
            this.key = key;
        }

        public void UpdateKey(string key)
        {
            this.key = key;
        }

        public void AddFileSystem(IFileSystem fs)
        {
            //_fileSystems[fs.ToString()] = fs;
            _fileSystems.Add(fs);
        }
        
        public void Dispose()
        {
            
        }

        public ICollection<VirtualFileSystemInfo> GetExtendedEntities(FileSystemPath path)
        {
            List<VirtualFileSystemInfo> ret = new List<VirtualFileSystemInfo>();
            foreach (FileSystemPath fsp in GetEntities(path))
            {
                ret.Add(new VirtualFileSystemInfo(fsp, db));
            }

            return ret;
        }

        // Here is the deal - 
        // User asks "what files/folders are in /test"
        // Using a MPTT or adjacency list will not answer that question
        // Those store tree data - but would require resolving ALL paths in the tree to find the folder
        // Obviously we could store the full path as a lookup like I do - but then what would
        // MPTT or AL get me?
        // Nothing.
        // Folders will also be stored as entities
        // so that we can just query the entities table to determine what files/folders there are
        // If a user requests what entities are in /test - we can easily look that up by querying directories
        // table then query entities with dir = directory ID
        // But what if a user requests a full dump of all files?
        // It would resolve as a GetEntities("/") then GetEntities("/test") etc
        // Src: https://stackoverflow.com/questions/6802539/hierarchical-tree-database-for-directories-path-in-filesystem
        public ICollection<FileSystemPath> GetEntities(FileSystemPath path)
        {
            DataTable dir = db.GetDataTable("SELECT * FROM directories WHERE fullpath = @path",
            new Dictionary<string, object>()
            {
                {"@path", path.Path}
            });
            DataTable res = db.GetDataTable("SELECT * FROM entities WHERE dir = @dir", new Dictionary<string, object>()
            {
                {"@dir", dir.Rows[0]["id"].ToString()}
            });
            List<FileSystemPath> files = new List<FileSystemPath>();
            //files.Add(FileSystemPath.Parse("/test.txt"));
            foreach (DataRow r in res.Rows)
            {
                FileSystemPath p = FileSystemPath.Parse(path.Path + r["fname"].ToString());
                files.Add(p);
            }

            return files;
        }

        public bool Exists(FileSystemPath path)
        {
            if (path.IsDirectory)
            {
                DataTable dir = db.GetDataTable("SELECT * FROM directories WHERE fullpath = @path",
                    new Dictionary<string, object>()
                    {
                        {"@path", path.Path}
                    });
                if (dir.Rows.Count > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                DataTable dir = db.GetDataTable("SELECT * FROM directories WHERE fullpath = @path",
                    new Dictionary<string, object>()
                    {
                        {"@path", path.ParentPath.Path}
                    });
                DataTable res = db.GetDataTable("SELECT * FROM entities WHERE dir = @dir and fname = @fname", new Dictionary<string, object>()
                {
                    {"@dir", dir.Rows[0]["id"].ToString()},
                    {"@fname", path.EntityName}
                });
                if (res.Rows.Count == 1)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        
        protected void CreateParentFolder(FileSystemPath folder)
        {
            throw new System.NotImplementedException();
            string folderID = db.ExecuteScalar("SELECT id FROM directories WHERE fullpath = @path",
                new Dictionary<string, object>()
                {
                    {"@path", folder.ParentPath}
                });
            
        }

        public void FlushTempFile()
        {
            tmpfile?.Flush();
        }
        
        public Stream CreateFile(FileSystemPath path)
        {
            tmpfile = new DeDupeTempFile(_fileSystems, path, db, key);
            return new FileStream(tmpfile.Path, FileMode.Open, FileAccess.Write);
        }

        public Stream OpenFile(FileSystemPath path, FileAccess access)
        {
            tmpfile = new DeDupeTempFile(_fileSystems, path, db, key);
            tmpfile.Download();
            return new FileStream(tmpfile.Path, FileMode.Open, FileAccess.Read);
        }

        public void CreateDirectory(FileSystemPath path)
        {
            DataTable parentDir = db.GetDataTable("SELECT * FROM directories WHERE fullpath = @path",
                new Dictionary<string, object>()
                {
                    {"@path", path.ParentPath.Path}
                });
            string sqlCreateDirectory = "INSERT INTO directories (dirname, fullpath) VALUES (@dirname, @fullpath)";
            db.ExecuteNonQuery(sqlCreateDirectory, new Dictionary<string, object>()
            {
                {"@dirname", path.EntityName},
                {"@fullpath", path.Path}
            });
            double now = DateTime.Now.UnixTimeStamp();
            string sqlCreateEntity =
                "INSERT INTO entities (fname, dir, cdate, mdate, isdir, accessdate, size) VALUES (@fname, @dir, @cdate, @mdate, @isdir, @accessdate, size)";
            db.ExecuteNonQuery(sqlCreateEntity, new Dictionary<string, object>()
            {
                {"@fname", path.EntityName},
                {"@dir", parentDir.Rows[0]["id"]},
                {"@cdate", now},
                {"@mdate", now},
                {"@isdir", 1},
                {"@accessdate", now},
                {"@size", 0}
            });

        }

        public void Delete(FileSystemPath path)
        {
            throw new System.NotImplementedException();
        }
    }
}