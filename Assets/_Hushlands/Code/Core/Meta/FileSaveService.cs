using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Hushlands.Core
{
    /// <summary>
    /// Versioned text save, engine-free. Line 1 "hushlands-save {version}", then "key\ttype\tvalue" lines, last line
    /// "crc {crc32}" over everything above. Flush writes a temp file, keeps the previous file as ".bak", then swaps.
    /// Load falls back to ".bak" on a bad checksum or header; a save from a newer version is never overwritten.
    /// Supported values: int, long, float, double, bool, string.
    /// </summary>
    public sealed class FileSaveService : ISaveService
    {
        public const int Version = 1;
        private const string Header = "hushlands-save";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly Dictionary<string, KeyValuePair<char, string>> _data = new Dictionary<string, KeyValuePair<char, string>>();
        private readonly string _path;

        public FileSaveService(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            if (TryRead(_path, out LoadResult main)) Apply(main);
            else if (File.Exists(_path) || File.Exists(BackupPath))
            {
                RecoveredFromBackup = TryRead(BackupPath, out LoadResult bak);
                if (RecoveredFromBackup) Apply(bak);
                CorruptionDetected = true;
            }
        }

        public string Path => _path;
        public string BackupPath => _path + ".bak";
        public bool CorruptionDetected { get; private set; }
        public bool RecoveredFromBackup { get; private set; }
        /// <summary>File came from a newer game version: kept read-only so it is never downgraded.</summary>
        public bool ReadOnlyNewerVersion { get; private set; }
        public bool Dirty { get; private set; }

        public void Save<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOf('\t') >= 0 || key.IndexOf('\n') >= 0)
                throw new ArgumentException("bad key", nameof(key));
            _data[key] = Encode(value);
            Dirty = true;
        }

        public bool TryLoad<T>(string key, out T value)
        {
            value = default(T);
            if (!_data.TryGetValue(key, out KeyValuePair<char, string> e)) return false;
            object o = Decode(e.Key, e.Value);
            if (o is T t)
            {
                value = t;
                return true;
            }
            return false;
        }

        public void Flush()
        {
            if (ReadOnlyNewerVersion) return;
            string dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, Serialize(), Encoding.UTF8);
            if (File.Exists(_path))
            {
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(_path, BackupPath);
            }
            File.Move(temp, _path);
            Dirty = false;
        }

        public string Serialize()
        {
            var body = new StringBuilder();
            body.Append(Header).Append(' ').Append(Version.ToString(Inv)).Append('\n');
            var keys = new List<string>(_data.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string k in keys)
            {
                KeyValuePair<char, string> e = _data[k];
                body.Append(k).Append('\t').Append(e.Key).Append('\t').Append(Escape(e.Value)).Append('\n');
            }
            string text = body.ToString();
            return text + "crc " + Crc32(text).ToString("x8", Inv) + "\n";
        }

        private struct LoadResult
        {
            public int Version;
            public Dictionary<string, KeyValuePair<char, string>> Data;
        }

        private void Apply(LoadResult r)
        {
            foreach (var kv in r.Data) _data[kv.Key] = kv.Value;
            ReadOnlyNewerVersion = r.Version > Version;
        }

        private static bool TryRead(string path, out LoadResult result)
        {
            result = new LoadResult { Data = new Dictionary<string, KeyValuePair<char, string>>() };
            if (!File.Exists(path)) return false;
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException)
            {
                return false;
            }
            int crcAt = text.LastIndexOf("crc ", StringComparison.Ordinal);
            if (crcAt <= 0) return false;
            string body = text.Substring(0, crcAt);
            string crcText = text.Substring(crcAt + 4).Trim();
            if (!uint.TryParse(crcText, NumberStyles.HexNumber, Inv, out uint crc) || crc != Crc32(body)) return false;
            string[] lines = body.Split('\n');
            string[] head = lines[0].Split(' ');
            if (head.Length != 2 || head[0] != Header || !int.TryParse(head[1], NumberStyles.Integer, Inv, out int version)) return false;
            result.Version = version;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                string[] p = lines[i].Split('\t');
                if (p.Length != 3 || p[1].Length != 1) return false;
                result.Data[p[0]] = new KeyValuePair<char, string>(p[1][0], Unescape(p[2]));
            }
            return true;
        }

        private static KeyValuePair<char, string> Encode<T>(T value)
        {
            switch (value)
            {
                case int i: return new KeyValuePair<char, string>('i', i.ToString(Inv));
                case long l: return new KeyValuePair<char, string>('l', l.ToString(Inv));
                case float f: return new KeyValuePair<char, string>('f', f.ToString("R", Inv));
                case double d: return new KeyValuePair<char, string>('d', d.ToString("R", Inv));
                case bool b: return new KeyValuePair<char, string>('b', b ? "1" : "0");
                case string s: return new KeyValuePair<char, string>('s', s);
                default: throw new NotSupportedException("save type " + typeof(T).Name);
            }
        }

        private static object Decode(char type, string v)
        {
            switch (type)
            {
                case 'i': return int.TryParse(v, NumberStyles.Integer, Inv, out int i) ? (object)i : null;
                case 'l': return long.TryParse(v, NumberStyles.Integer, Inv, out long l) ? (object)l : null;
                case 'f': return float.TryParse(v, NumberStyles.Float, Inv, out float f) ? (object)f : null;
                case 'd': return double.TryParse(v, NumberStyles.Float, Inv, out double d) ? (object)d : null;
                case 'b': return v == "1";
                case 's': return v;
                default: return null;
            }
        }

        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length)
                {
                    sb.Append(c);
                    continue;
                }
                char n = s[++i];
                sb.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n == 'r' ? '\r' : n);
            }
            return sb.ToString();
        }

        public static uint Crc32(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in bytes)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++) crc = (crc & 1u) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }
    }
}
