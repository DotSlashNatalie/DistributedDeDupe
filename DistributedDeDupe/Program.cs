using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Google.Apis.Drive.v3;
using SharpFileSystem;
using SharpFileSystem.IO;
using File = System.IO.File;

namespace DistributedDeDupe
{
    class Prompt
    {
        private DeDupeFileSystem fs;

        
        private void setUpDatabase(SQLiteDatabase db)
        {
            // Not that this matters - as input is not coming from externally....
            // But I'm sure some junior cyber "expert" would bitch about it so....
            db.ExecuteNonQuery(System.IO.File.ReadAllText( System.IO.Directory.GetCurrentDirectory() + "/migrations/sqlite/1.sql"));
            //db.ExecuteNonQuery("INSERT INTO settings (`key`, `value`) VALUES ('version', '1.0')");
            db.ExecuteNonQuery("INSERT INTO settings (`key`, `value`) VALUES (@setting, @value)", new Dictionary<string, object>()
            {
                {"@setting", "version"},
                {"@value", "1.0"}
            });
            //db.ExecuteNonQuery("INSERT INTO settings (`key`, `value`) VALUES ('migration', '1')");
            db.ExecuteNonQuery("INSERT INTO settings (`key`, `value`) VALUES (@setting, @value)",new Dictionary<string, object>()
            {
                {"@setting", "migration"},
                {"@value", "1"}
            });
        }

        private void runMigrations(SQLiteDatabase db)
        {
            string[] files = System.IO.Directory.GetFiles(System.IO.Directory.GetCurrentDirectory() + "/migrations/sqlite/").Select(Path.GetFileName).ToArray();
            files = files.OrderByNatural(file => file).ToArray();
            //int lastMigration = Int32.Parse(db.ExecuteScalar("SELECT value FROM settings WHERE key = 'migration'"));
            int lastMigration = Int32.Parse(db.ExecuteScalar("SELECT value FROM settings WHERE key = @migration", new Dictionary<string, object>()
            {
                {"@migration", "migration"}
            }));
            Log.Instance.Add($"lastMigration = {lastMigration}");
            if (files.Length > 0)
            {
                int highestMigration = lastMigration;
                foreach (string file in files)
                {
                    int migrationNumber = Int32.Parse(file.Split('.')[0]);
                    Log.Instance.Add($"migrationNumber = {migrationNumber}");
                    if (migrationNumber > lastMigration)
                    {
                        db.ExecuteNonQuery(System.IO.File.ReadAllText( System.IO.Directory.GetCurrentDirectory() + $"/migrations/sqlite/{migrationNumber}.sql"));
                        highestMigration = migrationNumber;
                        Log.Instance.Add($"highestMigration = {highestMigration}");
                    }
                }

                //db.ExecuteNonQuery($"UPDATE settings set key = '{highestMigration}' WHERE value = 'migration'");
                Log.Instance.Add($"key = {highestMigration}");
                db.ExecuteNonQuery("UPDATE settings set value = @highestMigration WHERE key = @migration",
                new Dictionary<string, object>()
                {
                    {"@highestMigration", highestMigration},
                    {"@migration", "migration"}
                });
                
            }
        }
        
        public string ParseDotDot(string currentDirectory, string path)
        {
            string fullPath;
            if (path.StartsWith("/"))
            {
                fullPath = currentDirectory + path;
            }
            else
            {
                fullPath = currentDirectory + "/" + path;
            }

            string[] pathParts = fullPath.Split("/");
            List<string> newPath = new List<string>();
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (pathParts[i].Trim() == String.Empty)
                    continue;
                if (pathParts[i] == ".." && i > 1)
                {
                    newPath.RemoveAt(newPath.Count - 1);
                }
                else if (pathParts[i] != "..")
                {
                    newPath.Add(pathParts[i]);
                }
            }

            return "/" + String.Join("/", newPath).Replace("//", "/");

        }

        public void ShowHelp()
        {
            Console.WriteLine("DistributedDeDupe - program to deduplicate and encrypt files in cloud storage");
            Console.WriteLine("Version: 0.2b");
            Console.WriteLine("Commands:");
            Console.WriteLine("ls - directory listing");
            Console.WriteLine("ll - long directory listing (includes hash)");
            Console.WriteLine("mkdir - make directory");
            Console.WriteLine("cd - change directory");
            Console.WriteLine("changekey - change the key in memory in case it was mistyped");
            Console.WriteLine("showsettings - show settings");
            Console.WriteLine("generate - generates a new settings file");
            Console.WriteLine("*WARNING*");
            Console.WriteLine("If you haven't backed up your settings file and overwrite the one you have");
            Console.WriteLine("You will never be able to decrypt the files you have in remote storage");
            Console.WriteLine("This is really just for testing or an initial setup");
            Console.WriteLine("*WARNING*");
            Console.WriteLine("put [file] - puts a file into the remote storage(s)");
            Console.WriteLine("get [remote file] [local file] - gets a file, block by block, from the remote storage and places it in local file");
            Console.WriteLine("localcat [file] - attempts to decrypt a file with the key in memory and output to console");
            Console.WriteLine("remotecat [file] - downloads a remote file and attempts to decrypt a file with the key in memory and output to console");
            Console.WriteLine("decryptdb [file] - this decrypts the db for manual inspection");
            Console.WriteLine("addstorage [location] [name] - this adds a mounted physical location");
        }
        public SettingsData GenerateSettings()
        {
            //SettingsData data = SettingsFile.Read("settings.xml");
            SettingsData data;
            if (File.Exists("settings.xml"))
            {
                Console.Write("settings.xml file detected - are you sure you want to overwrite [y]/n? ");
                string choice = Console.ReadLine();
                if (choice.Trim() != "y" && choice.Trim() != "yes")
                {
                    return null;
                }
                data = SettingsFile.Read("settings.xml");
            }
            Console.Write("How many iterations do you want when generating the key [10000] ? ");
            string resp = Console.ReadLine();
            int iterations;
            if (resp.Trim() == String.Empty)
                iterations = 10000;
            else
                iterations = Convert.ToInt32(resp.Trim());
                        
            Console.Write("How many bytes do you want the salt to be [512] ? ");
            resp = Console.ReadLine();
            int saltBytes;
            if (resp.Trim() == String.Empty)
                saltBytes = 512;
            else
                saltBytes = Convert.ToInt32(resp.Trim());
                        
            data = new SettingsData() {iterations = iterations, salt = AESWrapper.GenerateSaltString(saltBytes)};
            return data;
        }

        public void Run()
        {
            string input = "";
            FileSystemPath currentDirectory = FileSystemPath.Parse("/");
            SettingsData data;
            if (File.Exists("settings.xml"))
            {
                data = SettingsFile.Read("settings.xml");
            }
            else
            {
                Console.WriteLine("Can't find settings.xml file - attempting to generate one");
                data = GenerateSettings();
                SettingsFile.Write(data, "settings.xml");
            }

            string[] Scopes = { DriveService.Scope.Drive,DriveService.Scope.DriveFile };
            string key = AESWrapper.GenerateKeyString(ConsoleEx.Password("Key: "), data.salt, data.iterations,
                data.keySize);

            using (EncryptedTempFile dbfile = new EncryptedTempFile("data.sqlite.enc", key))
            {
                SQLiteDatabase db = new SQLiteDatabase(dbfile.Path);
                var res = db.GetDataTable("PRAGMA table_info(settings)");
                if (res.Rows.Count == 0)
                {
                    this.setUpDatabase(db);
                }
                this.runMigrations(db);
                dbfile.Flush();
                //GDriveFileSystem gdrive = new GDriveFileSystem(Scopes, "DistrubtedDeDupe", dbfile.Path);
                Log.Instance.Write("log.txt");

                
                DeDupeFileSystem fs = new DeDupeFileSystem(dbfile.Path, key);
                foreach (KeyValuePair<string, string> kv in data.locations)
                {
                    fs.AddFileSystem(new DeDupeLocalFileSystem(kv.Value, kv.Key));
                }
                //fs.AddFileSystem(gdrive);
                string fileName;
                byte[] fileData;
                do
                {
                    if (currentDirectory.Path == "/")
                        Console.Write($"#:{currentDirectory.Path}> ");
                    else
                        Console.Write($"#:{currentDirectory.Path.TrimEnd('/')}> ");
                    input = Console.ReadLine();
                    switch (input.Split(" ")[0])
                    {
                        case "addstorage":
                            string location = input.Split(" ")[1].Trim();
                            string name = input.Split(" ")[2].Trim();
                            data.locations[name] = Path.GetFullPath(location);
                            fs.AddFileSystem(new DeDupeLocalFileSystem(Path.GetFullPath(location), name));
                            SettingsFile.Write(data, "settings.xml");
                            break;
                        case "decryptdb":
                            fileName = input.Split(" ")[1].Trim();
                            //byte[] plain = AESWrapper.DecryptToByte(System.IO.File.ReadAllBytes(dbfile.Path), key);
                            System.IO.File.WriteAllBytes(fileName, System.IO.File.ReadAllBytes(dbfile.Path));
                            break;
                        case "help":
                            ShowHelp();
                            break;
                        case "changekey":
                            key = AESWrapper.GenerateKeyString(ConsoleEx.Password("Key: "), data.salt, data.iterations,
                                data.keySize);
                            fs.UpdateKey(key);
                            break;
                        case "generate":
                            data = GenerateSettings();
                            if (data != null)
                            {
                                SettingsFile.Write(data, "settings.xml");
                                key = AESWrapper.GenerateKeyString(ConsoleEx.Password("Key: "), data.salt,
                                    data.iterations,
                                    data.keySize);
                                fs.UpdateKey(key);
                            }

                            break;
                        case "showsettings":
                            Console.WriteLine($"Iterations = {data.iterations}");
                            Console.WriteLine($"Salt = {data.salt}");
                            Console.WriteLine($"Key size = {data.keySize}");
                            foreach (var kv in data.locations)
                            {
                                Console.WriteLine($"{kv.Key} = {kv.Value}");
                            }
                            break;
                        case "ls":
                            Console.WriteLine(VirtualDirectoryListing.List(fs.GetExtendedEntities(currentDirectory)));
                            break;
                        case "ll":
                            Console.WriteLine(
                                VirtualDirectoryListing.ListWithHash(fs.GetExtendedEntities(currentDirectory)));
                            break;
                        case "put":
                            if (data.locations.Count == 0)
                            {
                                Console.WriteLine("[Error]: No file locations setup");
                                break;
                            }
                            fileName = input.Split(" ")[1].Trim();
                            byte[] fileDataPut = System.IO.File.ReadAllBytes(fileName);
                            //string encFile = AESWrapper.EncryptToString(fileData, key);
                            Stopwatch watch1 = new Stopwatch(); 
                            watch1.Start();
                            using (Stream f = fs.CreateFile(FileSystemPath.Parse(currentDirectory.Path + fileName)))
                            {
                                f.Write(fileDataPut, 0, fileDataPut.Length);
                            }
                            fs.FlushTempFile();
                            dbfile.Flush();
                            watch1.Stop();
                            Console.WriteLine($"Elapsed time: {watch1.Elapsed.ToString("g")}");
                            break;
                        case "localcat":
                            if (data.locations.Count == 0)
                            {
                                Console.WriteLine("[Error]: No file locations setup");
                                break;
                            }
                            fileName = input.Split(" ")[1].Trim();
                            fileData = System.IO.File.ReadAllBytes(fileName);
                            Console.WriteLine(AESWrapper.DecryptToString(fileData, key));
                            break;
                        case "remotecat":
                            if (data.locations.Count == 0)
                            {
                                Console.WriteLine("[Error]: No file locations setup");
                                break;
                            }
                            fileName = input.Split(" ")[1].Trim();
                            try
                            {
                                using (Stream f = fs.OpenFile(FileSystemPath.Parse(currentDirectory.Path + fileName),
                                    FileAccess.Read))
                                {
                                    Console.WriteLine(f.ReadAllText());
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("[Error]: " + e.ToString());
                            }

                            break;
                        case "get":
                            if (data.locations.Count == 0)
                            {
                                Console.WriteLine("[Error]: No file locations setup");
                                break;
                            }
                            fileName = input.Split(" ")[1].Trim();
                            string dstFileName = input.Split(" ")[2].Trim();
                            Stopwatch watch2 = new Stopwatch();
                            watch2.Start();
                            using (Stream f = fs.OpenFile(FileSystemPath.Parse(currentDirectory.Path + fileName),
                                FileAccess.Read))
                            {
                                byte[] test = f.ReadAllBytes();
                                System.IO.File.WriteAllBytes(dstFileName, test);
                            }
                            watch2.Stop();
                            Console.WriteLine($"Elapsed time: {watch2.Elapsed.ToString("g")}");
                            break;
                        case "mkdir":
                            string newDir = input.Split(" ")[1].Trim();
                            fs.CreateDirectory(currentDirectory.AppendDirectory(newDir));
                            dbfile.Flush();
                            break;
                        case "cd":
                            string dirtmpStr;
                            //dirtmp = currentDirectory.AppendDirectory(input.Split(" ")[1]);
                            dirtmpStr = ParseDotDot(currentDirectory.Path, input.Split(" ")[1]);
                            FileSystemPath dirtmp;
                            if (dirtmpStr == "/")
                            {
                                dirtmp = FileSystemPath.Parse(dirtmpStr);
                            }
                            else
                            {
                                dirtmp = FileSystemPath.Parse(dirtmpStr + "/");
                            }

                            if (fs.Exists(dirtmp))
                            {
                                //currentDirectory = currentDirectory.AppendDirectory(input.Split(" ")[1]);
                                currentDirectory = dirtmp;
                            }
                            else
                            {
                                Console.WriteLine("No such directory exists");
                            }

                            break;
                    }
                } while (input != "exit" && input != "quit");
                dbfile.Flush();
            }
        }
    }
    class Program
    {

        static void Main(string[] args)
        {
            Prompt p = new Prompt();
            p.Run();
        }
    }
}