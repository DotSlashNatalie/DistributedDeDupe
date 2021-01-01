# DistributedDeDupe

This is a program that is used to distribute data across multiple storage systems.

# Install

Currently this works with google drive.

You will need to create an application with Google APIs to use this.

- Navigate to https://console.developers.google.com/
- Create new project
- Enable the google drive API (from member I think if you goto library on the left hand side, search for google drive, and enable)
- Go to credentials tab
- Click + Create Credentials
- Click OAuth client ID
- You can name it whatever you want
- Click on download JSON at the top
- Rename or save as  credentials.json
- Place in the same folder as the binary
- When you run the program for the first time it may spawn a web browser for you to login
- After you login it should prompt you for an auth key and it will tell you what to do with it
- After that it should prompt you for a key and place you into a CLI shell
- type `help` and you can take it from there

* The reason for these steps is that accessing google drive API is quota limited. I would rather not to have to worry about hitting a quota on my account.

# Motivation

This idea was actually in the back of my brain for several years. The original idea consisted of creating a deduplicating FUSE file system. In theory this is a great idea and has been done by many other people. Peter Odding created [http://peterodding.com/code/python/dedupfs/](such a system) in Python. There are some file systems that have it built in - [https://constantin.glez.de/2011/07/27/zfs-to-dedupe-or-not-dedupe/](such as ZFS). The problem with ZFS is that the deduplication doesn't scale - per the article you need 20GB of RAM per TB of data. If I have 6TB - I need 120GB of RAM. As a home user I simply do not have that hardware.

However, all of these projects rely on the underlying file disk. I recently lost a 6TB array and want to make sure that doesn't happen again.

# Goals

- Has to support any service (Google Drive and FTP being 2 targeted ones)
- Has to be self healing - in other words if it detects there is an issue - it alerts or attempts to repair
- There may be an agent but it MUST be usable agentless (many pieces of software require many components requiring hours of configuration)
- Has to be fast (that being said - obviously if data is stored externally your bandwidth and external resource will be the bottleneck)
- Data will be encrypted at rest
- Any new version can be automatically upgraded without manual intervention

Think of this as a distributed and deduplicated [https://github.com/vgough/encfs](encfs).

Files will be accessible via a CLI or mount*

* Mount may only be available on Linux

# Design

I will use SQLite for the database backend - but file data will be stored on remote sources. We will have to do stress tests to determine how the database will handle growth but I think it should be fine.

After doing some research - it seems to be best to use fixed block size deduplication. I will design the system to be able to leverage variable sized blocks but I think that would require programming knowledge of file types (or as wikipedia defines it - "content-aware data deduplication"). The fixed size can be specified by the user but default to 128k blocks. Testing will need to be done if deduplication increases when we increase or decrease this value. I suspect the smaller the value the greater the deduplication but slower processing times.

Just encrypt the SQLite database - 

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

We will store Jenkins hash, then a SHA-512 of the block. And use those to determine if a duplicate block has been detected.

# License

The license, unless otherwise stated in a file, is under an MIT license.

# Projects Used

- Google.Apis.Drive.v3
- [https://github.com/bobvanderlinden/sharpfilesystem](sharpfilesystem)
- System.Data.SQLite

# Similar projects

- [https://linux.die.net/man/1/fdupes](fdupes)
- [https://github.com/vgough/encfs](encfs)
- [http://peterodding.com/code/python/dedupfs/](dedupefs)