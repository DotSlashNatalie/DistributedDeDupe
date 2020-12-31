# DistributedDeDupe

This is a program that is used to distribute data across multiple storage systems.

# Motivation

This idea was actually in the back of my brain for several years. The original idea consisted of creating a deduplicating FUSE file system. In theory this is a great idea and has been done by many other people. Peter Odding created [http://peterodding.com/code/python/dedupfs/](such a system) in Python. There are some file systems that have it built in - [https://constantin.glez.de/2011/07/27/zfs-to-dedupe-or-not-dedupe/](such as ZFS). The problem with ZFS is that the deduplication doesn't scale - per the article you need 20GB of RAM per TB of data. If I have 6TB - I need 120GB of RAM. As a home user I simply do not have that hardware.

However, all of these projects rely on the underlying file disk. I recently lost a 6TB array and want to make sure that doesn't happen again.

# Goals

- Has to support any service (Google Drive and FTP being 2 targeted ones)
- Has to be self healing - in other words if it detects there is an issue - it alerts or attempts to repair
- There may be an agent but it MUST be usable agentless (many pieces of software require many components requiring hours of configuration)
- Has to be fast (that being said - obviously if data is stored externally your bandwidth will be the bottleneck)
- Data will be encrypted at rest
- Any new version can be automatically upgraded without manual intervention

Think of this as a distributed and deduplicated [https://github.com/vgough/encfs](encfs).

Files will be accessible via a CLI or mount*

* Mount may only be available on Linux

# Design

I will use SQLite for the database backend - but file data will be stored on remote sources. We will have to do stress tests to determine how the database will handle growth but I think it should be fine.

After doing some research - it seems to be best to use fixed block size deduplication. I will design the system to be able to leverage variable sized blocks but I think that would require programming knowledge of file types (or as wikipedia defines it - "content-aware data deduplication"). The fixed size can be specified by the user but default to 128k blocks. Testing will need to be done if deduplication increases when we increase or decrease this value. I suspect the smaller the value the greater the deduplication but slower processing times.

12/6:

Unfortunately to keep a sane design the files and folders will need to be stored unencrypted. This is due to the design of AES and random IVs.

If a user was requesting all the files in /folder1 - we would have to decrypt EVERY folder in the database to find a match. This is defeating the point of the database.

It would be more scalable if we stored block data via flat files locally (similar to encfs) but I think encfs would have the same scaling issue (it would also depend on the file system + access speeds as well).

Maybe some day I'll think of a solution to this....

wow - ok just encrypt the SQLite database - 

There is a DeleteOnClose option - which would work perfectly

```c#
using (FileStream fs = new FileStream(Path.GetTempFileName(),
       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
       4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose))
{
    // temp file exists
}
```

On *nix implementation they delete it in dispose because *nix doesn't offer this feature via the kernel - https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/IO/FileStream.Unix.cs#L269

(Windows kernel would handle the file deleting)

However, we can minimize the potential security risks by putting the file in /tmp and changing permissions. /tmp SHOULD (of course if some [https://superuser.com/questions/946054/preserving-tmp-on-reboot](system administrator) decides to change this DEFAULT behavior they are accepting the security consequences)

If malware gets root permission - then yes it would be able to read it but protecting against that is outside of the context of this application.

 

# Theory

We will store SHA-256 then perform a byte-by-byte comparison.

# License

The license, unless otherwise stated in a file, is under an MIT license.

# Projects Used

- Google.Apis.Drive.v3
- [https://github.com/bobvanderlinden/sharpfilesystem](sharpfilesystem)

# Similar projects

- [https://linux.die.net/man/1/fdupes](fdupes)
- [https://github.com/vgough/encfs](encfs)