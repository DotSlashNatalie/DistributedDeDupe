using System;
using System.Collections.Generic;

public class VirtualDirectoryListing
{
    public static string List(ICollection<VirtualFileSystemInfo> fsinfo)
    {
        string formatStringTop = "total {0}\n";
        string formatListingFile = "{0,-10} {1,10} {2, 10}\n";
        string formatListingDirectory = "{0,-10} {1,10} {2, 10}/\n";
        // Maybe localize the date format?
        string dateFormat = "MMM dd yyyy HH:mm";
        string ret = String.Format(formatStringTop, fsinfo.Count);
        foreach (VirtualFileSystemInfo fs in fsinfo)
        {
            if (fs.IsDirectory)
            {
                ret += String.Format(formatListingDirectory, 0, fs.LastWriteTime.ToString(dateFormat), fs.Name);
            }
            else
            {
                ret += String.Format(formatListingFile, fs.Size.GetBytesReadable(), fs.LastWriteTime.ToString(dateFormat), fs.Name);
            }
        }

        return ret;
    }
    
    public static string ListWithHash(ICollection<VirtualFileSystemInfo> fsinfo)
    {
        string formatStringTop = "total {0}\n";
        string formatListingFile = "{0,-10} {1,10} {2, 10} {3, 10}\n";
        string formatListingDirectory = "{0,-10} {1,10} {2, 10}/\n";
        // Maybe localize the date format?
        string dateFormat = "MMM dd yyyy HH:mm";
        string ret = String.Format(formatStringTop, fsinfo.Count);
        foreach (VirtualFileSystemInfo fs in fsinfo)
        {
            if (fs.Path.IsDirectory)
            {
                ret += String.Format(formatListingDirectory, 0, fs.LastWriteTime.ToString(dateFormat), fs.Name);
            }
            else
            {
                ret += String.Format(formatListingFile, fs.Size.GetBytesReadable(), fs.LastWriteTime.ToString(dateFormat), fs.Name, fs.Hash);
            }
        }

        return ret;
    }
}