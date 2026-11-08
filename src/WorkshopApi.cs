using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GoHMM
{
    public class WorkshopInfo
    {
        public string Id = "";
        public string Title = "";
        public string Description = "";
        public string CreatorId = "";
        public string CreatorName = "";
        public string PreviewUrl = "";
        public long Subscriptions;
        public int TimeCreated;
        public int TimeUpdated;
        public bool Online;   // 是否来自联网结果

        public DateTime UpdatedLocal
        {
            get
            {
                if (TimeUpdated <= 0) return DateTime.MinValue;
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(TimeUpdated).ToLocalTime();
            }
        }
    }

    // 通过 Steam 公开接口补全作者 / 订阅数 / 更新时间，结果缓存到本地
    public static class WorkshopApi
    {
        static Dictionary<string, WorkshopInfo> cache;
        static Dictionary<string, string> authorNames;
        static readonly object pad = new object();

        public static string CacheDir
        {
            get
            {
                string p = Path.Combine(AppConfig.Dir, "cache");
                try { Directory.CreateDirectory(p); } catch { }
                return p;
            }
        }

        static string InfoFile { get { return Path.Combine(CacheDir, "workshop.json"); } }
        static string AuthorFile { get { return Path.Combine(CacheDir, "authors.json"); } }

        static void EnsureLoaded()
        {
            if (cache != null) return;
            lock (pad)
            {
                if (cache != null) return;
                cache = new Dictionary<string, WorkshopInfo>(StringComparer.OrdinalIgnoreCase);
                authorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (File.Exists(InfoFile))
                    {
                        var d = Json.Parse(File.ReadAllText(InfoFile, Encoding.UTF8)) as Dictionary<string, object>;
                        if (d != null)
                        {
                            foreach (var kv in d)
                            {
                                var o = kv.Value as Dictionary<string, object>;
                                if (o == null) continue;
                                var w = new WorkshopInfo();
                                w.Id = kv.Key;
                                w.Title = Str(o, "title");
                                w.Description = Str(o, "desc");
                                w.CreatorId = Str(o, "creator");
                                w.CreatorName = Str(o, "creator_name");
                                w.PreviewUrl = Str(o, "preview");
                                w.Subscriptions = (long)Num(o, "subs");
                                w.TimeCreated = (int)Num(o, "created");
                                w.TimeUpdated = (int)Num(o, "updated");
                                cache[kv.Key] = w;
                            }
                        }
                    }
                }
                catch { }
                try
                {
                    if (File.Exists(AuthorFile))
                    {
                        var d = Json.Parse(File.ReadAllText(AuthorFile, Encoding.UTF8)) as Dictionary<string, object>;
                        if (d != null)
                            foreach (var kv in d)
                                if (kv.Value is string) authorNames[kv.Key] = (string)kv.Value;
                    }
                }
                catch { }
            }
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object o;
            return d.TryGetValue(k, out o) && o is string ? (string)o : "";
        }

        static double Num(Dictionary<string, object> d, string k)
        {
            object o;
            return d.TryGetValue(k, out o) && o is double ? (double)o : 0;
        }

        public static WorkshopInfo Cached(string id)
        {
            EnsureLoaded();
            WorkshopInfo w;
            lock (pad)
                if (cache.TryGetValue(id, out w)) return w;
            return null;
        }

        public static string CachedAuthor(string creatorId)
        {
            if (string.IsNullOrEmpty(creatorId)) return "";
            EnsureLoaded();
            string n;
            lock (pad)
                if (authorNames.TryGetValue(creatorId, out n)) return n;
            return "";
        }

        public static void Remember(WorkshopInfo w)
        {
            EnsureLoaded();
            lock (pad) cache[w.Id] = w;
            Save();
        }

        public static void RememberAuthor(string creatorId, string name)
        {
            if (string.IsNullOrEmpty(creatorId) || string.IsNullOrEmpty(name)) return;
            EnsureLoaded();
            lock (pad) authorNames[creatorId] = name;
            Save();
        }

        static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                int i = 0;
                List<WorkshopInfo> all;
                lock (pad) all = new List<WorkshopInfo>(cache.Values);
                foreach (var w in all)
                {
                    if (++i > 1) sb.Append(",\n");
                    sb.Append("  \"").Append(Json.Esc(w.Id)).Append("\": {");
                    sb.Append("\"title\": \"").Append(Json.Esc(w.Title)).Append("\", ");
                    sb.Append("\"desc\": \"").Append(Json.Esc(Fmt.Short(w.Description, 400))).Append("\", ");
                    sb.Append("\"creator\": \"").Append(Json.Esc(w.CreatorId)).Append("\", ");
                    sb.Append("\"creator_name\": \"").Append(Json.Esc(w.CreatorName)).Append("\", ");
                    sb.Append("\"preview\": \"").Append(Json.Esc(w.PreviewUrl)).Append("\", ");
                    sb.Append("\"subs\": ").Append(w.Subscriptions).Append(", ");
                    sb.Append("\"created\": ").Append(w.TimeCreated).Append(", ");
                    sb.Append("\"updated\": ").Append(w.TimeUpdated);
                    sb.Append("}");
                }
                sb.Append("\n}\n");
                File.WriteAllText(InfoFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { AppConfig.Log("写缓存失败: " + ex.Message); }

            try
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                int i = 0;
                List<KeyValuePair<string, string>> all;
                lock (pad) all = new List<KeyValuePair<string, string>>(authorNames);
                foreach (var kv in all)
                {
                    if (++i > 1) sb.Append(",\n");
                    sb.Append("  \"").Append(Json.Esc(kv.Key)).Append("\": \"").Append(Json.Esc(kv.Value)).Append("\"");
                }
                sb.Append("\n}\n");
                File.WriteAllText(AuthorFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { AppConfig.Log("写作者缓存失败: " + ex.Message); }
        }

        // 拉取创意工坊条目详情（POST 表单，无需 API Key）
        public static WorkshopInfo Fetch(string id)
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // Tls12
                string body = "itemcount=1&publishedfileids[0]=" + Uri.EscapeDataString(id);
                string json = HttpPost("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/?format=json", body, 9000);
                var root = Json.Parse(json) as Dictionary<string, object>;
                if (root == null) return null;
                object respObj;
                if (!root.TryGetValue("response", out respObj)) return null;
                var resp = respObj as Dictionary<string, object>;
                if (resp == null) return null;
                object arrObj;
                if (!resp.TryGetValue("publishedfiledetails", out arrObj)) return null;
                var arr = arrObj as List<object>;
                if (arr == null || arr.Count == 0) return null;
                var d = arr[0] as Dictionary<string, object>;
                if (d == null) return null;

                double result = Num(d, "result");
                if (result != 1) return null;

                var w = new WorkshopInfo();
                w.Id = id;
                w.Title = Str(d, "title");
                w.Description = CleanWorkshopText(Str(d, "description"));
                w.CreatorId = Str(d, "creator");
                w.PreviewUrl = Str(d, "preview_url");
                w.Subscriptions = (long)Num(d, "subscriptions");
                w.TimeCreated = (int)Num(d, "time_created");
                w.TimeUpdated = (int)Num(d, "time_updated");
                w.Online = true;

                string name = CachedAuthor(w.CreatorId);
                if (string.IsNullOrEmpty(name)) name = FetchAuthorName(w.CreatorId);
                w.CreatorName = name ?? "";

                Remember(w);
                return w;
            }
            catch (Exception ex)
            {
                AppConfig.Log("获取工坊信息失败 " + id + ": " + ex.Message);
                return null;
            }
        }

        public static string FetchAuthorName(string creatorId)
        {
            if (string.IsNullOrEmpty(creatorId)) return "";
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                string xml = HttpGet("https://steamcommunity.com/profiles/" + creatorId + "/?xml=1", 9000);
                if (string.IsNullOrEmpty(xml)) return "";
                var m = Regex.Match(xml, "<steamID><!\\[CDATA\\[(.*?)\\]\\]></steamID>", RegexOptions.Singleline);
                if (!m.Success) m = Regex.Match(xml, "<steamID>(.*?)</steamID>", RegexOptions.Singleline);
                if (!m.Success) return "";
                string name = m.Groups[1].Value.Trim();
                RememberAuthor(creatorId, name);
                return name;
            }
            catch (Exception ex)
            {
                AppConfig.Log("获取作者名失败 " + creatorId + ": " + ex.Message);
                return "";
            }
        }

        static string HttpPost(string url, string body, int timeout)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.UserAgent = "GoHModManagerPlus/1.0";
            req.Timeout = timeout;
            req.ReadWriteTimeout = timeout;
            byte[] data = Encoding.UTF8.GetBytes(body);
            req.ContentLength = data.Length;
            using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        static string HttpGet(string url, int timeout)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) GoHModManagerPlus/1.0";
            req.Timeout = timeout;
            req.ReadWriteTimeout = timeout;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        // 把工坊简介里的 BBCode/HTML 清成纯文本
        public static string CleanWorkshopText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, "\\[/?[^\\]]{1,20}\\]", "");       // [b] [url=..] 之类
            s = Regex.Replace(s, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "<[^>]+>", "");
            s = s.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
            s = Regex.Replace(s, "[ \\t]+\\n", "\n");
            s = Regex.Replace(s, "\\n{3,}", "\n\n");
            return s.Trim();
        }

        // 从简介里猜作者（离线兜底）
        public static string GuessAuthor(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return "";
            string[] patterns = new string[]
            {
                @"(?:Developed|Created|Made|Modded|Maintained)\s+by\s+([^\r\n\.。]{2,60})",
                @"作者[:：]\s*([^\r\n\.。]{1,40})",
                @"制作[:：]\s*([^\r\n\.。]{1,40})",
                @"(?:^|\n)\s*By\s+([^\r\n\.。]{2,50})",
                @"\bby\s+([A-Z][^\r\n\.。]{1,40})"
            };
            foreach (var p in patterns)
            {
                var m = Regex.Match(desc, p, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string v = m.Groups[1].Value.Trim().TrimEnd(',', ';', ' ');
                    if (v.Length > 0) return v;
                }
            }
            return "";
        }
    }
}
