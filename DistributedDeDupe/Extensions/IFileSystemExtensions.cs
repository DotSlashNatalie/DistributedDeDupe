using System.IO;
using SharpFileSystem;

public static class IFileSystemExtensions
{
    public static Stream OpenOrCreate(this IFileSystem fs, FileSystemPath file, FileAccess access)
    {
        if (fs.Exists(file))
        {
            return fs.OpenFile(file, access);
        }
        else
        {
            return fs.CreateFile(file);
        }
    }
}