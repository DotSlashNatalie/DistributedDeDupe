using System;

public static class DateTimeExtensions
{
    // Src: https://stackoverflow.com/questions/249760/how-can-i-convert-a-unix-timestamp-to-datetime-and-vice-versa
    public static DateTime UnixTimeStampToDateTime(this double unixTimeStamp )
    {
        // Unix timestamp is seconds past epoch
        System.DateTime dtDateTime = new DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc);
        dtDateTime = dtDateTime.AddSeconds( unixTimeStamp ).ToLocalTime();
        return dtDateTime;
    }

    public static double UnixTimeStamp(this DateTime dt)
    {
        return (dt.ToUniversalTime().Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
    }
}