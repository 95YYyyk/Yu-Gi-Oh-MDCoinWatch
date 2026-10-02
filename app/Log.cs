using System.Text;

namespace MdCoinWatch;

/// <summary>写文件日志；如果是从命令行启动的，顺带也写一份到控制台。</summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static string _path = "";
    internal static bool Echo;

    internal static void Init(string path)
    {
        _path = path;
        Write("---- 启动 MdCoinWatch ----");
    }

    internal static void Write(string msg)
    {
        var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg;
        if (Echo) Console.WriteLine(line);
        lock (Gate)
        {
            try
            {
                var fi = new FileInfo(_path);
                if (fi.Exists && fi.Length > 1_000_000) File.Delete(_path);
                File.AppendAllText(_path, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
