using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using SharpFileSystem;
using System.Text;
using System.Data.SQLite;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Apis.Download;
using Google.Apis.Http;
using Google.Apis.Upload;
using SharpFileSystem.IO;
using Directory = System.IO.Directory;
using File = Google.Apis.Drive.v3.Data.File;

namespace DistributedDeDupe
{

    public class GDriveFileStream : MemoryStream
    {
        protected DriveService svc;
        protected string fileName;
        protected string mimeType;
        protected SQLiteDatabase db;
        protected double sizeOfUplaod;
        protected Task<IDownloadProgress> progress;
        public GDriveFileStream(string fileName, string mimeType, DriveService svc, SQLiteDatabase db)
        {
            this.fileName = fileName;
            this.svc = svc;
            this.mimeType = mimeType;
            this.db = db;
            
            //string fileId =
            //    db.ExecuteScalar($"SELECT fileid FROM gdrive WHERE filename = '{fileName}' AND directory = 0");
            string fileId =
                db.ExecuteScalar("SELECT fileid FROM gdrive WHERE filename = @fileName AND directory = @directory",
                    new Dictionary<string, object>()
                    {
                        {"@fileName", fileName},
                        {"@directory", 0}
                    });
            if (fileId != "")
            {
                //var req2 = svc.Files.List();
                //string fnameURLEncode = System.Web.HttpUtility.UrlEncode(fileName);
                //req2.Q = $"id = '{fileId}'";
                //req2.Fields = "nextPageToken, files(id, name,parents,mimeType, trashed)";
                //var feed = req2.Execute();
                var request = svc.Files.Get(fileId);
                //request.Fields = "*";
                //string fid = request.FileId;
                //var t = request.RequestParameters;
                var z = request.DownloadWithStatus(this);
                int totalTries = 10;
                int tries = 0;
                if (Length == 0)
                {
                    while (Length == 0 && tries <= totalTries)
                    {
                        z = request.DownloadWithStatus(this);
                        tries++;
                    }

                    if (Length == 0)
                    {
                        throw new Exception(z.ToString());
                    }
                }

                this.Position = 0;
                //this.Position = 0;
                //var fileResp = request.Execute();
                //this.Position = 0;



            }
        }

        public bool IsDownloaded()
        {
            return progress.Result.Status == DownloadStatus.Completed;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            /*if (progress != null)
            {
                while (progress.Result.Status != DownloadStatus.Completed)
                {
                    /*if (progress.Result.Status == DownloadStatus.Failed)
                        throw new Exception("Failed downloading file from gdrive");

                    if (progress.Result.Status == DownloadStatus.NotStarted)
                        throw new Exception("Download not started?");
                }
                this.Position = 0;
            }*/
            return base.Read(buffer, offset, count);
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            // Someday I will separate it out and make a PR to SharpFileSystem
            //string directoryID =
            //    db.ExecuteScalar("SELECT fileid FROM gdrive WHERE filename = 'distrodedup' AND directory = 1");
            string directoryID =
                db.ExecuteScalar("SELECT fileid FROM gdrive WHERE filename = @fileName AND directory = @directory",
                    new Dictionary<string, object>()
                    {
                        {"@fileName", "distrodedup"},
                        {"@directory", 1}
                    });
            if (directoryID == "")
            {
                File fileData = new File();
                fileData.Name = "DistroDeDup";
                fileData.MimeType = "application/vnd.google-apps.folder";
                var req = svc.Files.Create(fileData);
                req.Fields = "id";
                var folder = req.Execute();
                //db.ExecuteNonQuery(
                //    $"INSERT INTO gdrive (filename, fileid, directory) VALUES ('distrodedup', '{folder.Id}', 1)");
                db.ExecuteNonQuery(
                    "INSERT INTO gdrive (filename, fileid, directory) VALUES (@fileName, @fileID, @directory)",
                    new Dictionary<string, object>()
                    {
                        {"@fileName", "distrodedup"},
                        {"@fileID", folder.Id},
                        {"@directory", 1}
                    });
                
                directoryID = folder.Id;
            }

            //string fileId =
            //    db.ExecuteScalar($"SELECT fileid FROM gdrive WHERE filename = '{fileName}' AND directory = 0");
            string fileId =
                db.ExecuteScalar("SELECT fileid FROM gdrive WHERE filename = @fileName AND directory = @directory",
                    new Dictionary<string, object>()
                    {
                        {"@fileName", fileName},
                        {"@directory", 0}
                    });
            
            if (fileId != "")
            {
                FilesResource.ListRequest reqExists = svc.Files.List();
                reqExists.Q = String.Format("name = '{0}' and trashed=false and parents in '{1}'", fileName, directoryID);
                reqExists.Fields = "nextPageToken, files(id, name,parents,mimeType, trashed)";
                var result = reqExists.Execute();
                bool foundFile = false;
                foreach (var file in result.Files)
                {
                    if (file.Id == fileId)
                    {
                        // I guess we need to create another file object?
                        /*File uploadFile = new File();
                        uploadFile.Name = fileName;
                        uploadFile.MimeType = mimeType;
                        var res = svc.Files.Update(uploadFile, fileId, this, mimeType);
                        var req = res.Upload();*/
                        GDriveFile f = new GDriveFile(this, fileName, mimeType, directoryID, db, GDriveFile.Operation.NONE);
                        f.Update(svc, fileId);
                        foundFile = true;
                        break;
                    }
                    
                }

                if (!foundFile)
                {
                    GDriveFile f = new GDriveFile(this, fileName, mimeType, directoryID, db, GDriveFile.Operation.UPDATE);
                    f.Upload(svc);
                    /*File body = new File();
                    body.Name = fileName;
                    body.MimeType = mimeType;
                    body.Parents = new List<string>();
                    body.Parents.Add(directoryID);
                    FilesResource.CreateMediaUpload req = svc.Files.Create(body, this, mimeType);
                    req.Fields = "id, parents";
                    var res = req.Upload();
                    string newFileId = req.ResponseBody.Id;
                        
                    //db.ExecuteNonQuery(
                    //    $"UPDATE gdrive set fileid = '{newFileId}' WHERE filename = '{fileName}' and directory = 0");
                    
                    db.ExecuteNonQuery(
                        "UPDATE gdrive set fileid = @newFileId WHERE filename = @fileName and directory = 0",
                        new Dictionary<string, object>()
                        {
                            {"@newFileId", newFileId},
                            {"@fileName", fileName}
                        });*/
                }
                
                
            }
            else
            {
                GDriveFile f = new GDriveFile(this, fileName, mimeType, directoryID, db, GDriveFile.Operation.CREATE);
                f.Upload(svc);
                /*File body = new File();
                body.Name = fileName;
                body.MimeType = mimeType;
                body.Parents = new List<string>();
                body.Parents.Add(directoryID);
                FilesResource.CreateMediaUpload req = svc.Files.Create(body, this, mimeType);
                req.Fields = "id, parents";
                req.ProgressChanged += ReqOnProgressChanged;
                //req.UploadAsync()
                var res = req.Upload();
                
                string newFileId = req.ResponseBody.Id;

                //db.ExecuteNonQuery(
                //    $"INSERT INTO gdrive (filename, fileid, directory) VALUES ('{fileName}', '{newFileId}', 0)");
                db.ExecuteNonQuery(
                    "INSERT INTO gdrive (filename, fileid, directory) VALUES (@fileName, @newFileId, 0)",
                    new Dictionary<string, object>()
                    {
                        {"@fileName", fileName},
                        {"@newFileID", newFileId}
                    });*/
            }
        }


        /*public override int Read(byte[] buffer, int offset, int count)
        {
            int x = _stream.Read(_stream.GetBuffer(), (int)_stream.Position, _stream.);
            return x;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }*/

        /*public override void Write(byte[] buffer, int offset, int count)
        {
            
        }*/

        /*public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;
        public override long Length => _stream.Length;
        public override long Position
        {
            get { return _stream.Position;}
            set { _stream.Position = value; }
        }*/
    }

    
    public class GDriveFileSystem : IFileSystem
    {
        protected UserCredential credential;
        protected DriveService service;
        protected SQLiteDatabase db;
        
        public GDriveFileSystem(string[] scopes, string ApplicationName, string dbFile, string credentialsFile = "credentials.json", string userCredentials = "token.json")
        {
            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }
            
            service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
                
            });
            
            db = new SQLiteDatabase(dbFile);
            
        }

        
        
        public void Dispose()
        {
            
        }

        public ICollection<FileSystemPath> GetEntities(FileSystemPath path)
        {
            throw new System.NotImplementedException();
        }

        public bool Exists(FileSystemPath path)
        {
            throw new System.NotImplementedException();
        }

        public Stream CreateFile(FileSystemPath path)
        {
            return CreateFile(path, "application/octet-stream");
        }
        
        public virtual Stream CreateFile(FileSystemPath path, string mime)
        {
            return new GDriveFileStream(path.Path.TrimStart('/'), mime, service, db);
        }

        public void UploadFile(Stream file)
        {
            
        }

        public Stream OpenFile(FileSystemPath path, FileAccess access)
        {
            return OpenFile(path, "application/octet-stream");
        }

        public Stream OpenFile(FileSystemPath path, string mime)
        {
            GDriveFileStream fs = new GDriveFileStream(path.Path.TrimStart('/'), mime, service, db);
            return fs;
        }

        public void CreateDirectory(FileSystemPath path)
        {
            throw new System.NotImplementedException();
        }

        public void Delete(FileSystemPath path)
        {
            throw new System.NotImplementedException();
        }

        public override string ToString()
        {
            return "GDRIVE";
        }
    }
}