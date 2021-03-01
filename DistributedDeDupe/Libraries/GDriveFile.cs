using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using SharpFileSystem.IO;
using File = Google.Apis.Drive.v3.Data.File;

public class GDriveFile
{
    protected MemoryStream _stream = new MemoryStream();
    protected string fileName;
    protected string mimeType;
    protected string directoryID;
    protected SQLiteDatabase db;

    public enum Operation
    {
        CREATE,
        UPDATE,
        NONE
    };

    protected Operation op;
    public GDriveFile(Stream s, string filename, string mimetype, string directoryID, SQLiteDatabase db, Operation p)
    {
        // Since this is async it is possible that the underlying stream is disposed before we get a chance to
        // upload to gdrive
        // So lets copy to a memorystream and not worry about it
        s.Position = 0;
        byte[] tmp = s.ReadAllBytes();
        _stream.Write(tmp);
        this.fileName = filename;
        this.mimeType = mimetype;
        this.directoryID = directoryID;
        this.db = db;
        this.op = p;

    }

    public async void Upload(DriveService svc)
    {
        Google.Apis.Drive.v3.Data.File body = new File();
        body.Name = fileName;
        body.MimeType = mimeType;
        body.Parents = new List<string>();
        body.Parents.Add(directoryID);
        FilesResource.CreateMediaUpload req = svc.Files.Create(body, _stream, mimeType);
        req.Fields = "id, parents";
        req.ResponseReceived += ReqOnResponseReceived;

        var result = await req.UploadAsync();


    }

    public async void Update(DriveService svc, string fileId)
    {
        File uploadFile = new File();
        uploadFile.Name = fileName;
        uploadFile.MimeType = mimeType;
        var res = svc.Files.Update(uploadFile, fileId, _stream, mimeType);
        res.ResponseReceived += ReqOnResponseReceived;
        await res.UploadAsync();
        
    }

    private void ReqOnResponseReceived(File obj)
    {

        switch (op)
        {
            case Operation.CREATE:
                db.ExecuteNonQuery(
                "INSERT INTO gdrive (filename, fileid, directory) VALUES (@fileName, @newFileId, 0)",
                new Dictionary<string, object>()
                {
                    {"@fileName", fileName},
                    {"@newFileID", obj.Id}
                });
                break;
            case Operation.UPDATE:
                db.ExecuteNonQuery(
                    "UPDATE gdrive set fileid = @newFileId WHERE filename = @fileName and directory = 0",
                    new Dictionary<string, object>()
                    {
                        {"@newFileId", obj.Id},
                        {"@fileName", fileName}
                    });
                break;
            case Operation.NONE:
            default:
                break;
        }

    }
}