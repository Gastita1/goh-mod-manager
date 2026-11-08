using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace GoHMM
{
    internal static class Gdi
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        [System.Runtime.InteropServices.DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool BitBlt(IntPtr hdcDest, int x, int y, int w, int h, IntPtr hdcSrc, int sx, int sy, uint rop);

        public const uint SRCCOPY = 0x00CC0020;

        public static IntPtr MemDC;
        public static IntPtr EnsureMemDC()
        {
            if (MemDC == IntPtr.Zero) MemDC = CreateCompatibleDC(IntPtr.Zero);
            return MemDC;
        }
    }

    public class MainForm : Form
    {
        AppConfig cfg = new AppConfig();
        Paths paths = new Paths();
        List<ModEntry> catalog = new List<ModEntry>();
        List<ModEntry> order = new List<ModEntry>();
        Dictionary<string, ModEntry> bySetId = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> conflictMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<ConflictGroup> conflicts = new List<ConflictGroup>();

        string filter = L.T("全部");
        string search = "";
        bool dirty;
        int dragIndex = -1;
        bool dragging;
        Point dragStart;
        int hoverIndex = -1;

        // 顶部
        Label lblTitle, lblSubtitle;
        FlatButton btnRefresh, btnLaunch, btnMenu, btnLang;

        // 左
        Card leftCard;
        Label lblCatalog, lblCatalogCount;
        Panel searchBox;
        TextBox txtSearch;
        Chip chipAll, chipOn, chipShop, chipLocal, chipIssue;
        ListBox listCatalog;
        ScrollHost hostCatalog;

        // 右
        Card rightCard;
        Label lblOrderTitle, lblOrderHint;
        FlatButton btnUp, btnDown, btnClear;
        ListBox listOrder;
        ScrollHost hostOrder;
        Card detailCard;
        PictureBox picCover;
        Label lblDetName, lblDetMeta, lblDetAuthor, lblDetTags, lblDescCaption;
        int infoToken;
        static readonly HashSet<string> fetchTried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Image> thumbCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        Image emptyTile;
        TextPanel descPanel;
        FlatButton btnDetailAction, btnDetailFolder;

        // 底部
        Label lblStatus;
        FlatButton btnPreset;
        FlatButton btnBackup, btnApply;

        public MainForm()
        {
            L.LoadTable();
            var boot = AppConfig.Load();
            L.Code = string.IsNullOrEmpty(boot.Language) ? L.Detect() : boot.Language;

            using (var g = CreateGraphics()) Theme.S = g.DpiX / 96f;
            if (Theme.S <= 0) Theme.S = 1f;

            Text = L.T("地狱之门 · 模组管理器");
            BackColor = Theme.Bg;
            Font = Theme.F(13, false);
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(Theme.P(1000), Theme.P(640));

            int wantW = boot.WinW > 0 ? boot.WinW : 1280;
            int wantH = boot.WinH > 0 ? boot.WinH : 840;
            var wa = Screen.PrimaryScreen.WorkingArea;
            int maxW = wa.Width - Theme.P(16);
            int maxH = wa.Height - Theme.P(16);
            ClientSize = new Size(Math.Min(Theme.P(wantW), maxW), Math.Min(Theme.P(wantH), maxH));
            AllowDrop = true;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();
            Resize += delegate { LayoutUi(); };
            Shown += delegate { LayoutUi(); LoadAll(); };
            DragEnter += new DragEventHandler(OnDragEnter);
            DragDrop += new DragEventHandler(OnDragDrop);
            KeyPreview = true;
            KeyDown += new KeyEventHandler(OnKeyDown);
        }

        // ------------------------------------------------------------------
        // 构建控件
        // ------------------------------------------------------------------
        void BuildUi()
        {
            Text = L.T("地狱之门 · 模组管理器");
            lblTitle = new Label();
            lblTitle.Text = L.T("地狱之门 · 模组管理器");
            lblTitle.Font = Theme.F(21, true);
            lblTitle.ForeColor = Theme.Text;
            lblTitle.AutoSize = false;
            lblTitle.BackColor = Theme.Bg;
            Controls.Add(lblTitle);

            lblSubtitle = new Label();
            lblSubtitle.Text = L.T("正在检测游戏目录…");
            lblSubtitle.Font = Theme.F(11.5f, false);
            lblSubtitle.ForeColor = Theme.Dim;
            lblSubtitle.AutoSize = false;
            lblSubtitle.BackColor = Theme.Bg;
            Controls.Add(lblSubtitle);

            btnRefresh = new FlatButton();
            btnRefresh.Text = L.T("刷新");
            btnRefresh.Ghost = true;
            btnRefresh.Click += delegate { LoadAll(); };
            Controls.Add(btnRefresh);

            btnLaunch = new FlatButton();
            btnLaunch.Text = L.T("启动游戏");
            btnLaunch.Accent = true;
            btnLaunch.Click += delegate { LaunchGame(); };
            Controls.Add(btnLaunch);

            btnLang = new FlatButton();
            btnLang.Text = L.ShortName(L.Code) + " ▾";
            btnLang.Font = Theme.F(12, false);
            btnLang.Click += delegate { ShowLanguageMenu(); };
            Controls.Add(btnLang);

            btnMenu = new FlatButton();
            btnMenu.Text = "⋯";
            btnMenu.Ghost = true;
            btnMenu.Font = Theme.F(17, true);
            btnMenu.Click += delegate { ShowMenu(); };
            Controls.Add(btnMenu);

            // ---------------- 左侧目录 ----------------
            leftCard = new Card();
            Controls.Add(leftCard);
            leftCard.Resize += delegate { LayoutLeft(); };

            lblCatalog = new Label();
            lblCatalog.Text = L.T("目录");
            lblCatalog.Font = Theme.F(16, true);
            lblCatalog.ForeColor = Theme.Text;
            lblCatalog.AutoSize = false;
            lblCatalog.BackColor = Theme.Panel;
            leftCard.Controls.Add(lblCatalog);

            lblCatalogCount = new Label();
            lblCatalogCount.Text = "";
            lblCatalogCount.Font = Theme.F(11.5f, false);
            lblCatalogCount.ForeColor = Theme.Dim;
            lblCatalogCount.AutoSize = false;
            lblCatalogCount.TextAlign = ContentAlignment.MiddleRight;
            lblCatalogCount.BackColor = Theme.Panel;
            leftCard.Controls.Add(lblCatalogCount);

            searchBox = new Panel();
            searchBox.BackColor = Theme.PanelAlt;
            searchBox.Paint += new PaintEventHandler(searchBox_Paint);
            leftCard.Controls.Add(searchBox);

            txtSearch = new TextBox();
            txtSearch.BorderStyle = BorderStyle.None;
            txtSearch.BackColor = Theme.PanelAlt;
            txtSearch.ForeColor = Theme.Text;
            txtSearch.Font = Theme.F(13, false);
            txtSearch.TextChanged += delegate { search = txtSearch.Text.Trim(); RefreshCatalogList(); };
            searchBox.Controls.Add(txtSearch);

            chipAll = MakeChip(L.T("全部"), true);
            chipOn = MakeChip(L.T("已启用"), false);
            chipShop = MakeChip(L.T("创意工坊"), false);
            chipLocal = MakeChip(L.T("本地"), false);
            chipIssue = MakeChip(L.T("有问题"), false);

            listCatalog = new ListBox();
            listCatalog.DrawMode = DrawMode.OwnerDrawFixed;
            listCatalog.BorderStyle = BorderStyle.None;
            listCatalog.BackColor = Theme.Panel;
            listCatalog.ForeColor = Theme.Text;
            listCatalog.IntegralHeight = false;
            listCatalog.HorizontalScrollbar = false;
            listCatalog.ItemHeight = Theme.P(66);
            listCatalog.DrawItem += new DrawItemEventHandler(listCatalog_DrawItem);
            listCatalog.SelectedIndexChanged += delegate { UpdateDetails(); };
            listCatalog.MouseDown += new MouseEventHandler(listCatalog_MouseDown);
            listCatalog.DoubleClick += delegate { ToggleSelectedCatalog(); };
            listCatalog.MouseMove += new MouseEventHandler(listCatalog_MouseMove);
            listCatalog.MouseLeave += delegate { hoverIndex = -1; listCatalog.Invalidate(); };
            var cmCatalog = BuildCatalogMenu();
            listCatalog.ContextMenuStrip = cmCatalog;
            listCatalog.MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right) { int i = listCatalog.IndexFromPoint(e.Location); if (i >= 0) listCatalog.SelectedIndex = i; } };
            EnableDoubleBuffer(listCatalog);
            hostCatalog = new ScrollHost(listCatalog);
            leftCard.Controls.Add(hostCatalog);

            // ---------------- 右侧加载顺序 ----------------
            rightCard = new Card();
            Controls.Add(rightCard);
            rightCard.Resize += delegate { LayoutRight(); };

            lblOrderTitle = new Label();
            lblOrderTitle.Text = L.T("加载顺序");
            lblOrderTitle.Font = Theme.F(16, true);
            lblOrderTitle.ForeColor = Theme.Text;
            lblOrderTitle.AutoSize = false;
            lblOrderTitle.BackColor = Theme.Panel;
            rightCard.Controls.Add(lblOrderTitle);

            lblOrderHint = new Label();
            lblOrderHint.Text = L.T("从上到下依次加载 · 越靠下的模组覆盖越靠上的");
            lblOrderHint.Font = Theme.F(11, false);
            lblOrderHint.ForeColor = Theme.Dim;
            lblOrderHint.AutoSize = false;
            lblOrderHint.BackColor = Theme.Panel;
            rightCard.Controls.Add(lblOrderHint);

            btnUp = new FlatButton();
            btnUp.Text = L.T("上移");
            btnUp.Click += delegate { MoveSelected(-1); };
            rightCard.Controls.Add(btnUp);

            btnDown = new FlatButton();
            btnDown.Text = L.T("下移");
            btnDown.Click += delegate { MoveSelected(1); };
            rightCard.Controls.Add(btnDown);

            btnClear = new FlatButton();
            btnClear.Text = L.T("清空");
            btnClear.Click += delegate { ClearOrder(); };
            rightCard.Controls.Add(btnClear);

            listOrder = new ListBox();
            listOrder.DrawMode = DrawMode.OwnerDrawFixed;
            listOrder.BorderStyle = BorderStyle.None;
            listOrder.BackColor = Theme.Panel;
            listOrder.ForeColor = Theme.Text;
            listOrder.IntegralHeight = false;
            listOrder.HorizontalScrollbar = false;
            listOrder.ItemHeight = Theme.P(48);
            listOrder.DrawItem += new DrawItemEventHandler(listOrder_DrawItem);
            listOrder.SelectedIndexChanged += delegate { UpdateDetails(); };
            listOrder.MouseDown += new MouseEventHandler(listOrder_MouseDown);
            listOrder.MouseMove += new MouseEventHandler(listOrder_MouseMove);
            listOrder.MouseUp += delegate { dragging = false; dragIndex = -1; listOrder.Invalidate(); };
            listOrder.DoubleClick += delegate { OpenSelectedFolder(); };
            listOrder.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) { RemoveSelectedOrder(); e.Handled = true; }
                else if (e.KeyCode == Keys.Up && e.Control) { MoveSelected(-1); e.Handled = true; }
                else if (e.KeyCode == Keys.Down && e.Control) { MoveSelected(1); e.Handled = true; }
            };
            var cmOrder = BuildOrderMenu();
            listOrder.ContextMenuStrip = cmOrder;
            listOrder.MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right) { int i = listOrder.IndexFromPoint(e.Location); if (i >= 0) listOrder.SelectedIndex = i; } };
            EnableDoubleBuffer(listOrder);
            hostOrder = new ScrollHost(listOrder);
            rightCard.Controls.Add(hostOrder);

            // 详情
            detailCard = new Card();
            detailCard.Fill = Theme.PanelAlt;
            detailCard.Edge = Theme.BorderSoft;
            detailCard.Soft = true;
            rightCard.Controls.Add(detailCard);
            detailCard.Resize += delegate { LayoutDetail(); };

            picCover = new PictureBox();
            picCover.SizeMode = PictureBoxSizeMode.Zoom;
            picCover.BackColor = Theme.Panel;
            detailCard.Controls.Add(picCover);

            lblDetName = new Label();
            lblDetName.Text = "";
            lblDetName.Font = Theme.F(14.5f, true);
            lblDetName.ForeColor = Theme.Text;
            lblDetName.AutoSize = false;
            lblDetName.BackColor = Theme.PanelAlt;
            detailCard.Controls.Add(lblDetName);

            lblDetMeta = new Label();
            lblDetMeta.Text = "";
            lblDetMeta.Font = Theme.F(11.5f, false);
            lblDetMeta.ForeColor = Theme.Sub;
            lblDetMeta.AutoSize = false;
            lblDetMeta.BackColor = Theme.PanelAlt;
            detailCard.Controls.Add(lblDetMeta);

            lblDetAuthor = new Label();
            lblDetAuthor.Text = "";
            lblDetAuthor.Font = Theme.F(11.5f, false);
            lblDetAuthor.ForeColor = Theme.Text;
            lblDetAuthor.AutoSize = false;
            lblDetAuthor.BackColor = Theme.PanelAlt;
            detailCard.Controls.Add(lblDetAuthor);

            lblDescCaption = new Label();
            lblDescCaption.Text = L.T("简介");
            lblDescCaption.Font = Theme.F(11, true);
            lblDescCaption.ForeColor = Theme.Dim;
            lblDescCaption.AutoSize = false;
            lblDescCaption.BackColor = Theme.PanelAlt;
            detailCard.Controls.Add(lblDescCaption);

            lblDetTags = new Label();
            lblDetTags.Text = "";
            lblDetTags.Font = Theme.F(11.5f, false);
            lblDetTags.ForeColor = Theme.Warn;
            lblDetTags.AutoSize = false;
            lblDetTags.BackColor = Theme.PanelAlt;
            detailCard.Controls.Add(lblDetTags);

            descPanel = new TextPanel();
            descPanel.BackColor = Theme.PanelAlt;
            descPanel.ForeColor = Theme.Sub;
            descPanel.Font = Theme.F(12.5f, false);
            detailCard.Controls.Add(descPanel);

            btnDetailAction = new FlatButton();
            btnDetailAction.Text = L.T("加入加载顺序");
            btnDetailAction.Font = Theme.F(12, false);
            btnDetailAction.Click += delegate { ToggleSelectedCatalog(); };
            detailCard.Controls.Add(btnDetailAction);

            btnDetailFolder = new FlatButton();
            btnDetailFolder.Text = L.T("打开文件夹");
            btnDetailFolder.Font = Theme.F(12, false);
            btnDetailFolder.Ghost = false;
            btnDetailFolder.BackNormal = Theme.PanelAlt;
            btnDetailFolder.BackHover = Theme.PanelHover;
            btnDetailFolder.Click += delegate { OpenSelectedFolder(); };
            detailCard.Controls.Add(btnDetailFolder);

            // ---------------- 底部 ----------------
            lblStatus = new Label();
            lblStatus.Text = "";
            lblStatus.Font = Theme.F(12, false);
            lblStatus.ForeColor = Theme.Sub;
            lblStatus.AutoSize = false;
            lblStatus.BackColor = Theme.Bg;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lblStatus);

            btnPreset = new FlatButton();
            btnPreset.Text = L.T("预设 ▾");
            btnPreset.Font = Theme.F(12, false);
            btnPreset.Click += delegate { ShowPresetMenu(); };
            Controls.Add(btnPreset);

            btnBackup = new FlatButton();
            btnBackup.Text = L.T("备份与还原");
            btnBackup.Font = Theme.F(12, false);
            btnBackup.Click += delegate { ShowBackups(); };
            Controls.Add(btnBackup);

            btnApply = new FlatButton();
            btnApply.Text = L.T("保存并应用");
            btnApply.Accent = true;
            btnApply.Font = Theme.F(13.5f, true);
            btnApply.Click += delegate { SaveApply(); };
            Controls.Add(btnApply);
        }

        static void EnableDoubleBuffer(Control c)
        {
            try
            {
                var pi = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(c, true, null);
            }
            catch { }
        }

        Image GetThumb(ModEntry m, int size, bool tileFallback)
        {
            string key = (m.Dir ?? m.Id) + "|" + size;
            Image img;
            if (thumbCache.TryGetValue(key, out img)) return img;
            if (!string.IsNullOrEmpty(m.Thumb)) img = Draw.Thumb(m.Thumb, size);
            if (img == null && tileFallback) img = Draw.Tile(m.Name, size);
            if (img != null) thumbCache[key] = img;
            return img;
        }

        Image EmptyTile()
        {
            if (emptyTile == null) emptyTile = Draw.Tile("?", Theme.P(62));
            return emptyTile;
        }

        Chip MakeChip(string key, bool first)
        {
            var c = new Chip();
            c.Text = L.T(key);
            c.Tag = key;
            c.Checked = first;
            c.Click += delegate { SetFilter(key); };
            leftCard.Controls.Add(c);
            return c;
        }

        ContextMenuStrip BuildCatalogMenu()
        {
            var cm = NewMenu();
            var miAdd = new ToolStripMenuItem(L.T("加入加载顺序"));
            miAdd.Click += delegate { ToggleSelectedCatalog(); };
            cm.Items.Add(miAdd);
            var miFolder = new ToolStripMenuItem(L.T("打开文件夹"));
            miFolder.Click += delegate { OpenSelectedFolder(); };
            cm.Items.Add(miFolder);
            var miShop = new ToolStripMenuItem(L.T("打开创意工坊页面"));
            miShop.Click += delegate { OpenWorkshopPage(); };
            cm.Items.Add(miShop);
            var miInfo = new ToolStripMenuItem(L.T("联网获取作者 / 简介"));
            miInfo.Click += delegate
            {
                var mm = SelectedCatalogMod();
                if (mm == null || !mm.IsWorkshop)
                {
                    MessageBox.Show(this, L.T("只有创意工坊模组才能联网获取作者信息。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                fetchTried.Remove(mm.Id);
                lblDetAuthor.Text = L.T("作者：正在联网读取…");
                lblDetAuthor.ForeColor = Theme.Dim;
                QueueWorkshopInfo(mm);
            };
            cm.Items.Add(miInfo);

            var miCopy = new ToolStripMenuItem(L.T("复制模组 ID"));
            miCopy.Click += delegate
            {
                var m = SelectedCatalogMod();
                if (m != null) Clipboard.SetText(m.Id);
            };
            cm.Items.Add(miCopy);
            return cm;
        }

        ContextMenuStrip BuildOrderMenu()
        {
            var cm = NewMenu();
            var miUp = new ToolStripMenuItem(L.T("上移（Ctrl+↑）"));
            miUp.Click += delegate { MoveSelected(-1); };
            cm.Items.Add(miUp);
            var miDown = new ToolStripMenuItem(L.T("下移（Ctrl+↓）"));
            miDown.Click += delegate { MoveSelected(1); };
            cm.Items.Add(miDown);
            cm.Items.Add(new ToolStripSeparator());
            var miTop = new ToolStripMenuItem(L.T("移到最上"));
            miTop.Click += delegate { MoveTop(); };
            cm.Items.Add(miTop);
            var miBottom = new ToolStripMenuItem(L.T("移到最下"));
            miBottom.Click += delegate { MoveBottom(); };
            cm.Items.Add(miBottom);
            cm.Items.Add(new ToolStripSeparator());
            var miRemove = new ToolStripMenuItem(L.T("从加载顺序移除（Del）"));
            miRemove.Click += delegate { RemoveSelectedOrder(); };
            cm.Items.Add(miRemove);
            var miFolder = new ToolStripMenuItem(L.T("打开文件夹"));
            miFolder.Click += delegate { OpenSelectedFolder(); };
            cm.Items.Add(miFolder);
            return cm;
        }

        ContextMenuStrip NewMenu()
        {
            var cm = new ContextMenuStrip();
            cm.BackColor = Theme.PanelAlt;
            cm.ForeColor = Theme.Text;
            cm.Renderer = new DarkMenuRenderer();
            cm.Font = Theme.F(12.5f, false);
            return cm;
        }

        // ------------------------------------------------------------------
        // 布局
        // ------------------------------------------------------------------
        void LayoutUi()
        {
            if (lblTitle == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            int pad = Theme.P(22);
            int headerH = Theme.P(84);

            lblTitle.Bounds = new Rectangle(pad, Theme.P(16), Theme.P(520), Theme.P(36));
            lblSubtitle.Bounds = new Rectangle(pad + Theme.P(1), Theme.P(54), Theme.P(700), Theme.P(22));

            int bx = w - pad - Theme.P(36);
            btnMenu.Bounds = new Rectangle(bx, Theme.P(22), Theme.P(36), Theme.P(38));
            bx -= Theme.P(8) + Theme.P(86);
            btnLang.Bounds = new Rectangle(bx, Theme.P(22), Theme.P(86), Theme.P(38));
            bx -= Theme.P(10) + Theme.P(112);
            btnLaunch.Bounds = new Rectangle(bx, Theme.P(22), Theme.P(112), Theme.P(38));
            bx -= Theme.P(8) + Theme.P(64);
            btnRefresh.Bounds = new Rectangle(bx, Theme.P(22), Theme.P(64), Theme.P(38));

            int bodyTop = headerH;
            int bodyBottom = h - Theme.P(84);
            int bodyH = bodyBottom - bodyTop;
            if (bodyH < Theme.P(300)) bodyH = Theme.P(300);

            int gap = Theme.P(16);
            int leftW = (int)((w - pad * 2 - gap) * 0.55);
            int rightW = w - pad * 2 - gap - leftW;

            leftCard.Bounds = new Rectangle(pad, bodyTop, leftW, bodyH);
            rightCard.Bounds = new Rectangle(pad + leftW + gap, bodyTop, rightW, bodyH);

            // 底部
            int by = h - Theme.P(66);
            int rightEdge = w - pad;
            btnApply.Bounds = new Rectangle(rightEdge - Theme.P(150), by, Theme.P(150), Theme.P(42));
            rightEdge -= Theme.P(150) + Theme.P(10);
            btnBackup.Bounds = new Rectangle(rightEdge - Theme.P(110), by + Theme.P(3), Theme.P(110), Theme.P(36));
            rightEdge -= Theme.P(110) + Theme.P(10);
            btnPreset.Bounds = new Rectangle(rightEdge - Theme.P(190), by + Theme.P(3), Theme.P(190), Theme.P(36));
            rightEdge -= Theme.P(190) + Theme.P(12);

            lblStatus.Bounds = new Rectangle(pad, by, Math.Max(Theme.P(120), rightEdge - pad), Theme.P(48));

            LayoutLeft();
            LayoutRight();
        }

        void LayoutLeft()
        {
            if (lblCatalog == null) return;
            int w = leftCard.Width, h = leftCard.Height;
            int pad = Theme.P(22);

            lblCatalog.Bounds = new Rectangle(pad, Theme.P(18), Theme.P(120), Theme.P(26));
            lblCatalogCount.Bounds = new Rectangle(w - pad - Theme.P(220), Theme.P(22), Theme.P(220), Theme.P(22));

            searchBox.Bounds = new Rectangle(pad, Theme.P(54), w - pad * 2, Theme.P(38));
            txtSearch.Bounds = new Rectangle(Theme.P(38), Theme.P(10), searchBox.Width - Theme.P(52), Theme.P(20));

            int cx = pad, cy = Theme.P(104);
            Chip[] chips = { chipAll, chipOn, chipShop, chipLocal, chipIssue };
            foreach (var c in chips)
            {
                int cw = TextRenderer.MeasureText(c.Text, c.Font).Width + Theme.P(26);
                c.Bounds = new Rectangle(cx, cy, cw, Theme.P(30));
                cx += cw + Theme.P(8);
            }

            int listTop = cy + Theme.P(40);
            hostCatalog.Bounds = new Rectangle(Theme.P(10), listTop, w - Theme.P(20), h - listTop - Theme.P(10));
            listCatalog.ItemHeight = Theme.P(66);
        }

        void LayoutRight()
        {
            if (lblOrderTitle == null) return;
            int w = rightCard.Width, h = rightCard.Height;
            int pad = Theme.P(22);

            lblOrderTitle.Bounds = new Rectangle(pad, Theme.P(18), Theme.P(150), Theme.P(26));
            lblOrderHint.Bounds = new Rectangle(pad, Theme.P(44), w - pad * 2, Theme.P(20));

            int bx = w - pad - Theme.P(58);
            btnClear.Bounds = new Rectangle(bx, Theme.P(20), Theme.P(58), Theme.P(32));
            bx -= Theme.P(6) + Theme.P(58);
            btnDown.Bounds = new Rectangle(bx, Theme.P(20), Theme.P(58), Theme.P(32));
            bx -= Theme.P(6) + Theme.P(58);
            btnUp.Bounds = new Rectangle(bx, Theme.P(20), Theme.P(58), Theme.P(32));

            int listTop = Theme.P(76);
            int detailH = Math.Min(Theme.P(330), Math.Max(Theme.P(240), (int)(h * 0.42f)));
            int listH = h - listTop - detailH - Theme.P(24);
            if (listH < Theme.P(120)) listH = Theme.P(120);

            hostOrder.Bounds = new Rectangle(Theme.P(10), listTop, w - Theme.P(20), listH);
            listOrder.ItemHeight = Theme.P(46);

            detailCard.Bounds = new Rectangle(Theme.P(10), listTop + listH + Theme.P(10), w - Theme.P(20), detailH);
            LayoutDetail();
        }

        void LayoutDetail()
        {
            if (lblDetName == null) return;
            int w = detailCard.Width, h = detailCard.Height;
            int pad = Theme.P(14);

            int cover = Theme.P(56);
            picCover.Bounds = new Rectangle(pad, pad, cover, cover);

            int tx = pad + cover + Theme.P(12);
            lblDetName.Bounds = new Rectangle(tx, pad - Theme.P(2), w - tx - pad, Theme.P(22));
            lblDetMeta.Bounds = new Rectangle(tx, pad + Theme.P(19), w - tx - pad, Theme.P(18));
            lblDetAuthor.Bounds = new Rectangle(tx, pad + Theme.P(37), w - tx - pad, Theme.P(18));

            lblDetTags.Bounds = new Rectangle(pad, pad + cover + Theme.P(2), w - pad * 2, Theme.P(18));

            int btnY = pad + cover + Theme.P(24);
            btnDetailAction.Bounds = new Rectangle(pad, btnY, Theme.P(112), Theme.P(30));
            btnDetailFolder.Bounds = new Rectangle(pad + Theme.P(118), btnY, Theme.P(96), Theme.P(30));

            lblDescCaption.Bounds = new Rectangle(pad, btnY + Theme.P(34), w - pad * 2, Theme.P(16));
            int descTop = btnY + Theme.P(52);
            descPanel.Bounds = new Rectangle(pad, descTop, w - pad * 2, Math.Max(Theme.P(30), h - descTop - pad));
        }

        // ------------------------------------------------------------------
        // 数据加载
        // ------------------------------------------------------------------
        void LoadAll()
        {
            try
            {
                new FileInfo(AppConfig.FilePath).Directory.Create();
                if (!File.Exists(AppConfig.FilePath))
                {
                    var probe = new AppConfig();
                    if (probe.TryImportLegacy())
                    {
                        probe.Save();
                        AppConfig.Log("已从旧版管理器导入配置");
                    }
                }
                cfg = AppConfig.Load();
                paths = Paths.Discover(cfg);
                if (!string.IsNullOrEmpty(paths.GameDir)) cfg.GamePath = paths.GameDir;
                if (!string.IsNullOrEmpty(paths.WorkshopDir)) cfg.WorkshopPath = paths.WorkshopDir;
                if (!string.IsNullOrEmpty(paths.ProfileFile)) cfg.ProfileFile = paths.ProfileFile;
                cfg.Save();

                if (picCover != null) picCover.Image = null;
                foreach (var img in thumbCache.Values) img.Dispose();
                thumbCache.Clear();
                ConflictScanner.ClearCache();
                catalog = Catalog.Scan(paths.WorkshopDir, paths.GameDir, paths.GameVersion);
                bySetId = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in catalog) bySetId[m.SetId] = m;

                order.Clear();
                foreach (var id in OptionsFile.ReadActive(paths.ProfileFile))
                {
                    ModEntry m;
                    if (bySetId.TryGetValue(id, out m))
                    {
                        if (!order.Contains(m)) order.Add(m);
                    }
                    else
                    {
                        var miss = new ModEntry();
                        miss.Id = id.StartsWith("mod_") ? id.Substring(4) : id;
                        miss.Folder = miss.Id;
                        miss.Name = miss.Id;
                        miss.BrokenReason = L.T("该模组不在创意工坊或本地目录中");
                        miss.Issues.Add(L.T("缺失"));
                        bySetId[id] = miss;
                        catalog.Add(miss);
                        order.Add(miss);
                    }
                }

                dirty = false;
                Recalculate();
                RefreshPresets();
                RefreshCatalogList();
                RefreshOrderList();
                UpdateStatus();
                UpdateDetails();
                UpdateSubtitle();
            }
            catch (Exception ex)
            {
                AppConfig.Log("加载失败: " + ex);
                MessageBox.Show(this, L.T("载入失败：\n") + ex.Message, L.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void UpdateSubtitle()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(paths.GameDir)) sb.Append(paths.GameDir);
            else sb.Append(L.T("未找到游戏目录"));
            if (!string.IsNullOrEmpty(paths.GameVersion)) sb.Append(L.T("    ·    游戏版本 ")).Append(paths.GameVersion);
            if (string.IsNullOrEmpty(paths.ProfileFile)) sb.Append(L.T("    ·    ⚠ 未找到存档配置 (options.set)"));
            lblSubtitle.Text = sb.ToString();
            lblSubtitle.ForeColor = string.IsNullOrEmpty(paths.ProfileFile) ? Theme.Warn : Theme.Dim;
        }

        void Recalculate()
        {
            int total;
            conflicts = ConflictScanner.Scan(order, out total);
            conflictMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in conflicts)
                foreach (var m in g.Mods)
                {
                    int c;
                    conflictMap.TryGetValue(m.SetId, out c);
                    conflictMap[m.SetId] = c + 1;
                }

            foreach (var m in catalog)
            {
                if (m.Broken) continue;
                m.Issues.Clear();
                if (!ModInfoReader.VersionInRange(m.MinVersion, m.MaxVersion, paths.GameVersion))
                    m.Issues.Add(L.T("版本不兼容"));
                int cc;
                if (conflictMap.TryGetValue(m.SetId, out cc) && cc > 0)
                    m.Issues.Add(cc + L.T(" 处文件冲突"));
                if (!m.Broken)
                {
                    foreach (var req in m.Requires)
                    {
                        var dep = catalog.FirstOrDefault(x =>
                            string.Equals(x.Folder, req, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(x.Name, req, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(x.Id, req, StringComparison.OrdinalIgnoreCase));
                        if (dep == null) m.Issues.Add(L.T("缺少依赖：") + req);
                        else if (!order.Contains(dep)) m.Issues.Add(L.T("依赖未启用：") + req);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // 列表绘制
        // ------------------------------------------------------------------
        bool IsEnabled(ModEntry m) { return order.Contains(m); }

        readonly Dictionary<string, Bitmap> rowCache = new Dictionary<string, Bitmap>();
        readonly Dictionary<string, IntPtr> rowHbitmap = new Dictionary<string, IntPtr>();
        int rowCacheW = -1, rowCacheH = -1;

        void ClearRowCache()
        {
            foreach (var b in rowCache.Values) b.Dispose();
            rowCache.Clear();
            foreach (var h in rowHbitmap.Values) Gdi.DeleteObject(h);
            rowHbitmap.Clear();
        }

        string RowKey(ModEntry m, int index, bool sel, bool hov)
        {
            return index + "|" + (IsEnabled(m) ? "1" : "0") + "|" + m.Issues.Count + "|" + m.Broken
                + "|" + (sel ? "s" : (hov ? "h" : "n"));
        }

        Bitmap GetRowBitmap(ModEntry m, int index, int w, int h, bool sel, bool hov)
        {
            if (w <= 0 || h <= 0) return null;
            if (w != rowCacheW || h != rowCacheH)
            {
                ClearRowCache();
                rowCacheW = w;
                rowCacheH = h;
            }
            string key = RowKey(m, index, sel, hov);
            Bitmap cached;
            if (rowCache.TryGetValue(key, out cached)) return cached;

            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var gg = Graphics.FromImage(bmp))
            {
                gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var bg = new SolidBrush(Theme.Panel)) gg.FillRectangle(bg, 0, 0, w, h);
                DrawRowCore(gg, m, new Rectangle(0, 0, w, h), index, sel, hov && !sel);
            }
            rowCache[key] = bmp;
            rowHbitmap[key] = bmp.GetHbitmap(Theme.Panel);
            return bmp;
        }


        void listCatalog_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= listCatalog.Items.Count) return;
            var m = listCatalog.Items[e.Index] as ModEntry;
            if (m == null) return;

            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool hov = e.Index == hoverIndex;
            int w = listCatalog.ClientSize.Width - Theme.P(20);

            var bmp = GetRowBitmap(m, e.Index, w, listCatalog.ItemHeight, sel, hov);
            if (bmp != null)
            {
                IntPtr hbm;
                if (rowHbitmap.TryGetValue(RowKey(m, e.Index, sel, hov), out hbm) && hbm != IntPtr.Zero)
                {
                    IntPtr hdc = e.Graphics.GetHdc();
                    try
                    {
                        IntPtr mem = Gdi.EnsureMemDC();
                        IntPtr oldObj = Gdi.SelectObject(mem, hbm);
                        Gdi.BitBlt(hdc, e.Bounds.X, e.Bounds.Y, w, e.Bounds.Height, mem, 0, 0, Gdi.SRCCOPY);
                        Gdi.SelectObject(mem, oldObj);
                    }
                    finally { e.Graphics.ReleaseHdc(hdc); }
                }
                else e.Graphics.DrawImageUnscaled(bmp, e.Bounds.X, e.Bounds.Y);
                return;
            }
            DrawRowCore(e.Graphics, m, e.Bounds, e.Index, sel, hov);
        }

        // 真正的行绘制（缓存和直画共用）
        void DrawRowCore(Graphics g, ModEntry m, Rectangle bounds, int index, bool sel, bool hov)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int w = bounds.Width;

            using (var b = new SolidBrush(sel ? Theme.PanelHover : (hov ? Theme.PanelAlt : Theme.Panel)))
                g.FillRectangle(b, bounds);

            bool en = IsEnabled(m);
            if (en)
            {
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, new Rectangle(Theme.P(4), bounds.Y + Theme.P(10), Theme.P(3), bounds.Height - Theme.P(20)));
            }

            // 勾选框
            var cb = new Rectangle(Theme.P(14), bounds.Y + (bounds.Height - Theme.P(18)) / 2, Theme.P(18), Theme.P(18));
            if (en)
            {
                Theme.FillRound(g, Theme.Accent, cb, Theme.P(5));
                using (var pen = new Pen(Color.FromArgb(24, 16, 10), Math.Max(2f, Theme.P(2))))
                {
                    g.DrawLines(pen, new Point[] {
                        new Point(cb.X + Theme.P(4), cb.Y + Theme.P(9)),
                        new Point(cb.X + Theme.P(8), cb.Y + Theme.P(13)),
                        new Point(cb.X + Theme.P(14), cb.Y + Theme.P(5))
                    });
                }
            }
            else
            {
                using (var pen = new Pen(Theme.Border, Math.Max(1f, Theme.P(1.5f))))
                    using (var p = Theme.Round(cb, Theme.P(5)))
                        g.DrawPath(pen, p);
            }

            // 缩略图
            int th = Theme.P(42);
            var tr = new Rectangle(Theme.P(44), bounds.Y + (bounds.Height - th) / 2, th, th);
            var thumb = GetThumb(m, th, false);
            if (thumb != null)
            {
                DrawRoundedImage(g, thumb, tr, Theme.P(7));
            }
            else DrawPlaceholder(g, tr, m);

            int tx = Theme.P(96);
            int tw = w - tx - Theme.P(96);
            Color nameColor = m.Broken ? Theme.Dim : Theme.Text;
            Theme.RenderText(g, m.Name, Theme.F(13.5f, true), nameColor, new Rectangle(tx, bounds.Y + Theme.P(12), tw, Theme.P(20)), StringAlignment.Near);

            var sb = new StringBuilder();
            if (m.Tags.Count > 0) sb.Append(string.Join(" · ", m.Tags.ToArray())).Append("   ");
            sb.Append(m.IsWorkshop ? L.T("创意工坊") : L.T("本地")).Append("   ").Append(Fmt.Size(m.Size));
            if (m.FileCount > 0) sb.Append("   ").Append(m.FileCount.ToString("n0")).Append(L.T(" 文件"));
            Theme.RenderText(g, sb.ToString(), Theme.F(11.5f, false), Theme.Dim, new Rectangle(tx, bounds.Y + Theme.P(34), tw, Theme.P(18)), StringAlignment.Near);

            // 右侧状态
            int rx = w - Theme.P(10);
            if (m.Broken)
            {
                var br = new Rectangle(rx - Theme.P(92), bounds.Y + Theme.P(22), Theme.P(88), Theme.P(22));
                Draw.Badge(g, L.T("无法加载"), Theme.GreenSoft, Theme.Danger, br, Theme.F(11, false));
            }
            else if (m.Issues.Count > 0)
            {
                var br = new Rectangle(rx - Theme.P(92), bounds.Y + Theme.P(22), Theme.P(88), Theme.P(22));
                Draw.Badge(g, "⚠ " + Fmt.Short(m.Issues[0], 8), Color.FromArgb(56, 44, 24), Theme.Warn, br, Theme.F(11, false));
            }
            else if (en)
            {
                var br = new Rectangle(rx - Theme.P(64), bounds.Y + Theme.P(22), Theme.P(58), Theme.P(22));
                Draw.Badge(g, L.T("已启用"), Theme.GreenSoft, Theme.Green, br, Theme.F(11, false));
            }
            else if (m.IsWorkshop)
            {
                Theme.RenderText(g, "ID " + m.Id, Theme.F(10.5f, false), Theme.Dim, new Rectangle(rx - Theme.P(96), bounds.Y + Theme.P(22), Theme.P(92), Theme.P(22)), StringAlignment.Far);
            }

        }

        static void DrawRoundedImage(Graphics g, Image img, Rectangle r, int radius)
        {
            using (var p = Theme.Round(r, radius))
            using (var tb = new TextureBrush(img))
            {
                g.FillPath(tb, p);
            }
        }

        void DrawPlaceholder(Graphics g, Rectangle r, ModEntry m)
        {
            Theme.FillRound(g, Theme.PanelAlt, r, Theme.P(7));
            string t = string.IsNullOrEmpty(m.Name) ? "?" : m.Name.Substring(0, 1);
            Theme.RenderText(g, t, Theme.F(16, true), Theme.Dim, r, StringAlignment.Center);
        }

        void listOrder_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= listOrder.Items.Count) return;
            var m = listOrder.Items[e.Index] as ModEntry;
            if (m == null) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var b = new SolidBrush(sel ? Theme.PanelHover : Theme.Panel))
                g.FillRectangle(b, e.Bounds);

            // 序号
            int d = Theme.P(24);
            var cr = new Rectangle(Theme.P(14), e.Bounds.Y + (e.Bounds.Height - d) / 2, d, d);
            Theme.FillRound(g, Theme.PanelAlt, cr, Theme.P(d / 2));
            Theme.RenderText(g, (e.Index + 1).ToString(), Theme.F(11.5f, true), Theme.Sub, cr, StringAlignment.Center);

            int tx = Theme.P(48);
            Theme.RenderText(g, m.Name, Theme.F(13, true), m.Broken ? Theme.Danger : Theme.Text,
                new Rectangle(tx, e.Bounds.Y + Theme.P(8), listOrder.ClientSize.Width - tx - Theme.P(110), Theme.P(18)), StringAlignment.Near);

            string sub = m.IsWorkshop ? (L.T("创意工坊 · ") + Fmt.Size(m.Size)) : (L.T("本地 · ") + Fmt.Size(m.Size));
            if (m.Broken) sub = m.BrokenReason;
            else if (m.Issues.Count > 0) sub = m.Issues[0];
            Theme.RenderText(g, sub, Theme.F(11, false), m.Issues.Count > 0 ? Theme.Warn : Theme.Dim,
                new Rectangle(tx, e.Bounds.Y + Theme.P(26), listOrder.ClientSize.Width - tx - Theme.P(40), Theme.P(16)), StringAlignment.Near);

            if (e.Index == dragIndex && dragging)
            {
                using (var pen = new Pen(Theme.Accent, Math.Max(2f, Theme.P(2))))
                    g.DrawLine(pen, Theme.P(8), e.Bounds.Bottom - Theme.P(3), listOrder.ClientSize.Width - Theme.P(8), e.Bounds.Bottom - Theme.P(3));
            }
        }

        void searchBox_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Theme.FillRound(g, Theme.PanelAlt, new Rectangle(0, 0, searchBox.Width - 1, searchBox.Height - 1), Theme.P(8));
            Theme.StrokeRound(g, Theme.BorderSoft, new Rectangle(0, 0, searchBox.Width - 1, searchBox.Height - 1), Theme.P(8));
            using (var pen = new Pen(Theme.Dim, Math.Max(1.4f, Theme.P(1.6f))))
            {
                int cx = Theme.P(15), cy = searchBox.Height / 2;
                int r = Theme.P(6);
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                g.DrawLine(pen, cx + r - 1, cy + r - 1, cx + r + Theme.P(4), cy + r + Theme.P(4));
            }
            if (string.IsNullOrEmpty(txtSearch.Text) && !txtSearch.Focused)
            {
                Theme.RenderText(g, L.T("搜索模组…"), Theme.F(12.5f, false), Theme.Dim,
                    new Rectangle(Theme.P(38), 0, searchBox.Width - Theme.P(50), searchBox.Height), StringAlignment.Near);
            }
        }

        // ------------------------------------------------------------------
        // 列表刷新
        // ------------------------------------------------------------------
        void RefreshCatalogList()
        {
            int keep = listCatalog.SelectedIndex;
            ClearRowCache();
            listCatalog.BeginUpdate();
            listCatalog.Items.Clear();
            foreach (var m in catalog)
            {
                if (!Match(m)) continue;
                listCatalog.Items.Add(m);
            }
            listCatalog.EndUpdate();
            if (keep >= 0 && keep < listCatalog.Items.Count) listCatalog.SelectedIndex = keep;
            int issues = catalog.Count(x => x.Broken || x.Issues.Count > 0);
            lblCatalogCount.Text = string.Format(L.T("{0} 个模组 · {1} 个有问题"), catalog.Count, issues);
            UpdateChipCounts();
        }

        void UpdateChipCounts()
        {
            chipAll.Checked = filter == L.T("全部");
            chipOn.Checked = filter == L.T("已启用");
            chipShop.Checked = filter == L.T("创意工坊");
            chipLocal.Checked = filter == L.T("本地");
            chipIssue.Checked = filter == L.T("有问题");
            foreach (var c in new Chip[] { chipAll, chipOn, chipShop, chipLocal, chipIssue }) c.Invalidate();
        }

        bool Match(ModEntry m)
        {
            if (search.Length > 0)
            {
                string s = (m.Name + " " + m.Id + " " + string.Join(" ", m.Tags.ToArray())).ToLower();
                if (!s.Contains(search.ToLower())) return false;
            }
            switch (filter)
            {
                case "已启用": return IsEnabled(m);
                case "创意工坊": return m.IsWorkshop;
                case "本地": return !m.IsWorkshop;
                case "有问题": return m.Broken || m.Issues.Count > 0;
                default: return true;
            }
        }

        void RefreshOrderList()
        {
            listOrder.BeginUpdate();
            listOrder.Items.Clear();
            foreach (var m in order) listOrder.Items.Add(m);
            listOrder.EndUpdate();
            listOrder.Invalidate();
        }

        void UpdateStatus()
        {
            int issue = catalog.Count(x => x.Broken || x.Issues.Count > 0);
            var sb = new StringBuilder();
            sb.Append(L.T("已启用 ")).Append(order.Count).Append(L.T(" 个"));
            if (conflicts.Count > 0) sb.Append(L.T("    ·    文件冲突 ")).Append(conflicts.Count.ToString("n0")).Append(L.T(" 处"));
            if (issue > 0) sb.Append("    ·    ").Append(issue).Append(L.T(" 个模组需要留意"));
            if (dirty) sb.Append(L.T("    ·    尚未保存"));
            lblStatus.Text = sb.ToString();
            lblStatus.ForeColor = dirty ? Theme.Warn : (conflicts.Count > 0 ? Theme.Sub : Theme.Green);

            if (paths.GameVersion.Length > 0 && lblSubtitle.Text.IndexOf(paths.GameVersion) < 0) UpdateSubtitle();
        }

        void RefreshPresets()
        {
            string cur = cfg.LastPreset;
            btnPreset.Text = string.IsNullOrEmpty(cur) ? L.T("预设 ▾") : (L.T("预设：") + Fmt.Short(cur, 8) + " ▾");
            btnPreset.Invalidate();
        }

        void ShowPresetMenu()
        {
            var cm = NewMenu();
            foreach (var k in cfg.Presets.Keys.OrderBy(x => x, StringComparer.CurrentCulture))
            {
                string name = k;
                var mi = new ToolStripMenuItem((name == cfg.LastPreset ? "✓  " : "     ") + name);
                mi.Click += delegate { ApplyPreset(name); };
                cm.Items.Add(mi);
            }
            if (cfg.Presets.Count > 0) cm.Items.Add(new ToolStripSeparator());
            var miSave = new ToolStripMenuItem(L.T("保存当前顺序为预设…"));
            miSave.Click += delegate { SavePreset(); };
            cm.Items.Add(miSave);
            if (!string.IsNullOrEmpty(cfg.LastPreset))
            {
                var miDel = new ToolStripMenuItem(L.T("删除预设「") + cfg.LastPreset + "」");
                miDel.Click += delegate { DeletePreset(cfg.LastPreset); };
                cm.Items.Add(miDel);
            }
            cm.Show(btnPreset, new Point(0, btnPreset.Height + Theme.P(4)));
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------
        void SetFilter(string f)
        {
            filter = f;
            UpdateChipCounts();
            RefreshCatalogList();
        }

        ModEntry SelectedCatalogMod()
        {
            return listCatalog.SelectedItem as ModEntry;
        }

        ModEntry SelectedOrderMod()
        {
            return listOrder.SelectedItem as ModEntry;
        }

        void ToggleSelectedCatalog()
        {
            var m = SelectedCatalogMod();
            if (m == null) return;
            if (m.Broken)
            {
                MessageBox.Show(this, L.T("这个模组无法加载：") + m.BrokenReason, L.T("无法启用"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (IsEnabled(m))
            {
                order.Remove(m);
            }
            else
            {
                order.Add(m);
                var missing = m.Requires.Where(r => !catalog.Any(x =>
                    (string.Equals(x.Folder, r, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.Name, r, StringComparison.OrdinalIgnoreCase)) && order.Contains(x))).ToList();
                if (missing.Count > 0)
                {
                    var dep = catalog.FirstOrDefault(x => missing.Any(r =>
                        string.Equals(x.Folder, r, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name, r, StringComparison.OrdinalIgnoreCase)));
                    if (dep != null && !IsEnabled(dep))
                    {
                        var r = MessageBox.Show(this,
                            string.Format(L.T("「{0}」需要依赖「{1}」，但「{1}」尚未启用。\n\n要把它一并加到加载顺序里吗？"), m.Name, dep.Name),
                            L.T("缺少依赖"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r == DialogResult.Yes)
                        {
                            order.Remove(dep);
                            order.Insert(Math.Max(0, order.IndexOf(m)), dep);
                        }
                    }
                }
            }
            MarkDirty();
        }

        void MarkDirty()
        {
            dirty = true;
            Recalculate();
            RefreshOrderList();
            RefreshCatalogList();
            UpdateStatus();
            UpdateDetails();
        }

        void MoveSelected(int delta)
        {
            var m = SelectedOrderMod();
            if (m == null) return;
            int i = order.IndexOf(m);
            int j = i + delta;
            if (j < 0 || j >= order.Count) return;
            order.RemoveAt(i);
            order.Insert(j, m);
            MarkDirty();
            listOrder.SelectedIndex = j;
        }

        void MoveTop()
        {
            var m = SelectedOrderMod();
            if (m == null) return;
            order.Remove(m);
            order.Insert(0, m);
            MarkDirty();
            listOrder.SelectedIndex = 0;
        }

        void MoveBottom()
        {
            var m = SelectedOrderMod();
            if (m == null) return;
            order.Remove(m);
            order.Add(m);
            MarkDirty();
            listOrder.SelectedIndex = order.Count - 1;
        }

        void RemoveSelectedOrder()
        {
            var m = SelectedOrderMod();
            if (m == null) return;
            order.Remove(m);
            MarkDirty();
        }

        void ClearOrder()
        {
            if (order.Count == 0) return;
            if (MessageBox.Show(this, L.T("要停用全部模组吗？"), L.T("清空加载顺序"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            order.Clear();
            MarkDirty();
        }

        void listCatalog_MouseDown(object sender, MouseEventArgs e)
        {
            int i = listCatalog.IndexFromPoint(e.Location);
            if (i < 0) return;
            if (e.X < Theme.P(40)) ToggleAt(i);
        }

        void ToggleAt(int i)
        {
            if (i < 0 || i >= listCatalog.Items.Count) return;
            listCatalog.SelectedIndex = i;
            ToggleSelectedCatalog();
        }

        void listCatalog_MouseMove(object sender, MouseEventArgs e)
        {
            int i = listCatalog.IndexFromPoint(e.Location);
            if (i != hoverIndex) { hoverIndex = i; listCatalog.Invalidate(); }
        }

        // 拖拽排序
        void listOrder_MouseDown(object sender, MouseEventArgs e)
        {
            int i = listOrder.IndexFromPoint(e.Location);
            if (i < 0) return;
            dragIndex = i;
            dragStart = e.Location;
            dragging = false;
            listOrder.SelectedIndex = i;
        }

        void listOrder_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragIndex < 0) return;
            if (!dragging && Math.Abs(e.Y - dragStart.Y) > Theme.P(6)) dragging = true;
            if (!dragging) return;
            int target = listOrder.IndexFromPoint(new Point(e.X, e.Y));
            if (target < 0) target = order.Count - 1;
            if (target == dragIndex) return;
            var m = order[dragIndex];
            order.RemoveAt(dragIndex);
            order.Insert(target, m);
            dragIndex = target;
            RefreshOrderList();
            listOrder.SelectedIndex = target;
            MarkDirty();
            listOrder.Invalidate();
        }

        void UpdateDetails()
        {
            var m = SelectedCatalogMod();
            if (m == null) m = SelectedOrderMod();
            if (m == null)
            {
                lblDetName.Text = L.T("未选中模组");
                lblDetMeta.Text = L.T("从左侧目录选择一个模组查看详情");
                lblDetTags.Text = "";
                lblDetAuthor.Text = "";
                descPanel.Value = "";
                if (picCover.Image != null) { var oldImg = picCover.Image; picCover.Image = null; oldImg.Dispose(); }
                picCover.Image = EmptyTile();
                btnDetailAction.Text = L.T("加入加载顺序");
                return;
            }

            bool en = IsEnabled(m);
            lblDetName.Text = m.Name;
            var meta = new StringBuilder();
            meta.Append(m.IsWorkshop ? L.T("创意工坊模组") : L.T("本地模组"));
            meta.Append("   ·   ").Append(Fmt.Size(m.Size));
            if (m.FileCount > 0) meta.Append("   ·   ").Append(m.FileCount.ToString("n0")).Append(L.T(" 个文件"));
            if (m.IsWorkshop) meta.Append("   ·   ID ").Append(m.Id);
            else meta.Append(L.T("   ·   文件夹 ")).Append(m.Folder);
            lblDetMeta.Text = meta.ToString();
            ShowAuthor(m);
            QueueWorkshopInfo(m);

            var tags = new StringBuilder();
            if (m.Tags.Count > 0) tags.Append(L.T("标签：")).Append(string.Join(" / ", m.Tags.ToArray()));
            if (m.Requires.Count > 0)
            {
                if (tags.Length > 0) tags.Append("        ");
                tags.Append(L.T("依赖："));
                for (int i = 0; i < m.Requires.Count; i++)
                {
                    if (i > 0) tags.Append("、");
                    tags.Append(m.Requires[i]);
                }
            }
            bool verUseful = !(IsAny(m.MinVersion) && IsAny(m.MaxVersion));
            if (verUseful)
            {
                if (tags.Length > 0) tags.Append("        ");
                tags.Append(L.T("兼容 ")).Append(string.IsNullOrEmpty(m.MinVersion) ? "?" : m.MinVersion)
                    .Append(" ~ ").Append(string.IsNullOrEmpty(m.MaxVersion) ? "?" : m.MaxVersion);
            }
            if (m.Issues.Count > 0)
            {
                if (tags.Length > 0) tags.Append("    ");
                tags.Append("⚠ ").Append(string.Join("；", m.Issues.ToArray()));
            }
            lblDetTags.Text = tags.ToString();
            lblDetTags.ForeColor = m.Issues.Count > 0 ? Theme.Warn : Theme.Dim;

            descPanel.Value = string.IsNullOrEmpty(m.Desc) ? (m.Broken ? L.T("（无法读取模组信息：") + m.BrokenReason + "）" : L.T("（这个模组没有填写描述）")) : m.Desc;
            lblDescCaption.ForeColor = string.IsNullOrEmpty(m.Desc) ? Theme.Dim : Theme.Sub;

            picCover.Image = GetThumb(m, Theme.P(62), true);
            picCover.BackColor = Theme.PanelAlt;
            picCover.Visible = true;

            btnDetailAction.Text = en ? L.T("从加载顺序移除") : L.T("加入加载顺序");
            btnDetailAction.Accent = !en;
            btnDetailAction.Invalidate();
            btnDetailFolder.Enabled = !string.IsNullOrEmpty(m.Dir) && Directory.Exists(m.Dir);
        }

        static bool IsAny(string v)
        {
            return string.IsNullOrEmpty(v) || v.Equals("any", StringComparison.OrdinalIgnoreCase);
        }

        void ShowAuthor(ModEntry m)
        {
            string author = "";
            string hint = "";
            string stats = "";

            if (!string.IsNullOrEmpty(m.Author)) author = m.Author;

            if (author.Length == 0 && m.IsWorkshop)
            {
                var info = WorkshopApi.Cached(m.Id);
                if (info != null)
                {
                    if (!string.IsNullOrEmpty(info.CreatorName)) author = info.CreatorName;
                    stats = StatsText(info);
                }
            }

            if (author.Length == 0)
            {
                string guess = WorkshopApi.GuessAuthor(m.Desc);
                if (guess.Length > 0) { author = guess; hint = L.T("（据简介）"); }
            }

            if (author.Length > 0)
            {
                lblDetAuthor.Text = L.T("作者：") + author + hint + stats;
                lblDetAuthor.ForeColor = Theme.Text;
            }
            else if (m.IsWorkshop)
            {
                lblDetAuthor.Text = L.T("作者：正在从创意工坊读取…") + stats;
                lblDetAuthor.ForeColor = Theme.Dim;
            }
            else
            {
                lblDetAuthor.Text = L.T("作者：—") + stats;
                lblDetAuthor.ForeColor = Theme.Dim;
            }
        }

        static string StatsText(WorkshopInfo info)
        {
            if (info == null) return "";
            var sb = new StringBuilder();
            if (info.Subscriptions > 0) sb.Append("    ·    ").Append(info.Subscriptions.ToString("n0")).Append(L.T(" 人订阅"));
            if (info.TimeUpdated > 0)
            {
                var t = info.UpdatedLocal;
                if (t.Year > 2000) sb.Append(L.T("    ·    更新于 ")).Append(t.ToString("yyyy-MM-dd"));
            }
            return sb.ToString();
        }

        void QueueWorkshopInfo(ModEntry m)
        {
            if (m == null || !m.IsWorkshop) return;
            var cached = WorkshopApi.Cached(m.Id);
            if (cached != null && !string.IsNullOrEmpty(cached.CreatorName)) return;
            if (fetchTried.Contains(m.Id)) return;
            fetchTried.Add(m.Id);

            string id = m.Id;
            int token = ++infoToken;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                WorkshopInfo info = null;
                try { info = WorkshopApi.Fetch(id); }
                catch (Exception ex) { AppConfig.Log("联网获取失败: " + ex.Message); }
                try
                {
                    if (IsDisposed) return;
                    BeginInvoke((Action)delegate
                    {
                        if (isClosed || token != infoToken) return;
                        if (info != null) ApplyWorkshopInfo(info);
                        else AuthorUnavailable();
                    });
                }
                catch { }
            });
        }

        bool isClosed;

        void AuthorUnavailable()
        {
            ModEntry m = SelectedCatalogMod();
            if (m == null) m = SelectedOrderMod();
            if (m == null || !m.IsWorkshop) return;
            string guess = WorkshopApi.GuessAuthor(m.Desc);
            if (guess.Length > 0)
            {
                lblDetAuthor.Text = L.T("作者：") + guess + L.T("（据简介）");
                lblDetAuthor.ForeColor = Theme.Text;
            }
            else
            {
                lblDetAuthor.Text = L.T("作者：未获取到（无网络时可点创意工坊页面查看）");
                lblDetAuthor.ForeColor = Theme.Dim;
            }
        }

        void ApplyWorkshopInfo(WorkshopInfo info)
        {
            ModEntry m = SelectedCatalogMod();
            if (m == null) m = SelectedOrderMod();
            if (m == null || !string.Equals(m.Id, info.Id)) return;

            string author = string.IsNullOrEmpty(info.CreatorName) ? "" : info.CreatorName;
            string stats = StatsText(info);
            if (author.Length > 0)
            {
                lblDetAuthor.Text = L.T("作者：") + author + stats;
                lblDetAuthor.ForeColor = Theme.Text;
            }
            else
            {
                lblDetAuthor.Text = L.T("作者：未获取到（可点创意工坊页面查看）") + stats;
                lblDetAuthor.ForeColor = Theme.Dim;
            }
            if (string.IsNullOrEmpty(m.Desc) && !string.IsNullOrEmpty(info.Description))
                descPanel.Value = info.Description;
        }

        // ------------------------------------------------------------------
        // 保存
        // ------------------------------------------------------------------
        void SaveApply()
        {
            if (string.IsNullOrEmpty(paths.ProfileFile) || !File.Exists(paths.ProfileFile))
            {
                MessageBox.Show(this, L.T("没找到存档配置文件 options.set。\n\n请先在「菜单 → 设置」里确认路径，或先启动一次游戏让它生成配置。"), L.T("无法保存"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (IsGameRunning())
            {
                if (MessageBox.Show(this, L.T("游戏正在运行。\n游戏退出时可能会用内存里的设置覆盖我们写入的模组列表。\n\n仍然写入吗？"), L.T("游戏正在运行"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            try
            {
                string backup = Backups.Create(paths.ProfileFile);
                var ids = order.Select(x => x.SetId).ToList();
                OptionsFile.WriteActive(paths.ProfileFile, ids);
                dirty = false;
                UpdateStatus();
                AppConfig.Log("已写入 " + ids.Count + " 个模组，备份：" + backup);
                MessageBox.Show(this,
                    string.Format(L.T("已写入 {0} 个模组到 options.set。\n\n备份：{1}\n\n下次启动游戏时生效。"), ids.Count, backup ?? L.T("（无）")),
                    L.T("保存成功"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppConfig.Log("保存失败: " + ex);
                MessageBox.Show(this, L.T("保存失败：\n") + ex.Message, L.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        bool IsGameRunning()
        {
            try { return Process.GetProcessesByName("call_to_arms").Length > 0; } catch { return false; }
        }

        void LaunchGame()
        {
            try
            {
                if (IsGameRunning())
                {
                    MessageBox.Show(this, L.T("游戏已经在运行了。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (dirty)
                {
                    var r = MessageBox.Show(this, L.T("有尚未保存的改动，要先保存再启动吗？"), L.T("启动游戏"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel) return;
                    if (r == DialogResult.Yes) SaveApply();
                }
                Process.Start("steam://rungameid/400750");
                lblStatus.Text = L.T("已通过 Steam 启动游戏…");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.T("启动失败：") + ex.Message, L.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------
        // 预设
        // ------------------------------------------------------------------
        void SavePreset()
        {
            string def = cfg.LastPreset;
            if (string.IsNullOrEmpty(def)) def = L.T("我的配置");
            string name = InputDialog.Show(this, L.T("保存预设"), L.T("给这套加载顺序起个名字："), def);
            if (string.IsNullOrEmpty(name)) return;
            cfg.Presets[name.Trim()] = order.Select(x => x.SetId).ToList();
            cfg.LastPreset = name.Trim();
            cfg.Save();
            RefreshPresets();
            lblStatus.Text = string.Format(L.T("已保存预设「{0}」（{1} 个模组）"), name.Trim(), order.Count);
        }

        void ApplyPreset(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            List<string> ids;
            if (!cfg.Presets.TryGetValue(name, out ids)) return;

            var newOrder = new List<ModEntry>();
            var missing = new List<string>();
            foreach (var id in ids)
            {
                ModEntry m;
                if (bySetId.TryGetValue(id, out m) && !m.Broken) newOrder.Add(m);
                else missing.Add(id);
            }
            order = newOrder;
            cfg.LastPreset = name;
            cfg.Save();
            MarkDirty();
            lblStatus.Text = string.Format(L.T("已应用预设「{0}」· {1} 个模组"), name, order.Count);
            if (missing.Count > 0)
                MessageBox.Show(this, L.T("以下模组找不到了，已跳过：\n\n") + string.Join("\n", missing.ToArray()), L.T("预设不完整"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void DeletePreset(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (MessageBox.Show(this, string.Format(L.T("删除预设「{0}」？"), name), L.T("删除预设"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            cfg.Presets.Remove(name);
            if (cfg.LastPreset == name) cfg.LastPreset = "";
            cfg.Save();
            RefreshPresets();
        }

        // ------------------------------------------------------------------
        // 菜单 / 对话框
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // 分享码
        // ------------------------------------------------------------------
        void ExportShareCode()
        {
            var ids = order.Select(x => x.SetId).ToList();
            if (ids.Count == 0)
            {
                MessageBox.Show(this, L.T("当前没有启用任何模组，先加几个再导出吧。"), L.T("导出分享码"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new ShareExportDialog(L.T("当前加载顺序"), ids, cfg.Presets, TitleOf))
                dlg.ShowDialog(this);
        }

        string TitleOf(string setid)
        {
            ModEntry m;
            if (bySetId.TryGetValue(setid, out m) && !string.IsNullOrEmpty(m.Name)) return m.Name;
            var w = WorkshopApi.Cached(setid.StartsWith("mod_") ? setid.Substring(4) : setid);
            if (w != null && !string.IsNullOrEmpty(w.Title)) return w.Title;
            return "";
        }

        void ImportShareCode()
        {
            using (var dlg = new ShareImportDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Payload == null) return;
                var p = dlg.Payload;

                var newOrder = new List<ModEntry>();
                var missing = new List<MissingEntry>();
                foreach (var id in p.Ids)
                {
                    ModEntry m;
                    if (bySetId.TryGetValue(id, out m) && !m.Broken) newOrder.Add(m);
                    else
                    {
                        var e = new MissingEntry();
                        e.Id = id.StartsWith("mod_") ? id.Substring(4) : id;
                        string t;
                        if (p.Titles.TryGetValue(id, out t)) e.Title = t;
                        else e.Title = TitleOf(id);
                        missing.Add(e);
                    }
                }

                order = newOrder;
                MarkDirty();

                if (missing.Count > 0)
                {
                    using (var d = new MissingModsDialog(missing, p.Name))
                        d.ShowDialog(this);
                }

                var sb = new StringBuilder();
                sb.AppendFormat(L.T("已导入 {0} 个模组"), newOrder.Count);
                if (!string.IsNullOrEmpty(p.Name)) sb.Append(L.T("（分享码：「")).Append(p.Name).Append("」）");
                sb.Append("。");
                if (missing.Count > 0) sb.Append(L.T("\n\n缺少 ")).Append(missing.Count).Append(L.T(" 个模组（已跳过），订阅后重新导入一次就能补齐。"));
                sb.Append(L.T("\n\n现在就写入游戏配置吗？（也可以自己调整顺序后再点「保存并应用」）"));

                if (MessageBox.Show(this, sb.ToString(), L.T("导入分享码"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    SaveApply();
            }
        }

        void ShowLanguageMenu()
        {
            var cm = NewMenu();
            foreach (var code in L.Codes)
            {
                string c = code;
                var mi = new ToolStripMenuItem((code == L.Code ? "✓  " : "     ") + L.NativeName(code));
                mi.Click += delegate { SwitchLanguage(c); };
                cm.Items.Add(mi);
            }
            cm.Show(btnLang, new Point(0, btnLang.Height + Theme.P(4)));
        }

        void SwitchLanguage(string code)
        {
            if (string.IsNullOrEmpty(code) || code == L.Code) return;
            L.Code = code;
            cfg.Language = code;
            try { cfg.Save(); } catch { }
            RebuildUi();
            LoadAll();
        }

        void RebuildUi()
        {
            SuspendLayout();
            var old = new List<Control>();
            foreach (Control c in Controls) old.Add(c);
            Controls.Clear();
            foreach (var c in old) { try { c.Dispose(); } catch { } }

            BuildUi();
            LayoutUi();
            RefreshPresets();
            RefreshCatalogList();
            RefreshOrderList();
            UpdateStatus();
            UpdateDetails();
            UpdateSubtitle();
            ResumeLayout(true);
        }

        void ShowMenu()
        {
            var cm = NewMenu();
            var miSettings = new ToolStripMenuItem(L.T("设置…"));
            miSettings.Click += delegate { ShowSettings(); };
            cm.Items.Add(miSettings);
            var miBackup = new ToolStripMenuItem(L.T("备份与还原…"));
            miBackup.Click += delegate { ShowBackups(); };
            cm.Items.Add(miBackup);
            var miConflict = new ToolStripMenuItem(L.T("查看文件冲突…"));
            miConflict.Click += delegate { ShowConflicts(); };
            cm.Items.Add(miConflict);

            var miExportShare = new ToolStripMenuItem(L.T("导出分享码…"));
            miExportShare.Click += delegate { ExportShareCode(); };
            cm.Items.Add(miExportShare);

            var miImportShare = new ToolStripMenuItem(L.T("导入分享码…"));
            miImportShare.Click += delegate { ImportShareCode(); };
            cm.Items.Add(miImportShare);
            cm.Items.Add(new ToolStripSeparator());
            var miOpenGame = new ToolStripMenuItem(L.T("打开游戏目录"));
            miOpenGame.Click += delegate { Shell(paths.GameDir); };
            cm.Items.Add(miOpenGame);
            var miOpenShop = new ToolStripMenuItem(L.T("打开创意工坊目录"));
            miOpenShop.Click += delegate { Shell(paths.WorkshopDir); };
            cm.Items.Add(miOpenShop);
            var miOpenCfg = new ToolStripMenuItem(L.T("打开 options.set 所在目录"));
            miOpenCfg.Click += delegate { Shell(paths.ProfileDir); };
            cm.Items.Add(miOpenCfg);
            var miImport = new ToolStripMenuItem(L.T("导入本地模组压缩包…"));
            miImport.Click += delegate { ImportArchive(); };
            cm.Items.Add(miImport);
            cm.Items.Add(new ToolStripSeparator());
            var miAbout = new ToolStripMenuItem(L.T("关于"));
            miAbout.Click += delegate
            {
                MessageBox.Show(this,
                    L.T("地狱之门 · 模组管理器  v1.0\n\n") +
                    L.T("受旧版 GoH Mod Manager 启发重写，兼容它的配置与预设。\n\n") +
                    L.T("游戏：Call to Arms - Gates of Hell (400750)\n") +
                    L.T("配置目录：") + AppConfig.Dir + "\n" +
                    L.T("日志：") + AppConfig.LogPath,
                    L.T("关于"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            cm.Items.Add(miAbout);
            var miExit = new ToolStripMenuItem(L.T("退出"));
            miExit.Click += delegate { Close(); };
            cm.Items.Add(miExit);

            cm.Show(btnMenu, new Point(0, btnMenu.Height + Theme.P(4)));
        }

        void ShowSettings()
        {
            using (var dlg = new SettingsDialog(cfg, paths))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                cfg.GamePath = dlg.GamePath;
                cfg.WorkshopPath = dlg.WorkshopPath;
                cfg.ProfileFile = dlg.ProfileFile;
                cfg.Save();
                LoadAll();
            }
        }

        void ShowBackups()
        {
            using (var dlg = new BackupsDialog(paths.ProfileFile))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.RestoredPath == null) return;
                LoadAll();
                MessageBox.Show(this, L.T("已还原备份。游戏下次启动时会使用还原的模组列表。"), L.T("还原完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ShowConflicts()
        {
            if (conflicts.Count == 0)
            {
                MessageBox.Show(this, L.T("当前启用的模组之间没有发现同名文件冲突。"), L.T("文件冲突"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new ConflictsDialog(conflicts))
                dlg.ShowDialog(this);
        }

        void Shell(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    MessageBox.Show(this, L.T("目录不存在。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.T("错误")); }
        }

        void OpenSelectedFolder()
        {
            var m = SelectedCatalogMod();
            if (m == null) m = SelectedOrderMod();
            if (m == null || string.IsNullOrEmpty(m.Dir))
            {
                MessageBox.Show(this, L.T("这个模组没有对应的本地文件夹。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Shell(m.Dir);
        }

        void OpenWorkshopPage()
        {
            var m = SelectedCatalogMod();
            if (m == null) m = SelectedOrderMod();
            if (m == null || !m.IsWorkshop)
            {
                MessageBox.Show(this, L.T("只有创意工坊模组才有创意工坊页面。"), L.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Process.Start("https://steamcommunity.com/sharedfiles/filedetails/?id=" + m.Id);
        }

        // ------------------------------------------------------------------
        // 拖放导入
        // ------------------------------------------------------------------
        void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            Import(files);
        }

        void ImportArchive()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = L.T("模组压缩包|*.zip;*.7z;*.rar;*.tar;*.gz|所有文件|*.*");
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                Import(new string[] { ofd.FileName });
            }
        }

        void Import(string[] items)
        {
            if (string.IsNullOrEmpty(paths.GameDir))
            {
                MessageBox.Show(this, L.T("还没找到游戏目录，无法导入本地模组。"), L.T("无法导入"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string modsDir = Path.Combine(paths.GameDir, "mods");
            Directory.CreateDirectory(modsDir);

            foreach (var item in items)
            {
                try
                {
                    if (Directory.Exists(item))
                    {
                        string name = new DirectoryInfo(item).Name;
                        string dst = Path.Combine(modsDir, name);
                        if (Directory.Exists(dst))
                        {
                            if (MessageBox.Show(this, L.T("本地已存在同名模组「") + name + L.T("」，覆盖吗？"), L.T("导入"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) continue;
                        }
                        CopyDir(item, dst);
                        lblStatus.Text = L.T("已导入本地模组：") + name;
                    }
                    else if (File.Exists(item))
                    {
                        string ext = Path.GetExtension(item).ToLower();
                        string name = Path.GetFileNameWithoutExtension(item);
                        if (ext == ".zip")
                        {
                            string dst = Path.Combine(modsDir, name);
                            if (Directory.Exists(dst))
                            {
                                if (MessageBox.Show(this, L.T("本地已存在同名模组「") + name + L.T("」，覆盖吗？"), L.T("导入"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) continue;
                                Directory.Delete(dst, true);
                            }
                            ZipFile.ExtractToDirectory(item, dst);
                            NormalizeSingleFolder(dst);
                            lblStatus.Text = L.T("已导入本地模组：") + name;
                        }
                        else
                        {
                            MessageBox.Show(this, L.T("暂时只内置了 .zip 解压。\n.7z / .rar 请先解压成文件夹再拖进来。"), L.T("格式不支持"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, L.T("导入失败：") + ex.Message, L.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            ConflictScanner.ClearCache();
            LoadAll();
        }

        static void NormalizeSingleFolder(string dst)
        {
            try
            {
                var dirs = Directory.GetDirectories(dst);
                var files = Directory.GetFiles(dst);
                if (dirs.Length == 1 && files.Length == 0 && File.Exists(Path.Combine(dirs[0], "mod.info")))
                {
                    string inner = dirs[0];
                    foreach (var f in Directory.GetFiles(inner)) File.Move(f, Path.Combine(dst, Path.GetFileName(f)));
                    foreach (var d in Directory.GetDirectories(inner)) Directory.Move(d, Path.Combine(dst, Path.GetFileName(d)));
                    Directory.Delete(inner);
                }
            }
            catch { }
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S) { SaveApply(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.E) { ExportShareCode(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.I) { ImportShareCode(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.L)
            {
                int i = Array.IndexOf(L.Codes, L.Code);
                SwitchLanguage(L.Codes[(i + 1) % L.Codes.Length]);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5) { LoadAll(); e.Handled = true; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            isClosed = true;
            if (dirty && e.CloseReason == CloseReason.UserClosing)
            {
                var r = MessageBox.Show(this, L.T("有尚未保存的改动，要保存后再退出吗？"), L.T("退出"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.Yes) { SaveApply(); if (dirty) { e.Cancel = true; return; } }
            }
            cfg.WinW = ClientSize.Width;
            cfg.WinH = ClientSize.Height;
            try { cfg.Save(); } catch { }
            base.OnFormClosing(e);
        }
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Dim;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    public class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.PanelHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.PanelHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.PanelHover; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.PanelAlt; } }
        public override Color ImageMarginGradientBegin { get { return Theme.PanelAlt; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.PanelAlt; } }
        public override Color ImageMarginGradientEnd { get { return Theme.PanelAlt; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }
}
