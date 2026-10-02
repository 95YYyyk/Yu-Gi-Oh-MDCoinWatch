using System.Globalization;
using System.Text;

namespace MdCoinWatch;

internal sealed class Recorder
{
    private readonly string _path;

    internal Recorder(string path)
    {
        _path = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (!File.Exists(_path) || new FileInfo(_path).Length == 0)
        {
            File.WriteAllText(_path, "time,coin,turn_order,duel_seconds,note" + Environment.NewLine, new UTF8Encoding(true));
        }
        else
        {
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            fs.Seek(-1, SeekOrigin.End);
            if (fs.ReadByte() != '\n') { fs.Seek(0, SeekOrigin.End); fs.WriteByte((byte)'\n'); }
        }
    }

    internal string FilePath => _path;

    private static string Line(DateTime when, string coin, string turn, double? seconds, string note)
        => string.Join(',',
            when.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            coin, turn,
            seconds.HasValue ? seconds.Value.ToString("0", CultureInfo.InvariantCulture) : "",
            note);

    /// <summary>
    /// 追加一行，返回这一行在文件里的起始偏移。开局就先落一行，打完再按这个偏移改写。
    /// </summary>
    internal long Append(DateTime when, string coin, string turn, double? seconds, string note)
    {
        var bytes = Encoding.UTF8.GetBytes(Line(when, coin, turn, seconds, note) + Environment.NewLine);
        using var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
        long start = fs.Length;
        fs.Write(bytes, 0, bytes.Length);
        return start;
    }

    /// <summary>把之前写的那一行整行改写掉。长度会变，所以先截断再写。</summary>
    internal void Rewrite(long offset, DateTime when, string coin, string turn, double? seconds, string note)
    {
        if (offset < 0) return;
        var bytes = Encoding.UTF8.GetBytes(Line(when, coin, turn, seconds, note) + Environment.NewLine);
        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.Read);
        if (offset > fs.Length) return;
        fs.SetLength(offset);
        fs.Seek(offset, SeekOrigin.Begin);
        fs.Write(bytes, 0, bytes.Length);
    }
}
