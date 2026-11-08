using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GoHMM
{
    public class SharePayload
    {
        public string Name = "";
        public List<string> Ids = new List<string>();
        public Dictionary<string, string> Titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    // 分享码：JSON -> zlib -> Base64（和旧版 goh-mod-manager 同样的形状，eJz... 开头）
    public static class ShareCode
    {
        public static string Encode(string name, IList<string> setIds)
        {
            return Encode(name, setIds, null);
        }

        public static string Encode(string name, IList<string> setIds, IDictionary<string, string> titles)
        {
            var sb = new StringBuilder();
            sb.Append("{\"v\":1,\"app\":400750,\"name\":\"").Append(Json.Esc(name)).Append("\",\"mods\":[");
            for (int i = 0; i < setIds.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Json.Esc(setIds[i])).Append('"');
            }
            sb.Append(']');
            if (titles != null && titles.Count > 0)
            {
                sb.Append(",\"titles\":{");
                int n = 0;
                foreach (var kv in titles)
                {
                    if (string.IsNullOrEmpty(kv.Value)) continue;
                    if (n++ > 0) sb.Append(',');
                    sb.Append('"').Append(Json.Esc(kv.Key)).Append("\":\"").Append(Json.Esc(Fmt.Short(kv.Value, 90))).Append('"');
                }
                sb.Append('}');
            }
            sb.Append("}");
            byte[] raw = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] comp = ZlibCompress(raw);
            return Convert.ToBase64String(comp);
        }

        public static SharePayload Decode(string code, out string error)
        {
            error = null;
            try
            {
                string text = (code ?? "").Trim();
                if (text.Length == 0) { error = L.T("分享码是空的。"); return null; }

                // 容忍带前缀 / 带空格 / 带换行的写法
                int colon = text.IndexOf(':');
                if (colon > 0 && colon < 8) text = text.Substring(colon + 1);
                text = text.Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "");
                if (text.Length == 0) { error = L.T("分享码是空的。"); return null; }

                byte[] bytes;
                try { bytes = Convert.FromBase64String(text); }
                catch { error = L.T("这不是有效的分享码（Base64 解析失败）。"); return null; }

                string json = null;
                // 1) zlib
                try
                {
                    string t = Encoding.UTF8.GetString(ZlibDecompress(bytes));
                    if (LooksJson(t)) json = t;
                }
                catch { }
                // 2) 裸 deflate
                if (json == null)
                {
                    try
                    {
                        string t = Encoding.UTF8.GetString(DeflateDecompress(bytes, 0, bytes.Length));
                        if (LooksJson(t)) json = t;
                    }
                    catch { }
                }
                // 3) 其实就是明文 JSON
                if (json == null)
                {
                    string t = Encoding.UTF8.GetString(bytes);
                    if (LooksJson(t)) json = t;
                }
                if (json == null) { error = L.T("分享码解压失败，可能不是这个游戏的分享码。"); return null; }

                var root = Json.Parse(json) as Dictionary<string, object>;
                if (root == null) { error = L.T("分享码内容格式不对。"); return null; }

                var p = new SharePayload();
                object nameObj;
                if (root.TryGetValue("name", out nameObj) && nameObj is string) p.Name = (string)nameObj;

                object titlesObj;
                if (root.TryGetValue("titles", out titlesObj) && titlesObj is Dictionary<string, object>)
                {
                    foreach (var kv in (Dictionary<string, object>)titlesObj)
                    {
                        string t = kv.Value as string;
                        if (string.IsNullOrEmpty(t)) continue;
                        string key = kv.Key.StartsWith("mod_", StringComparison.OrdinalIgnoreCase) ? kv.Key : "mod_" + kv.Key;
                        p.Titles[key] = t;
                    }
                }

                object modsObj = null;
                if (!root.TryGetValue("mods", out modsObj)) root.TryGetValue("modList", out modsObj);

                var arr = modsObj as List<object>;
                if (arr == null)
                {
                    // 也许是 {"presets":{"1":[...]}} 这种结构，取第一个数组
                    object presetsObj;
                    if (root.TryGetValue("presets", out presetsObj) && presetsObj is Dictionary<string, object>)
                    {
                        foreach (var kv in (Dictionary<string, object>)presetsObj)
                        {
                            arr = kv.Value as List<object>;
                            if (arr != null) break;
                        }
                    }
                }
                if (arr == null) arr = FindIdArray(root);
                if (arr == null) { error = L.T("分享码里没有找到模组列表。"); return null; }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var o in arr)
                {
                    string s = o as string;
                    if (s == null && o is double) s = ((double)o).ToString("0");
                    if (string.IsNullOrEmpty(s)) continue;
                    s = s.Trim();
                    if (!s.StartsWith("mod_", StringComparison.OrdinalIgnoreCase)) s = "mod_" + s;
                    if (seen.Add(s)) p.Ids.Add(s);
                }
                if (p.Ids.Count == 0) { error = L.T("分享码里没有模组。"); return null; }
                return p;
            }
            catch (Exception ex)
            {
                error = L.T("分享码解析失败：") + ex.Message;
                return null;
            }
        }

        // 兜底：在任意层级的数组里找一串像模组 ID 的值
        static List<object> FindIdArray(Dictionary<string, object> root)
        {
            List<object> best = null;
            foreach (var kv in root)
            {
                var arr = kv.Value as List<object>;
                if (arr != null && ScoreIds(arr) > 0 && (best == null || arr.Count > best.Count)) best = arr;
                var sub = kv.Value as Dictionary<string, object>;
                if (sub == null) continue;
                foreach (var kv2 in sub)
                {
                    var arr2 = kv2.Value as List<object>;
                    if (arr2 != null && ScoreIds(arr2) > 0 && (best == null || arr2.Count > best.Count)) best = arr2;
                }
            }
            return best;
        }

        static int ScoreIds(List<object> arr)
        {
            int n = 0;
            foreach (var o in arr)
            {
                string v = o as string;
                if (v == null) continue;
                v = v.Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(v, "^(mod_)?[0-9]{6,}$")) n++;
            }
            return n;
        }

        static bool LooksJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.TrimStart();
            return t.Length > 1 && (t[0] == '{' || t[0] == '[');
        }

        // ---------------- zlib ----------------
        static byte[] ZlibCompress(byte[] raw)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var ds = new DeflateStream(ms, CompressionMode.Compress, true))
                    ds.Write(raw, 0, raw.Length);
                uint ad = Adler32(raw);
                ms.WriteByte((byte)(ad >> 24));
                ms.WriteByte((byte)(ad >> 16));
                ms.WriteByte((byte)(ad >> 8));
                ms.WriteByte((byte)ad);
                return ms.ToArray();
            }
        }

        static byte[] ZlibDecompress(byte[] data)
        {
            if (data.Length < 2) throw new InvalidDataException("too short");
            int cmf = data[0], flg = data[1];
            if ((cmf & 0x0F) != 8) throw new InvalidDataException("not zlib");
            if (((cmf << 8) + flg) % 31 != 0) throw new InvalidDataException("bad zlib header");
            bool dict = (flg & 0x20) != 0;
            int offset = 2 + (dict ? 4 : 0);
            int len = data.Length - offset - 4;   // 去掉尾部 adler32
            if (len < 0) throw new InvalidDataException("bad zlib length");
            return DeflateDecompress(data, offset, len);
        }

        static byte[] DeflateDecompress(byte[] data, int offset, int len)
        {
            using (var ms = new MemoryStream(data, offset, len))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                var buf = new byte[8192];
                int n;
                while ((n = ds.Read(buf, 0, buf.Length)) > 0) outMs.Write(buf, 0, n);
                return outMs.ToArray();
            }
        }

        static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte x in data)
            {
                a = (a + x) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }
}
