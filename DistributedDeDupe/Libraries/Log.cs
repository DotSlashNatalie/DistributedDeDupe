using System;
using System.Collections.Generic;
using System.IO;

public class Log
{
    private static readonly object padlock = new object();
    private static Log instance;
    private List<string> log = new List<string>();
    protected Log() {}

    public void Add(string line)
    {
        string caller = (new System.Diagnostics.StackTrace()).GetFrame(1).GetMethod().Name;
        log.Add("[" + DateTime.Now.ToString("M/d/yyyy H:mm:ss") + "]" + line);
    }

    public void Out()
    {
        foreach (string line in log)
        {
            Console.WriteLine(line);
        }
    }

    public void Write(string file)
    {
        string output = String.Join("\n", log.ToArray()) + "\n";
        File.WriteAllText(file, output);
    }

    public static Log Instance
    {
        get
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    instance = new Log();
                }

                return instance;
            }
        }
    }
}