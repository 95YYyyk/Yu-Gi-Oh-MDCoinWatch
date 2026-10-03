using System.Globalization;
using System.Text;

namespace MdCoinWatch;

internal sealed class Recorder
{
    private const string Header = "time,coin,turn_order,duel_seconds,result,note";

    private readonly object _gate = new();
    private readonly string _path;

    /// <summary>「清除全部记录」会把它 +1。之前落下的行带着旧 epoch，就不能再按旧偏移改写了。</summary>
    internal int Epoch { get; private set; }

    internal Recorder(string path)
    {
        _path = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (!File.Exists(_path) || new FileInfo(_path).Length == 0)
        {
            File.WriteAllText(_path, Header + Environment.NewLine, new UTF8Encoding(true));
        }
        else
        {
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            if (fs.Length > 0)
            {
                fs.Seek(-1, SeekOrigin.End);
                if (fs.ReadByte() != '\n') { fs.Seek(0, SeekOrigin.End); fs.WriteByte((byte)'\n'); }
            }
        }
    }

    internal string FilePath => _path;

    private static string Line(DateTime when, string coin, string turn, double? seconds, string result, string note)
        => string.Join(',',
            when.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            coin, turn,
            seconds.HasValue ? seconds.Value.ToString("0", CultureInfo.InvariantCulture) : "",
            result, note);

    /// <summary>追加一行，返回这一行在文件里的起始偏移。开局先落一行，打完按这个偏移改写。</summary>
    internal long Append(DateTime when, string coin, string turn, double? seconds, string result, string note)
    {
        lock (_gate)
        {
            var bytes = Encoding.UTF8.GetBytes(Line(when, coin, turn, seconds, result, note) + Environment.NewLine);
            using var fs = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            long start = fs.Length;
            fs.Write(bytes, 0, bytes.Length);
            return start;
        }
    }

    /// <summary>整行改写。长度会变，所以先截断再写。epoch 对不上（中途清过记录）就放弃并返回 false。</summary>
    internal bool Rewrite(long offset, int epoch, DateTime when, string coin, string turn,
                          double? seconds, string result, string note)
    {
        lock (_gate)
        {
            if (offset < 0 || epoch != Epoch) return false;
            var bytes = Encoding.UTF8.GetBytes(Line(when, coin, turn, seconds, result, note) + Environment.NewLine);
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.Read);
            if (offset > fs.Length) return false;
            fs.SetLength(offset);
            fs.Seek(offset, SeekOrigin.Begin);
            fs.Write(bytes, 0, bytes.Length);
            return true;
        }
    }

    /// <summary>清空全部记录，只留表头。</summary>
    internal void Reset()
    {
        lock (_gate)
        {
            Epoch++;
            File.WriteAllText(_path, Header + Environment.NewLine, new UTF8Encoding(true));
        }
    }
}

