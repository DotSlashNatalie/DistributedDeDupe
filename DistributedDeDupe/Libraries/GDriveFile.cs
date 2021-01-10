using System;
using System.Collections.Generic;
using System.IO;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using File = Google.Apis.Drive.v3.Data.File;

public class GDriveFile
{
    protected Stream s;
    protected bool showProgress;
    protected string fileName;
    protected string mimeType;
    protected string directoryID;
    protected ProgressBar pb;
    protected SQLiteDatabase db;

    public enum Operation
    {
        CREATE,
        UPDATE,
        NONE
    };

    protected Operation op;
    public GDriveFile(Stream s, string filename, string mimetype, string directoryID, SQLiteDatabase db, Operation p, bool showProgress = true)
    {
        this.s = s;
        this.showProgress = showProgress;
        this.fileName = filename;
        this.mimeType = mimetype;
        this.directoryID = directoryID;
        this.db = db;
        this.op = op;

    }

    public void Upload(DriveService svc)
    {
        Google.Apis.Drive.v3.Data.File body = new File();
        body.Name = fileName;
        body.MimeType = mimeType;
        body.Parents = new List<string>();
        body.Parents.Add(directoryID);
        FilesResource.CreateMediaUpload req = svc.Files.Create(body, s, mimeType);
        req.Fields = "id, parents";
        req.ResponseReceived += ReqOnResponseReceived;
        if (showProgress)
        {
            pb = new ProgressBar();
            req.ProgressChanged += ReqOnProgressChanged;
        }

        req.Upload();

    }

    public void Update(DriveService svc, string fileId)
    {
        File uploadFile = new File();
        uploadFile.Name = fileName;
        uploadFile.MimeType = mimeType;
        var res = svc.Files.Update(uploadFile, fileId, s, mimeType);
        res.ResponseReceived += ReqOnResponseReceived;
        if (showProgress)
        {
            pb = new ProgressBar();
            res.ProgressChanged += ReqOnProgressChanged;
        }
        var req = res.Upload();
        
    }

    private void ReqOnResponseReceived(File obj)
    {
        pb?.Dispose();

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

    private void ReqOnProgressChanged(IUploadProgress obj)
    {
        pb.Report((((double)obj.BytesSent)/s.Length));
    }
}