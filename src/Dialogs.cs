using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GoHMM
{
    public class DarkForm : Form
    {
        public DarkForm()
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(13, false);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected Label MakeLabel(string text, int x, int y, int w, bool sub)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.BackColor = Theme.Bg;
            l.ForeColor = sub ? Theme.Sub : Theme.Text;
            l.Font = Theme.F(sub ? 11.5f : 13, false);
            l.Bounds = new Rectangle(x, y, w, Theme.P(22));
            Controls.Add(l);
            return l;
        }

        protected TextBox MakeBox(string text, int x, int y, int w)
        {
            var t = new TextBox();
            t.Text = text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Theme.PanelAlt;
            t.ForeColor = Theme.Text;
            t.Font = Theme.F(12.5f, false);
            t.Bounds = new Rectangle(x, y, w, Theme.P(28));
            Controls.Add(t);
            return t;
        }
    }

    public class InputDialog : DarkForm
    {
        TextBox box;
        public string Value { get { return box.Text.Trim(); } }

        public static string Show(IWin32Window owner, string title, string label, string def)
        {
            using (var d = new InputDialog(title, label, def))
            {
                if (d.ShowDialog(owner) != DialogResult.OK) return null;
                return d.Value;
            }
        }

        InputDialog(string title, string label, string def)
        {
            Text = title;
            ClientSize = new Size(Theme.P(460), Theme.P(190));
            MakeLabel(label, Theme.P(22), Theme.P(22), Theme.P(416), false);
            box = MakeBox(def, Theme.P(22), Theme.P(56), Theme.P(416));
            box.SelectAll();

            var ok = new FlatButton();
            ok.Text = L.T("确定");
            ok.Accent = true;
            ok.Bounds = new Rectangle(ClientSize.Width - Theme.P(110), Theme.P(118), Theme.P(88), Theme.P(36));
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);

            var cancel = new FlatButton();
            cancel.Text = L.T("取消");
            cancel.Bounds = new Rectangle(ClientSize.Width - Theme.P(206), Theme.P(118), Theme.P(86), Theme.P(36));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = null;
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); }
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };
        }
    }

    public class SettingsDialog : DarkForm
    {
        public string GamePath { get { return boxGame.Text.Trim(); } }
        public string WorkshopPath { get { return boxShop.Text.Trim(); } }
        public string ProfileFile { get { return boxProfile.Text.Trim(); } }

        TextBox boxGame, boxShop, boxProfile;

        public SettingsDialog(AppConfig cfg, Paths paths)
        {
            Text = L.T("设置");
            ClientSize = new Size(Theme.P(760), Theme.P(380));

            MakeLabel(L.T("路径设置"), Theme.P(24), Theme.P(18), Theme.P(300), false);

            MakeLabel(L.T("游戏目录"), Theme.P(24), Theme.P(58), Theme.P(120), true);
            boxGame = MakeBox(string.IsNullOrEmpty(paths.GameDir) ? cfg.GamePath : paths.GameDir, Theme.P(24), Theme.P(82), Theme.P(600));
            var b1 = BrowseButton(Theme.P(632), Theme.P(80), delegate { PickFolder(boxGame); });
            Controls.Add(b1);

            MakeLabel(L.T("创意工坊目录（…\\workshop\\content\\400750）"), Theme.P(24), Theme.P(126), Theme.P(500), true);
            boxShop = MakeBox(string.IsNullOrEmpty(paths.WorkshopDir) ? cfg.WorkshopPath : paths.WorkshopDir, Theme.P(24), Theme.P(150), Theme.P(600));
            var b2 = BrowseButton(Theme.P(632), Theme.P(148), delegate { PickFolder(boxShop); });
            Controls.Add(b2);

            MakeLabel(L.T("存档配置 options.set"), Theme.P(24), Theme.P(194), Theme.P(500), true);
            boxProfile = MakeBox(string.IsNullOrEmpty(paths.ProfileFile) ? cfg.ProfileFile : paths.ProfileFile, Theme.P(24), Theme.P(218), Theme.P(600));
            var b3 = BrowseButton(Theme.P(632), Theme.P(216), delegate { PickFile(boxProfile); });
            Controls.Add(b3);

            var hint = MakeLabel(L.T("提示：一般不用手填。点下面的「自动检测」会从 Steam 注册表和 libraryfolders.vdf 里重新找一遍。"), Theme.P(24), Theme.P(258), Theme.P(700), true);
            hint.Font = Theme.F(11, false);
            hint.ForeColor = Theme.Dim;

            var auto = new FlatButton();
            auto.Text = L.T("自动检测");
            auto.Bounds = new Rectangle(Theme.P(24), Theme.P(300), Theme.P(110), Theme.P(38));
            auto.Click += delegate
            {
                var p = Paths.Discover(new AppConfig());
                if (!string.IsNullOrEmpty(p.GameDir)) boxGame.Text = p.GameDir;
                if (!string.IsNullOrEmpty(p.WorkshopDir)) boxShop.Text = p.WorkshopDir;
                if (!string.IsNullOrEmpty(p.ProfileFile)) boxProfile.Text = p.ProfileFile;
            };
            Controls.Add(auto);

            var ok = new FlatButton();
            ok.Text = L.T("保存");
            ok.Accent = true;
            ok.Bounds = new Rectangle(ClientSize.Width - Theme.P(120), Theme.P(300), Theme.P(96), Theme.P(38));
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);

            var cancel = new FlatButton();
            cancel.Text = L.T("取消");
            cancel.Bounds = new Rectangle(ClientSize.Width - Theme.P(226), Theme.P(300), Theme.P(96), Theme.P(38));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
        }

        FlatButton BrowseButton(int x, int y, EventHandler h)
        {
            var b = new FlatButton();
            b.Text = L.T("浏览…");
            b.Bounds = new Rectangle(x, y, Theme.P(96), Theme.P(32));
            b.Click += h;
            return b;
        }

        void PickFolder(TextBox box)
        {
            using (var f = new FolderBrowserDialog())
            {
                if (Directory.Exists(box.Text)) f.SelectedPath = box.Text;
                if (f.ShowDialog(this) == DialogResult.OK) box.Text = f.SelectedPath;
            }
        }

        void PickFile(TextBox box)
        {
            using (var f = new OpenFileDialog())
            {
                f.Filter = L.T("options.set|options.set|所有文件|*.*");
                string dir = Path.GetDirectoryName(box.Text);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) f.InitialDirectory = dir;
                if (f.ShowDialog(this) == DialogResult.OK) box.Text = f.FileName;
            }
        }
    }

    public class BackupsDialog : DarkForm
    {
        public string RestoredPath;
        ListBox list;
        string optionsFile;
        List<string> files = new List<string>();

        public BackupsDialog(string optionsFile)
        {
            this.optionsFile = optionsFile;
            Text = L.T("备份与还原");
            ClientSize = new Size(Theme.P(720), Theme.P(480));

            MakeLabel(L.T("每次保存前都会自动备份 options.set，下面是最近的备份。"), Theme.P(22), Theme.P(16), Theme.P(660), true);

            list = new ListBox();
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.PanelAlt;
            list.ForeColor = Theme.Text;
            list.Font = Theme.F(12.5f, false);
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = Theme.P(40);
            list.Bounds = new Rectangle(Theme.P(22), Theme.P(46), Theme.P(470), Theme.P(360));
            list.DrawItem += new DrawItemEventHandler(DrawBackup);
            list.DoubleClick += delegate { Restore(); };
            Controls.Add(list);

            var bRestore = new FlatButton();
            bRestore.Text = L.T("还原这个备份");
            bRestore.Accent = true;
            bRestore.Bounds = new Rectangle(Theme.P(512), Theme.P(46), Theme.P(180), Theme.P(40));
            bRestore.Click += delegate { Restore(); };
            Controls.Add(bRestore);

            var bOpen = new FlatButton();
            bOpen.Text = L.T("打开备份文件夹");
            bOpen.Bounds = new Rectangle(Theme.P(512), Theme.P(96), Theme.P(180), Theme.P(40));
            bOpen.Click += delegate { Process.Start("explorer.exe", "\"" + Backups.Dir + "\""); };
            Controls.Add(bOpen);

            var bDelete = new FlatButton();
            bDelete.Text = L.T("删除选中备份");
            bDelete.Ghost = true;
            bDelete.Bounds = new Rectangle(Theme.P(512), Theme.P(146), Theme.P(180), Theme.P(40));
            bDelete.Click += delegate
            {
                if (list.SelectedIndex < 0) return;
                try { File.Delete(files[list.SelectedIndex]); } catch { }
                Reload();
            };
            Controls.Add(bDelete);

            var bClose = new FlatButton();
            bClose.Text = L.T("关闭");
            bClose.Bounds = new Rectangle(ClientSize.Width - Theme.P(120), ClientSize.Height - Theme.P(60), Theme.P(96), Theme.P(38));
            bClose.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(bClose);

            Reload();
        }

        void Reload()
        {
            files = Backups.List();
            list.Items.Clear();
            foreach (var f in files) list.Items.Add(f);
        }

        void DrawBackup(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var b = new SolidBrush(sel ? Theme.PanelHover : Theme.PanelAlt))
                g.FillRectangle(b, e.Bounds);
            string f = (string)list.Items[e.Index];
            string name = Path.GetFileNameWithoutExtension(f);
            var info = new FileInfo(f);
            string when = info.LastWriteTime.ToString("yyyy-MM-dd  HH:mm:ss");
            Theme.RenderText(g, when, Theme.F(13, true), Theme.Text, new Rectangle(e.Bounds.X + Theme.P(10), e.Bounds.Y + Theme.P(6), Theme.P(300), Theme.P(18)), StringAlignment.Near);
            Theme.RenderText(g, Fmt.Size(info.Length) + "   ·   " + name, Theme.F(11, false), Theme.Dim,
                new Rectangle(e.Bounds.X + Theme.P(10), e.Bounds.Y + Theme.P(21), Theme.P(420), Theme.P(16)), StringAlignment.Near);
        }

        void Restore()
        {
            if (list.SelectedIndex < 0) { MessageBox.Show(this, L.T("先选一个备份。"), L.T("提示")); return; }
            if (string.IsNullOrEmpty(optionsFile) || !File.Exists(optionsFile))
            {
                MessageBox.Show(this, L.T("找不到当前的 options.set。"), L.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string f = files[list.SelectedIndex];
            if (MessageBox.Show(this, L.T("用这个备份覆盖当前的模组列表吗？\n\n") + Path.GetFileName(f), L.T("还原"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                Backups.Restore(f, optionsFile);
                RestoredPath = f;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.T("还原失败：") + ex.Message, L.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class ConflictsDialog : DarkForm
    {
        public ConflictsDialog(List<ConflictGroup> groups)
        {
            Text = L.T("文件冲突（") + groups.Count + L.T(" 处）");
            ClientSize = new Size(Theme.P(860), Theme.P(560));

            var hint = MakeLabel(L.T("下面这些文件会被多个已启用的模组同时覆盖 —— 加载顺序里靠后的那个会赢。"), Theme.P(20), Theme.P(14), Theme.P(820), true);
            hint.Font = Theme.F(11.5f, false);
            hint.ForeColor = Theme.Sub;

            var lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = false;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = Theme.PanelAlt;
            lv.ForeColor = Theme.Text;
            lv.Font = Theme.F(11.5f, false);
            lv.Columns.Add(L.T("文件"), Theme.P(470));
            lv.Columns.Add(L.T("涉及模组"), Theme.P(340));
            lv.Bounds = new Rectangle(Theme.P(20), Theme.P(46), Theme.P(820), Theme.P(440));
            lv.VirtualMode = true;
            lv.RetrieveVirtualItem += delegate(object s, RetrieveVirtualItemEventArgs e)
            {
                var g = groups[e.ItemIndex];
                var item = new ListViewItem(g.Path);
                item.SubItems.Add(string.Join("、", g.Mods.Select(x => Fmt.Short(x.Name, 16)).ToArray()));
                e.Item = item;
            };
            lv.VirtualListSize = groups.Count;
            Controls.Add(lv);

            var close = new FlatButton();
            close.Text = L.T("关闭");
            close.Bounds = new Rectangle(ClientSize.Width - Theme.P(120), ClientSize.Height - Theme.P(58), Theme.P(96), Theme.P(38));
            close.Click += delegate { Close(); };
            Controls.Add(close);
        }
    }
}

namespace GoHMM
{
    // 导出分享码
    public class ShareExportDialog : DarkForm
    {
        TextPanel box;
        FlatButton btnSource;
        string currentCode = "";
        string sourceName = L.T("当前加载顺序");
        readonly List<string> currentIds;
        readonly Dictionary<string, List<string>> presets;

        readonly Func<string, string> titleOf;

        public ShareExportDialog(string label, List<string> currentIds, Dictionary<string, List<string>> presets, Func<string, string> titleOf)
        {
            this.titleOf = titleOf;
            this.currentIds = currentIds;
            this.presets = presets;
            Text = L.T("导出分享码");
            ClientSize = new Size(Theme.P(660), Theme.P(430));

            var tip = MakeLabel(L.T("把这段发给朋友，他在「导入分享码」里粘贴，就能得到完全一样的加载顺序。"), Theme.P(22), Theme.P(16), Theme.P(600), true);
            tip.Font = Theme.F(11.5f, false);
            tip.ForeColor = Theme.Sub;

            btnSource = new FlatButton();
            btnSource.Text = L.T("来源：当前加载顺序 ▾");
            btnSource.Font = Theme.F(12, false);
            btnSource.Bounds = new Rectangle(Theme.P(22), Theme.P(48), Theme.P(250), Theme.P(34));
            btnSource.Click += delegate { ShowSourceMenu(); };
            Controls.Add(btnSource);

            box = new TextPanel();
            box.BackColor = Theme.PanelAlt;
            box.ForeColor = Theme.Text;
            box.Font = new Font("Consolas", Theme.S * 10.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            box.Bounds = new Rectangle(Theme.P(22), Theme.P(94), Theme.P(616), Theme.P(240));
            Controls.Add(box);

            var copy = new FlatButton();
            copy.Text = L.T("复制到剪贴板");
            copy.Accent = true;
            copy.Bounds = new Rectangle(Theme.P(22), Theme.P(350), Theme.P(150), Theme.P(40));
            copy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(currentCode);
                    copy.Text = L.T("已复制 ✓");
                }
                catch (Exception ex) { MessageBox.Show(this, L.T("复制失败：") + ex.Message); }
            };
            Controls.Add(copy);

            var close = new FlatButton();
            close.Text = L.T("关闭");
            close.Bounds = new Rectangle(ClientSize.Width - Theme.P(120), Theme.P(350), Theme.P(96), Theme.P(40));
            close.Click += delegate { Close(); };
            Controls.Add(close);

            RefreshCode();
        }

        void ShowSourceMenu()
        {
            var cm = new ContextMenuStrip();
            cm.BackColor = Theme.PanelAlt;
            cm.ForeColor = Theme.Text;
            cm.Font = Theme.F(12.5f, false);
            cm.Renderer = new DarkMenuRenderer();

            var miCur = new ToolStripMenuItem(L.T("当前加载顺序（") + currentIds.Count + L.T(" 个）"));
            miCur.Click += delegate { sourceName = L.T("当前加载顺序"); btnSource.Text = L.T("来源：当前加载顺序 ▾"); RefreshCode(); };
            cm.Items.Add(miCur);

            foreach (var kv in presets)
            {
                string name = kv.Key;
                string label = L.T("预设：") + name + "（" + kv.Value.Count + L.T(" 个）");
                var mi = new ToolStripMenuItem(label);
                mi.Click += delegate
                {
                    sourceName = name;
                    btnSource.Text = L.T("来源：预设「") + Fmt.Short(name, 10) + "」▾";
                    RefreshCode();
                };
                cm.Items.Add(mi);
            }
            cm.Show(btnSource, new Point(0, btnSource.Height + Theme.P(4)));
        }

        void RefreshCode()
        {
            List<string> ids = currentIds;
            string name = sourceName;
            List<string> presetIds;
            if (sourceName != L.T("当前加载顺序") && presets.TryGetValue(sourceName, out presetIds)) ids = presetIds;
            var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (titleOf != null)
            {
                foreach (var id in ids)
                {
                    string t = titleOf(id);
                    if (!string.IsNullOrEmpty(t)) titles[id] = t;
                }
            }
            currentCode = ShareCode.Encode(name, ids, titles);
            box.Value = currentCode;
        }
    }

    // 导入分享码
    public class ShareImportDialog : DarkForm
    {
        public SharePayload Payload;
        TextBox box;
        Label err;

        public ShareImportDialog()
        {
            Text = L.T("导入分享码");
            ClientSize = new Size(Theme.P(660), Theme.P(400));

            var tip = MakeLabel(L.T("把朋友发来的分享码粘贴到下面，然后点「导入」。"), Theme.P(22), Theme.P(16), Theme.P(600), true);
            tip.Font = Theme.F(11.5f, false);
            tip.ForeColor = Theme.Sub;

            box = new TextBox();
            box.Multiline = true;
            box.WordWrap = true;
            box.ScrollBars = ScrollBars.None;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Theme.PanelAlt;
            box.ForeColor = Theme.Text;
            box.Font = new Font("Consolas", Theme.S * 10f, FontStyle.Regular, GraphicsUnit.Pixel);
            box.Bounds = new Rectangle(Theme.P(22), Theme.P(52), Theme.P(616), Theme.P(240));
            Controls.Add(box);

            err = MakeLabel("", Theme.P(22), Theme.P(300), Theme.P(600), true);
            err.ForeColor = Theme.Danger;

            var ok = new FlatButton();
            ok.Text = L.T("导入");
            ok.Accent = true;
            ok.Bounds = new Rectangle(Theme.P(22), Theme.P(330), Theme.P(120), Theme.P(40));
            ok.Click += delegate { DoImport(); };
            Controls.Add(ok);

            var cancel = new FlatButton();
            cancel.Text = L.T("取消");
            cancel.Bounds = new Rectangle(ClientSize.Width - Theme.P(120), Theme.P(330), Theme.P(96), Theme.P(40));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            Shown += delegate { box.Focus(); };
        }

        void DoImport()
        {
            string error;
            var p = ShareCode.Decode(box.Text, out error);
            if (p == null)
            {
                err.Text = error;
                return;
            }
            Payload = p;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

namespace GoHMM
{
    public class MissingEntry
    {
        public string Id = "";
        public string Title = "";
        public string Display { get { return string.IsNullOrEmpty(Title) ? (L.T("模组 ") + Id) : Title; } }
    }

    // 导入分享码时，列出对方有、自己没订阅的模组
    public class MissingModsDialog : DarkForm
    {
        readonly List<MissingEntry> items;
        readonly ListBox list;
        readonly ScrollHost host;

        public static string WorkshopUrl(string id)
        {
            return "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id;
        }

        public static void OpenInSteam(string id)
        {
            try { Process.Start("steam://url/CommunityFilePage/" + id); }
            catch
            {
                try { Process.Start(WorkshopUrl(id)); } catch { }
            }
        }

        public MissingModsDialog(List<MissingEntry> items, string presetName)
        {
            this.items = items;
            Text = L.T("缺少 ") + items.Count + L.T(" 个模组");
            ClientSize = new Size(Theme.P(680), Theme.P(520));

            var head = MakeLabel(L.T("缺少 ") + items.Count + L.T(" 个模组"), Theme.P(22), Theme.P(14), Theme.P(400), false);
            head.Font = Theme.F(15, true);

            var tip = MakeLabel(L.T("这些模组你还没订阅，导入时已跳过。订阅并等 Steam 下完，按 F5 刷新后再导入一次分享码就能补齐。"), Theme.P(22), Theme.P(44), Theme.P(640), true);
            tip.Font = Theme.F(11.5f, false);
            tip.ForeColor = Theme.Sub;

            list = new ListBox();
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.PanelAlt;
            list.ForeColor = Theme.Text;
            list.IntegralHeight = false;
            list.HorizontalScrollbar = false;
            list.ItemHeight = Theme.P(56);
            list.Bounds = new Rectangle(Theme.P(8), Theme.P(78), Theme.P(664), Theme.P(330));
            list.DrawItem += new DrawItemEventHandler(DrawRow);
            list.DoubleClick += delegate { OpenSelected(); };
            foreach (var e in items) list.Items.Add(e);
            host = new ScrollHost(list);
            host.Bounds = new Rectangle(Theme.P(20), Theme.P(78), Theme.P(640), Theme.P(330));
            Controls.Add(host);

            var cm = new ContextMenuStrip();
            cm.BackColor = Theme.PanelAlt;
            cm.ForeColor = Theme.Text;
            cm.Font = Theme.F(12, false);
            cm.Renderer = new DarkMenuRenderer();
            var miOpen = new ToolStripMenuItem(L.T("在 Steam 中打开"));
            miOpen.Click += delegate { OpenSelected(); };
            cm.Items.Add(miOpen);
            var miWeb = new ToolStripMenuItem(L.T("在浏览器中打开"));
            miWeb.Click += delegate
            {
                int i = list.SelectedIndex;
                if (i < 0) return;
                try { Process.Start(WorkshopUrl(items[i].Id)); } catch { }
            };
            cm.Items.Add(miWeb);
            var miCopy = new ToolStripMenuItem(L.T("复制这个模组的链接"));
            miCopy.Click += delegate
            {
                int i = list.SelectedIndex;
                if (i < 0) return;
                try { Clipboard.SetText(items[i].Display + "  " + WorkshopUrl(items[i].Id)); } catch { }
            };
            cm.Items.Add(miCopy);
            list.ContextMenuStrip = cm;

            var bOpen = new FlatButton();
            bOpen.Text = L.T("打开创意工坊");
            bOpen.Accent = true;
            bOpen.Bounds = new Rectangle(Theme.P(20), Theme.P(424), Theme.P(150), Theme.P(40));
            bOpen.Click += delegate { OpenSelected(); };
            Controls.Add(bOpen);

            var bAll = new FlatButton();
            bAll.Text = L.T("全部打开");
            bAll.Bounds = new Rectangle(Theme.P(180), Theme.P(424), Theme.P(110), Theme.P(40));
            bAll.Click += delegate
            {
                if (MessageBox.Show(this, L.T("会一次性打开 ") + items.Count + L.T(" 个创意工坊页面，继续吗？"), L.T("全部打开"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                foreach (var e in items)
                {
                    OpenInSteam(e.Id);
                    System.Threading.Thread.Sleep(400);
                }
            };
            Controls.Add(bAll);

            var bCopy = new FlatButton();
            bCopy.Text = L.T("复制清单（带链接）");
            bCopy.Bounds = new Rectangle(Theme.P(300), Theme.P(424), Theme.P(200), Theme.P(40));
            bCopy.Click += delegate
            {
                var sb = new StringBuilder();
                sb.AppendLine(L.T("我还缺这些模组（共 ") + items.Count + L.T(" 个），麻烦发一下链接或我订阅后重试："));
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append(i + 1).Append(". ").Append(items[i].Display).Append("  ").Append(WorkshopUrl(items[i].Id)).AppendLine();
                }
                try
                {
                    Clipboard.SetText(sb.ToString());
                    bCopy.Text = L.T("已复制 ✓");
                }
                catch (Exception ex) { MessageBox.Show(this, L.T("复制失败：") + ex.Message); }
            };
            Controls.Add(bCopy);

            var bClose = new FlatButton();
            bClose.Text = L.T("关闭");
            bClose.Bounds = new Rectangle(ClientSize.Width - Theme.P(120), Theme.P(424), Theme.P(96), Theme.P(40));
            bClose.Click += delegate { Close(); };
            Controls.Add(bClose);

            if (!string.IsNullOrEmpty(presetName))
            {
                var nameLabel = MakeLabel(L.T("分享码：") + presetName, Theme.P(22), Theme.P(64), Theme.P(400), true);
                nameLabel.ForeColor = Theme.Dim;
                nameLabel.Font = Theme.F(10.5f, false);
            }
        }

        void DrawRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var it = (MissingEntry)list.Items[e.Index];
            var g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var b = new SolidBrush(sel ? Theme.PanelHover : Theme.PanelAlt))
                g.FillRectangle(b, e.Bounds);

            Theme.RenderText(g, it.Display, Theme.F(13, true), Theme.Text,
                new Rectangle(e.Bounds.X + Theme.P(12), e.Bounds.Y + Theme.P(6), e.Bounds.Width - Theme.P(24), Theme.P(20)), StringAlignment.Near);
            Theme.RenderText(g, L.T("创意工坊 ID ") + it.Id + L.T("   ·   双击打开订阅页面"), Theme.F(11, false), Theme.Dim,
                new Rectangle(e.Bounds.X + Theme.P(12), e.Bounds.Y + Theme.P(28), e.Bounds.Width - Theme.P(24), Theme.P(18)), StringAlignment.Near);
        }

        void OpenSelected()
        {
            int i = list.SelectedIndex;
            if (i < 0)
            {
                MessageBox.Show(this, L.T("先在列表里选一个模组。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenInSteam(items[i].Id);
        }
    }
}
