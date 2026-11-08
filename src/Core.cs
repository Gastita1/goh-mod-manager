using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GoHMM
{
    // ---------------------------------------------------------------------
    // 极简 JSON（只处理本程序需要的结构，避免额外程序集依赖）
    // ---------------------------------------------------------------------
    public static class Json
    {
        public static object Parse(string s)
        {
            int i = 0;
            object v = ParseValue(s, ref i);
            return v;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++; // {
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object val = ParseValue(s, ref i);
                d[key] = val;
            }
            return d;
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                list.Add(ParseValue(s, ref i));
            }
            return list;
        }

        static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 3 < s.Length)
                            {
                                string hex = s.Substring(i, 4);
                                i += 4;
                                int cp = Convert.ToInt32(hex, 16);
                                sb.Append((char)cp);
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "-+.eE0123456789".IndexOf(s[i]) >= 0) i++;
            string t = s.Substring(start, i - start);
            double d;
            if (double.TryParse(t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
            return 0.0;
        }

        public static string Esc(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    // ---------------------------------------------------------------------
    // 配置
    // ---------------------------------------------------------------------
    public class AppConfig
    {
        public string GamePath = "";
        public string WorkshopPath = "";
        public string ProfileFile = "";
        public string SteamPath = "";
        public string Theme = "dark";
        public string Language = "";
        public string LastPreset = "";
        public Dictionary<string, List<string>> Presets = new Dictionary<string, List<string>>();
        public int WinW = 1280;
        public int WinH = 840;

        public static string Dir
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("GOHMM_HOME");
                if (!string.IsNullOrEmpty(env))
                {
                    try { Directory.CreateDirectory(env); } catch { }
                    return env;
                }
                // 优先跟着程序走（放在 F 盘），不可写再退回用户目录
                string side = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
                if (Writable(side)) return side;

                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".goh-mod-manager-plus");
                if (Writable(p)) return p;
                return side;
            }
        }

        static bool Writable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static string FilePath { get { return Path.Combine(Dir, "config.json"); } }
        public static string LogPath { get { return Path.Combine(Dir, "log.txt"); } }

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                if (!File.Exists(FilePath)) return cfg;
                object root = Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8));
                var d = root as Dictionary<string, object>;
                if (d == null) return cfg;

                cfg.GamePath = Str(d, "game_path");
                cfg.WorkshopPath = Str(d, "workshop_path");
                cfg.ProfileFile = Str(d, "profile_path");
                cfg.SteamPath = Str(d, "steam_path");
                cfg.Theme = Str(d, "theme", "dark");
                cfg.Language = Str(d, "language");
                cfg.LastPreset = Str(d, "last_preset");
                cfg.WinW = (int)Num(d, "win_w", 1280);
                cfg.WinH = (int)Num(d, "win_h", 840);

                object presetsObj;
                if (d.TryGetValue("presets", out presetsObj) && presetsObj is Dictionary<string, object>)
                {
                    foreach (var kv in (Dictionary<string, object>)presetsObj)
                    {
                        var arr = kv.Value as List<object>;
                        var list = new List<string>();
                        if (arr != null)
                            foreach (var o in arr)
                                if (o is string) list.Add((string)o);
                        cfg.Presets[kv.Key] = list;
                    }
                }
            }
            catch (Exception ex) { Log(L.T("配置读取失败: ") + ex.Message); }
            return cfg;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"game_path\": \"").Append(Json.Esc(GamePath)).Append("\",\n");
            sb.Append("  \"workshop_path\": \"").Append(Json.Esc(WorkshopPath)).Append("\",\n");
            sb.Append("  \"profile_path\": \"").Append(Json.Esc(ProfileFile)).Append("\",\n");
            sb.Append("  \"steam_path\": \"").Append(Json.Esc(SteamPath)).Append("\",\n");
            sb.Append("  \"theme\": \"").Append(Json.Esc(Theme)).Append("\",\n");
            sb.Append("  \"language\": \"").Append(Json.Esc(Language)).Append("\",\n");
            sb.Append("  \"last_preset\": \"").Append(Json.Esc(LastPreset)).Append("\",\n");
            sb.Append("  \"win_w\": ").Append(WinW).Append(",\n");
            sb.Append("  \"win_h\": ").Append(WinH).Append(",\n");
            sb.Append("  \"presets\": {\n");
            int n = 0;
            foreach (var kv in Presets)
            {
                sb.Append("    \"").Append(Json.Esc(kv.Key)).Append("\": [");
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('"').Append(Json.Esc(kv.Value[i])).Append('"');
                }
                sb.Append("]");
                if (++n < Presets.Count) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("  }\n}\n");
            try
            {
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log(L.T("配置保存失败: ") + ex.Message);
            }
        }

        static string Str(Dictionary<string, object> d, string k, string def = "")
        {
            object o;
            if (d.TryGetValue(k, out o) && o is string) return (string)o;
            return def;
        }

        static double Num(Dictionary<string, object> d, string k, double def)
        {
            object o;
            if (d.TryGetValue(k, out o) && o is double) return (double)o;
            return def;
        }

        // 从旧的 alexbdka 版管理器导入（%USERPROFILE%\.goh-mod-manager\config.json）
        public bool TryImportLegacy()
        {
            try
            {
                string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".goh-mod-manager", "config.json");
                if (!File.Exists(legacy)) return false;
                var d = Json.Parse(File.ReadAllText(legacy, Encoding.UTF8)) as Dictionary<string, object>;
                if (d == null) return false;

                string gp = Str(d, "game_path");
                string wp = Str(d, "workshop_path");
                string pp = Str(d, "profile_path");
                if (!string.IsNullOrEmpty(gp) && Directory.Exists(gp)) GamePath = gp;
                if (!string.IsNullOrEmpty(wp) && Directory.Exists(wp)) WorkshopPath = wp;
                if (!string.IsNullOrEmpty(pp) && File.Exists(pp)) ProfileFile = pp;

                object presetsObj;
                if (d.TryGetValue("presets", out presetsObj) && presetsObj is Dictionary<string, object>)
                {
                    foreach (var kv in (Dictionary<string, object>)presetsObj)
                    {
                        var arr = kv.Value as List<object>;
                        if (arr == null) continue;
                        var list = new List<string>();
                        foreach (var o in arr)
                        {
                            string s = o as string;
                            if (string.IsNullOrEmpty(s)) continue;
                            list.Add(s.StartsWith("mod_") ? s : "mod_" + s);
                        }
                        if (list.Count > 0) Presets[kv.Key] = list;
                    }
                }
                return true;
            }
            catch (Exception ex) { Log(L.T("导入旧配置失败: ") + ex.Message); return false; }
        }
    }

    // ---------------------------------------------------------------------
    // 路径发现
    // ---------------------------------------------------------------------
    public class Paths
    {
        public string SteamDir = "";
        public string GameDir = "";
        public string WorkshopDir = "";
        public string ProfilesDir = "";
        public string ProfileFile = "";
        public string ProfileDir = "";
        public string GameVersion = "";

        public static Paths Discover(AppConfig cfg)
        {
            var p = new Paths();
            p.SteamDir = GetSteamDir() ?? "";

            var libs = GetLibraries(p.SteamDir);
            foreach (var lib in libs)
            {
                if (string.IsNullOrEmpty(p.GameDir))
                {
                    string g = Path.Combine(lib, @"steamapps\common\Call to Arms - Gates of Hell");
                    if (Directory.Exists(g)) p.GameDir = g;
                    else
                    {
                        string common = Path.Combine(lib, @"steamapps\common");
                        if (Directory.Exists(common))
                        {
                            foreach (var d in Directory.GetDirectories(common))
                            {
                                string name = Path.GetFileName(d).ToLower();
                                if (name.Contains("gates of hell") || name.Contains("call to arms"))
                                {
                                    p.GameDir = d;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (string.IsNullOrEmpty(p.WorkshopDir))
                {
                    string w = Path.Combine(lib, @"steamapps\workshop\content\400750");
                    if (Directory.Exists(w)) p.WorkshopDir = w;
                }
            }

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string pd = Path.Combine(docs, @"My Games\gates of hell\profiles");
            if (!Directory.Exists(pd))
            {
                string alt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"OneDrive\Documents\My Games\gates of hell\profiles");
                if (Directory.Exists(alt)) pd = alt;
            }
            p.ProfilesDir = pd;

            // 配置里的自定义路径优先
            if (!string.IsNullOrEmpty(cfg.GamePath) && Directory.Exists(cfg.GamePath)) p.GameDir = cfg.GamePath;
            if (!string.IsNullOrEmpty(cfg.WorkshopPath) && Directory.Exists(cfg.WorkshopPath)) p.WorkshopDir = cfg.WorkshopPath;

            // 选择 profile：配置里指定的优先，否则取最近修改的
            if (!string.IsNullOrEmpty(cfg.ProfileFile) && File.Exists(cfg.ProfileFile))
            {
                p.ProfileFile = cfg.ProfileFile;
            }
            else if (Directory.Exists(pd))
            {
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (var dir in Directory.GetDirectories(pd))
                {
                    string opt = Path.Combine(dir, "options.set");
                    string set = Path.Combine(dir, "settings.set");
                    DateTime t = DateTime.MinValue;
                    if (File.Exists(opt)) t = File.GetLastWriteTime(opt);
                    if (File.Exists(set) && File.GetLastWriteTime(set) > t) t = File.GetLastWriteTime(set);
                    if (t > bestTime) { bestTime = t; best = opt; }
                }
                if (best != null && File.Exists(best)) p.ProfileFile = best;
            }

            if (!string.IsNullOrEmpty(p.ProfileFile)) p.ProfileDir = Path.GetDirectoryName(p.ProfileFile);

            // 游戏版本（取 profile 里记录的 lastGameVersion）
            try
            {
                if (!string.IsNullOrEmpty(p.ProfileDir))
                {
                    string s = Path.Combine(p.ProfileDir, "settings.set");
                    if (File.Exists(s))
                    {
                        var m = Regex.Match(File.ReadAllText(s), "lastGameVersion\\s+\"([^\"]+)\"");
                        if (m.Success) p.GameVersion = m.Groups[1].Value;
                    }
                }
            }
            catch { }

            return p;
        }

        public static string GetSteamDir()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string s = k.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(s) && Directory.Exists(s)) return s.Replace('/', '\\');
                    }
                }
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string s = k.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(s) && Directory.Exists(s)) return s.Replace('/', '\\');
                    }
                }
            }
            catch { }
            string d = @"C:\Program Files (x86)\Steam";
            if (Directory.Exists(d)) return d;
            return null;
        }

        static List<string> GetLibraries(string steam)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(steam)) list.Add(steam);
            try
            {
                if (!string.IsNullOrEmpty(steam))
                {
                    string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        string txt = File.ReadAllText(vdf);
                        foreach (Match m in Regex.Matches(txt, "\"path\"\\s+\"([^\"]+)\""))
                            AddLib(list, m.Groups[1].Value);
                        foreach (Match m in Regex.Matches(txt, "\"\\d+\"\\s+\"([A-Za-z]:[^\"]*)\""))
                            AddLib(list, m.Groups[1].Value);
                    }
                }
            }
            catch { }
            return list;
        }

        static void AddLib(List<string> list, string raw)
        {
            string p = raw.Replace(@"\\", @"\").Replace('/', '\\').TrimEnd('\\');
            if (p.Length == 0) return;
            try { if (Directory.Exists(p) && !list.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))) list.Add(p); }
            catch { }
        }
    }

    // ---------------------------------------------------------------------
    // 模组
    // ---------------------------------------------------------------------
    public class ModEntry
    {
        public string Id = "";
        public string Folder = "";
        public string Dir = "";
        public bool IsWorkshop;
        public string Name = "";
        public string Desc = "";
        public List<string> Tags = new List<string>();
        public List<string> Requires = new List<string>();
        public string MinVersion = "";
        public string MaxVersion = "";
        public long Size;
        public int FileCount;
        public string Thumb = "";
        public string Author = "";
        public string BrokenReason = "";
        public List<string> Issues = new List<string>();

        public bool Broken { get { return BrokenReason.Length > 0; } }
        public string SetId { get { return "mod_" + Id; } }
        public string KindText { get { return IsWorkshop ? L.T("创意工坊") : L.T("本地"); } }

        public override string ToString() { return Name; }
    }

    // ---------------------------------------------------------------------
    // mod.info 解析（S 表达式）
    // ---------------------------------------------------------------------
    public class Sexp
    {
        public string Atom;
        public List<Sexp> Kids;
        public bool IsList { get { return Kids != null; } }

        public static List<Sexp> Parse(string text)
        {
            int i = 0;
            var roots = new List<Sexp>();
            var stack = new Stack<Sexp>();
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == ';') { while (i < text.Length && text[i] != '\n') i++; continue; }
                if (c == '{')
                {
                    i++;
                    var node = new Sexp();
                    node.Kids = new List<Sexp>();
                    if (stack.Count > 0) stack.Peek().Kids.Add(node);
                    else roots.Add(node);
                    stack.Push(node);
                    continue;
                }
                if (c == '}')
                {
                    i++;
                    if (stack.Count > 0) stack.Pop();
                    continue;
                }
                if (c == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < text.Length)
                    {
                        char ch = text[i++];
                        if (ch == '"') break;
                        if (ch == '\\' && i < text.Length)
                        {
                            char e = text[i++];
                            switch (e)
                            {
                                case 'n': sb.Append('\n'); break;
                                case 't': sb.Append('\t'); break;
                                case 'r': sb.Append('\r'); break;
                                default: sb.Append(e); break;
                            }
                        }
                        else sb.Append(ch);
                    }
                    var atom = new Sexp();
                    atom.Atom = sb.ToString();
                    if (stack.Count > 0) stack.Peek().Kids.Add(atom);
                    continue;
                }
                // bare token
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}' && text[i] != '"' && text[i] != ';') i++;
                if (i > start)
                {
                    var atom = new Sexp();
                    atom.Atom = text.Substring(start, i - start);
                    if (stack.Count > 0) stack.Peek().Kids.Add(atom);
                }
            }
            return roots;
        }
    }

    public static class ModInfoReader
    {
        public static void Apply(string infoText, ModEntry m)
        {
            var roots = Sexp.Parse(infoText);
            if (roots.Count == 0) return;
            var root = roots[0];
            if (!root.IsList) return;
            foreach (var kid in root.Kids)
            {
                if (!kid.IsList || kid.Kids.Count == 0) continue;
                string key = kid.Kids[0].Atom ?? "";
                var vals = new List<string>();
                for (int i = 1; i < kid.Kids.Count; i++)
                    if (kid.Kids[i].Atom != null) vals.Add(kid.Kids[i].Atom);

                switch (key.ToLower())
                {
                    case "name": if (vals.Count > 0) m.Name = vals[0]; break;
                    case "desc": if (vals.Count > 0) m.Desc = Cleanup(vals[0]); break;
                    case "tags":
                        foreach (var v in vals) m.Tags.AddRange(v.Split(','));
                        m.Tags = m.Tags.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                        break;
                    case "author":
                    case "creator":
                        if (vals.Count > 0) m.Author = vals[0].Trim();
                        break;
                    case "require":
                        foreach (var v in vals) m.Requires.Add(v.Trim());
                        break;
                    case "mingameversion": if (vals.Count > 0) m.MinVersion = vals[0]; break;
                    case "maxgameversion": if (vals.Count > 0) m.MaxVersion = vals[0]; break;
                }
            }
        }

        // 去掉 <c(FFFFFF)> <e(shadow)> 之类的富文本标记，保留纯文本
        public static string Cleanup(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, "<[^>]*>", "");
            s = s.Replace("\r\n", "\n");
            return s.Trim();
        }

        public static bool VersionInRange(string min, string max, string current)
        {
            if (string.IsNullOrEmpty(current)) return true;
            if (string.IsNullOrEmpty(min) || min == "any") return true;
            try
            {
                var cur = Version.Parse(Normalize(current));
                if (!string.IsNullOrEmpty(min) && min != "any")
                    if (cur < Version.Parse(Normalize(min))) return false;
                if (!string.IsNullOrEmpty(max) && max != "any")
                    if (cur > Version.Parse(Normalize(max))) return false;
            }
            catch { }
            return true;
        }

        static string Normalize(string v)
        {
            // 1.067.0 -> 1.67.0 需要按每段数字归一，避免 1.067 < 1.5 的字符串陷阱
            var parts = v.Split('.');
            var outp = new List<string>();
            foreach (var p in parts)
            {
                int n;
                if (int.TryParse(p, out n)) outp.Add(n.ToString());
                else outp.Add("0");
            }
            while (outp.Count < 3) outp.Add("0");
            return string.Join(".", outp.ToArray());
        }
    }

    // ---------------------------------------------------------------------
    // 模组目录扫描
    // ---------------------------------------------------------------------
    public static class Catalog
    {
        public static List<ModEntry> Scan(string workshopDir, string gameDir, string gameVersion)
        {
            var list = new List<ModEntry>();

            if (!string.IsNullOrEmpty(workshopDir) && Directory.Exists(workshopDir))
            {
                foreach (var dir in Directory.GetDirectories(workshopDir))
                {
                    string folder = Path.GetFileName(dir);
                    if (folder.StartsWith(".")) continue;
                    var m = new ModEntry();
                    m.Id = folder;
                    m.Folder = folder;
                    m.Dir = dir;
                    m.IsWorkshop = true;
                    LoadOne(m, true);
                    Finish(m, gameVersion);
                    list.Add(m);
                }
            }

            if (!string.IsNullOrEmpty(gameDir))
            {
                string modsDir = Path.Combine(gameDir, "mods");
                if (Directory.Exists(modsDir))
                {
                    foreach (var dir in Directory.GetDirectories(modsDir))
                    {
                        string folder = Path.GetFileName(dir);
                        string low = folder.ToLower();
                        if (low == "template" || low == "tools") continue;
                        var m = new ModEntry();
                        m.Id = folder;
                        m.Folder = folder;
                        m.Dir = dir;
                        m.IsWorkshop = false;
                        LoadOne(m, false);
                        Finish(m, gameVersion);
                        list.Add(m);
                    }
                }
            }

            list.Sort(delegate(ModEntry a, ModEntry b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            return list;
        }

        static void LoadOne(ModEntry m, bool workshop)
        {
            try
            {
                string infoPath = Path.Combine(m.Dir, "mod.info");
                string infoText = null;
                if (File.Exists(infoPath)) infoText = File.ReadAllText(infoPath, Encoding.UTF8);

                if (infoText == null && !workshop)
                {
                    // 本地模组可能把 mod.info 放在 1.pat 里
                    string pat = Path.Combine(m.Dir, "1.pat");
                    if (File.Exists(pat))
                    {
                        try
                        {
                            using (var zip = ZipFile.OpenRead(pat))
                            {
                                var entry = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').TrimStart('/').ToLower() == "mod.info");
                                if (entry != null)
                                {
                                    using (var sr = new StreamReader(entry.Open(), Encoding.UTF8))
                                        infoText = sr.ReadToEnd();
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (infoText == null)
                {
                    m.BrokenReason = L.T("缺少 mod.info");
                    m.Name = m.Folder + L.T("（未识别）");
                    return;
                }

                ModInfoReader.Apply(infoText, m);
                if (string.IsNullOrEmpty(m.Name)) m.Name = m.Folder;

                CountFiles(m);

                foreach (var f in new string[] { "logo.jpg", "logo.png", "thumb.jpg", "thumb.png", "preview.jpg", "preview.png" })
                {
                    string p = Path.Combine(m.Dir, f);
                    if (File.Exists(p)) { m.Thumb = p; break; }
                }

                if (m.FileCount == 0) m.BrokenReason = L.T("目录为空（可能未下载完）");
            }
            catch (Exception ex)
            {
                m.BrokenReason = L.T("读取失败：") + ex.Message;
                if (string.IsNullOrEmpty(m.Name)) m.Name = m.Folder;
            }
        }

        static void CountFiles(ModEntry m)
        {
            try
            {
                long size = 0;
                int count = 0;
                foreach (var f in Directory.EnumerateFiles(m.Dir, "*", SearchOption.AllDirectories))
                {
                    count++;
                    try { size += new FileInfo(f).Length; } catch { }
                    if (count > 200000) break;
                }
                m.Size = size;
                m.FileCount = count;
            }
            catch { }
        }

        static void Finish(ModEntry m, string gameVersion)
        {
            if (m.Broken) return;
            if (!ModInfoReader.VersionInRange(m.MinVersion, m.MaxVersion, gameVersion))
                m.Issues.Add(L.T("版本不兼容"));
        }
    }

    // ---------------------------------------------------------------------
    // options.set 读写
    // ---------------------------------------------------------------------
    public static class OptionsFile
    {
        public static List<string> ReadActive(string path)
        {
            var result = new List<string>();
            try
            {
                if (!File.Exists(path)) return result;
                string text = File.ReadAllText(path, Encoding.UTF8);
                int start = text.IndexOf("{mods", StringComparison.Ordinal);
                if (start < 0) return result;
                int end = FindBlockEnd(text, start);
                if (end < 0) return result;
                string block = text.Substring(start, end - start + 1);
                foreach (Match m in Regex.Matches(block, "\"([^\"]+)\""))
                {
                    string raw = m.Groups[1].Value;
                    int colon = raw.LastIndexOf(':');
                    string id = colon > 0 ? raw.Substring(0, colon) : raw;
                    result.Add(id);
                }
            }
            catch (Exception ex) { AppConfig.Log("读取 options.set 失败: " + ex.Message); }
            return result;
        }

        // 保留每个 mod 原有的 :N 后缀
        public static void WriteActive(string path, IList<string> setIds)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";

            var suffixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int start = text.IndexOf("{mods", StringComparison.Ordinal);
            int blockStart = start;
            string indent = "\t";
            if (start >= 0)
            {
                int end = FindBlockEnd(text, start);
                if (end > 0)
                {
                    foreach (Match m in Regex.Matches(text.Substring(start, end - start + 1), "\"([^\"]+)\""))
                    {
                        string raw = m.Groups[1].Value;
                        int colon = raw.LastIndexOf(':');
                        if (colon > 0) suffixes[raw.Substring(0, colon)] = raw.Substring(colon + 1);
                    }
                    string before = text.Substring(0, start);
                    int lineStart = before.LastIndexOf('\n');
                    string linePrefix = lineStart >= 0 ? before.Substring(lineStart + 1) : before;
                    if (linePrefix.Trim().Length == 0)
                    {
                        indent = linePrefix;
                        blockStart = lineStart + 1;   // 连同原来的缩进一起替换，避免首行多缩进
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append(indent).Append("{mods").Append(nl);
            foreach (var id in setIds)
            {
                string suffix;
                if (!suffixes.TryGetValue(id, out suffix)) suffix = "0";
                sb.Append(indent).Append('\t').Append('"').Append(id).Append(':').Append(suffix).Append('"').Append(nl);
            }
            sb.Append(indent).Append('}');
            string block = sb.ToString();

            if (start >= 0)
            {
                int end = FindBlockEnd(text, start);
                if (end > 0)
                {
                    text = text.Substring(0, blockStart) + block + text.Substring(end + 1);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    return;
                }
            }

            // 没有 mods 块：插到最外层结束大括号前
            int last = text.LastIndexOf('}');
            if (last < 0) throw new InvalidDataException(L.T("options.set 结构异常，找不到结束括号"));
            string ins = block + nl;
            text = text.Insert(last, ins);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        static int FindBlockEnd(string text, int start)
        {
            int depth = 0;
            bool inQuote = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuote)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inQuote = false;
                    continue;
                }
                if (c == '"') { inQuote = true; continue; }
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }
    }

    // ---------------------------------------------------------------------
    // 备份
    // ---------------------------------------------------------------------
    public static class Backups
    {
        public static string Dir
        {
            get
            {
                string p = Path.Combine(AppConfig.Dir, "backups");
                Directory.CreateDirectory(p);
                return p;
            }
        }

        public static string Create(string optionsFile)
        {
            if (string.IsNullOrEmpty(optionsFile) || !File.Exists(optionsFile)) return null;
            string name = "options-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".set";
            string dst = Path.Combine(Dir, name);
            int n = 1;
            while (File.Exists(dst))
            {
                dst = Path.Combine(Dir, "options-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + n + ".set");
                n++;
            }
            File.Copy(optionsFile, dst, false);
            Prune(40);
            return dst;
        }

        static void Prune(int keep)
        {
            try
            {
                var files = new DirectoryInfo(Dir).GetFiles("options-*.set").OrderByDescending(f => f.LastWriteTime).ToList();
                for (int i = keep; i < files.Count; i++) files[i].Delete();
            }
            catch { }
        }

        public static List<string> List()
        {
            try
            {
                return new DirectoryInfo(Dir).GetFiles("options-*.set")
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => f.FullName).ToList();
            }
            catch { return new List<string>(); }
        }

        public static void Restore(string backup, string optionsFile)
        {
            if (!File.Exists(backup)) throw new FileNotFoundException(L.T("备份文件不存在"), backup);
            Create(optionsFile);
            File.Copy(backup, optionsFile, true);
        }
    }

    // ---------------------------------------------------------------------
    // 冲突检测：同名资源文件被多个模组同时覆盖
    // ---------------------------------------------------------------------
    public class ConflictGroup
    {
        public string Path;
        public List<ModEntry> Mods = new List<ModEntry>();
    }

    public static class ConflictScanner
    {
        static readonly Dictionary<string, string[]> cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache() { cache.Clear(); }

        public static string[] Files(ModEntry m)
        {
            string key = m.Dir;
            string[] hit;
            if (cache.TryGetValue(key, out hit)) return hit;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var f in Directory.EnumerateFiles(m.Dir, "*", SearchOption.AllDirectories))
                {
                    string rel = f.Substring(m.Dir.Length).TrimStart('\\', '/').Replace('\\', '/');
                    // 只有会进游戏虚拟文件系统的资源才算冲突；mod.info / logo / 说明文档不算
                    if (!IsResourcePath(rel)) continue;
                    set.Add(rel);
                    if (set.Count > 60000) break;
                }
                // 本地模组的 .pat 压缩包内容
                foreach (var pat in Directory.GetFiles(m.Dir, "*.pat"))
                {
                    try
                    {
                        using (var zip = ZipFile.OpenRead(pat))
                        {
                            foreach (var e in zip.Entries)
                            {
                                if (e.FullName.EndsWith("/")) continue;
                                string name = e.FullName.Replace('\\', '/').TrimStart('/');
                                if (name.Equals("mod.info", StringComparison.OrdinalIgnoreCase)) continue;
                                set.Add(name);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            var arr = set.ToArray();
            cache[key] = arr;
            return arr;
        }

        // 只有 resource/ 和 localizations/ 下的文件会被引擎合并进 VFS
        public static bool IsResourcePath(string rel)
        {
            if (string.IsNullOrEmpty(rel)) return false;
            string r = rel.Replace('\\', '/').TrimStart('/');
            if (r.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            return r.StartsWith("resource/", StringComparison.OrdinalIgnoreCase)
                || r.StartsWith("localizations/", StringComparison.OrdinalIgnoreCase);
        }

        public static List<ConflictGroup> Scan(IList<ModEntry> mods, out int totalUniqueFiles)
        {
            var map = new Dictionary<string, List<ModEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in mods)
            {
                foreach (var f in Files(m))
                {
                    List<ModEntry> list;
                    if (!map.TryGetValue(f, out list)) { list = new List<ModEntry>(); map[f] = list; }
                    list.Add(m);
                }
            }
            totalUniqueFiles = map.Count;

            var groups = new List<ConflictGroup>();
            foreach (var kv in map)
            {
                if (kv.Value.Count < 2) continue;
                var g = new ConflictGroup();
                g.Path = kv.Key;
                g.Mods = kv.Value;
                groups.Add(g);
            }
            groups.Sort(delegate(ConflictGroup a, ConflictGroup b) { return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase); });
            return groups;
        }
    }

    // ---------------------------------------------------------------------
    // 小工具
    // ---------------------------------------------------------------------
    public static class Fmt
    {
        public static string Size(long bytes)
        {
            if (bytes <= 0) return "—";
            string[] u = { "B", "KB", "MB", "GB" };
            double v = bytes;
            int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return (i == 0 ? ((long)v).ToString() : v.ToString("0.#")) + " " + u[i];
        }

        public static string Short(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }
}
