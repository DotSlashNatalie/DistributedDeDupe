using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using SharpFileSystem;

public class VirtualFileSystemInfo
{
    protected SQLiteDatabase db;
    protected DataRow entityData;
    protected FileSystemPath path;
    
    

    // Borrowed from FileSystemInfo
    public DateTime CreationTime
    {
        get => CreationTimeUtc.ToLocalTime();
    }

    public DateTime CreationTimeUtc
    {
        get => (Convert.ToDouble(entityData["cdate"].ToString())).UnixTimeStampToDateTime();
    }
    
    public DateTime LastWriteTime
    {
        get => LastWriteTimeUtc.ToLocalTime();
    }

    public DateTime LastWriteTimeUtc
    {
        get => (Convert.ToDouble(entityData["mdate"].ToString())).UnixTimeStampToDateTime();
    }
    
    public DateTime LastAccessTime
    {
        get => LastAccessTimeUtc.ToLocalTime();
    }

    public DateTime LastAccessTimeUtc
    {
        get => (Convert.ToDouble(entityData["accessdate"].ToString())).UnixTimeStampToDateTime();
    }
    
    // FileSystemInfo doesn't store size?
    public long Size => Convert.ToInt64(entityData["size"].ToString());
    
    public string Name => path.EntityName;

    public string Extension => path.GetExtension();
    
    public string FullName => path.Path;

    public string Hash => entityData["filehash"].ToString();
    
    public FileSystemPath Path => path;

    public bool IsDirectory => (bool)entityData["isdir"];

    public VirtualFileSystemInfo(FileSystemPath path, SQLiteDatabase db)
    {
        this.db = db;
        this.path = path;
        string folderID = db.ExecuteScalar("SELECT id from directories WHERE fullpath = @path",
            new Dictionary<string, object>()
            {
                {"@path", path.ParentPath.Path}
            });
        entityData = db.GetDataTable("SELECT * FROM entities WHERE dir = @dir and fname = @fileName", new Dictionary<string, object>()
        {
            {"@dir", folderID},
            {"@fileName", path.EntityName}
        }).Rows[0];


    }
}