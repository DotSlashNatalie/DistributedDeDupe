using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

class DeDupeStorage
{
    protected string _file = String.Empty;
    protected Stream _stream;
    protected SQLiteDatabase _db;
    public DeDupeStorage(string file, SQLiteDatabase db)
    {
        _file = file;
        _db = db;
    }
    
    public DeDupeStorage(Stream s, SQLiteDatabase db)
    {
        _stream = s;
        _db = db;
    }

    public byte[] GetFile(string file)
    {
        DataTable t = _db.GetDataTable("SELECT * FROM filestorage where name = @name", new Dictionary<string, object>()
        {
            {"@name", file}
        });
        if (t.Rows.Count == 0)
            return null;
        long start = long.Parse(t.Rows[0]["start"].ToString());
        long end = long.Parse(t.Rows[0]["end"].ToString());
        long size = long.Parse(t.Rows[0]["size"].ToString());
        byte[] buffer = new byte[size];
        if (_file != string.Empty)
        {
            using (var fs = new FileStream(_file, FileMode.Open))
            {
                fs.Seek(start, SeekOrigin.Begin);
                fs.Read(buffer, 0, buffer.Length);

            }
        }
        else
        {
            _stream.Seek(start, SeekOrigin.Begin);
            _stream.Read(buffer, 0, buffer.Length);
        }

        return buffer;
    }

    public void AddFile(string file, byte[] data)
    {
        string check = _db.ExecuteScalar("SELECT name FROM filestorage WHERE name = @name",
            new Dictionary<string, object>()
            {
                {"@name", file}
            });
        if (check != string.Empty)
            throw new Exception("File already exists in storage - no support for overwriting");
        long start, end, size;
        size = data.Length;
        if (_file != string.Empty)
        {
            using (var fs = new FileStream(_file, FileMode.OpenOrCreate))
            {
                long endPoint = fs.Length;
                fs.Seek(endPoint, SeekOrigin.Begin);
                start = fs.Position;
                end = start + size;
                fs.Write(data);
            }
        }
        else
        {
            long endPoint = _stream.Length;
            _stream.Seek(endPoint, SeekOrigin.Begin);
            start = _stream.Position;
            end = start + size;
            _stream.Write(data);
        }
        _db.ExecuteNonQuery(
            "INSERT INTO filestorage (name, start, end, size) VALUES (@name, @start, @end, @size)",
            new Dictionary<string, object>()
            {
                {"@name", file},
                {"@start", start},
                {"@end", end},
                {"@size", size}
            });
    }
}