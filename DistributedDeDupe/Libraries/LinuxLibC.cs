using System.Runtime.InteropServices;

namespace StackOverflow
{
    public class LinuxLibC
    {
        // https://stackoverflow.com/a/45135498/195722
        // https://unix.superglobalmegacorp.com/Net2/newsrc/sys/stat.h.html
        // user permissions
        const int S_IRUSR = 0x100;
        const int S_IWUSR = 0x80;
        const int S_IXUSR = 0x40;

        // group permission
        const int S_IRGRP = 0x20;
        const int S_IWGRP = 0x10;
        const int S_IXGRP = 0x8;

        // other permissions
        const int S_IROTH = 0x4;
        const int S_IWOTH = 0x2;
        const int S_IXOTH = 0x1;
    
        // I thought we would need this on Linux for the temp file
        // Turns out the temp file created has "sane" permissions
        // ie rw-------
        // This makes it so that the temp file can only be read by the user
        // and of course root
        // It's probably based on the umask?
        // At least on my system(tm) - Debian
        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);
    }
}