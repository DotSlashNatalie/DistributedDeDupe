using System;
using System.Collections.Generic;
using System.Security;

public static class ConsoleEx
{
    // 
    // Src: https://stackoverflow.com/a/3404464/195722
    // Originally had the below link as a source for converting securestring to a string
    // However, this is insecure and defeats the point
    // Leaving as a pointer in case anyone else comes across that need
    // Src: https://stackoverflow.com/a/25751722/195722
    public static SecureString SecurePassword(string prompt = "")
    {
            Console.Write(prompt);
            var pwd = new SecureString();
            while (true)
            {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter)
                {
                    break;
                }
                else if (i.Key == ConsoleKey.Backspace)
                {
                    if (pwd.Length > 0)
                    {
                        pwd.RemoveAt(pwd.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else if (i.KeyChar != '\u0000' ) // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                {
                    pwd.AppendChar(i.KeyChar);
                    Console.Write("*");
                }
            }

            return pwd;

    }
    
    public static string Password(string prompt = "")
    {
        Console.Write(prompt);
        List<char> pwd = new List<char>();
        while (true)
        {
            ConsoleKeyInfo i = Console.ReadKey(true);
            if (i.Key == ConsoleKey.Enter)
            {
                break;
            }
            else if (i.Key == ConsoleKey.Backspace)
            {
                if (pwd.Count > 0)
                {
                    pwd.RemoveAt(pwd.Count - 1);
                    Console.Write("\b \b");
                }
            }
            else if (i.KeyChar != '\u0000' ) // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
            {
                pwd.Add(i.KeyChar);
                Console.Write("*");
            }
        }
        Console.WriteLine(); // emulate a return
        return new string(pwd.ToArray());

    }
}