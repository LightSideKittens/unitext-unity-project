using System.IO;
using System.Threading;

public static class SRFileUtil
{
#if !UNITY_WEBPLAYER && !NETFX_CORE

    public static void DeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
            Thread.Sleep(0);
            Directory.Delete(path, true);
        }
    }

#endif

        /// <param name="i"></param>
    /// <remarks>http://stackoverflow.com/a/281684/147003</remarks>
    /// <returns></returns>
    public static string GetBytesReadable(long i)
    {
        var sign = (i < 0 ? "-" : "");
        double readable = (i < 0 ? -i : i);
        string suffix;
        if (i >= 0x40000000) // Gigabyte
        {
            suffix = "GB";
            readable = i >> 20;
        }
        else if (i >= 0x100000) // Megabyte
        {
            suffix = "MB";
            readable = i >> 10;
        }
        else if (i >= 0x400) // Kilobyte
        {
            suffix = "KB";
            readable = i;
        }
        else
        {
            return i.ToString(sign + "0 B"); // Byte
        }
        readable /= 1024;

        return sign + readable.ToString("0.### ") + suffix;
    }
}
