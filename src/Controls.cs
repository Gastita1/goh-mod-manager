using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace GoHMM
{
    public static class Theme
    {
        public static float S = 1f;

        public static Color Bg = Color.FromArgb(13, 15, 20);
        public static Color Panel = Color.FromArgb(21, 25, 32);
        public static Color PanelAlt = Color.FromArgb(27, 32, 41);
        public static Color PanelHover = Color.FromArgb(34, 40, 50);
        public static Color Border = Color.FromArgb(38, 45, 56);
        public static Color BorderSoft = Color.FromArgb(30, 36, 45);
        public static Color Text = Color.FromArgb(231, 234, 240);
        public static Color Sub = Color.FromArgb(143, 151, 164);
        public static Color Dim = Color.FromArgb(101, 109, 123);
        public static Color Accent = Color.FromArgb(226, 112, 58);
        public static Color AccentHover = Color.FromArgb(241, 130, 73);
        public static Color AccentSoft = Color.FromArgb(64, 40, 26);
        public static Color Green = Color.FromArgb(78, 197, 140);
        public static Color GreenSoft = Color.FromArgb(24, 52, 43);
        public static Color Warn = Color.FromArgb(230, 181, 104);
        public static Color Danger = Color.FromArgb(228, 76, 81);

        public static string Family = "Microsoft YaHei UI";

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public static int P(int v) { return (int)Math.Round(v * S); }
        public static int P(float v) { return (int)Math.Round(v * S); }

        public static Font F(float px, bool bold)
        {
            string key = px.ToString("0.#") + (bold ? "b" : "n");
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            f = new Font(Family, px * S, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            fonts[key] = f;
            return f;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Color color, Rectangle r, int radius)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color color, Rectangle r, int radius)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(color))
                g.DrawPath(pen, p);
        }

        public static void RenderText(Graphics g, string s, Font f, Color c, Rectangle r, StringAlignment align)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                (align == StringAlignment.Near ? TextFormatFlags.Left :
                 align == StringAlignment.Far ? TextFormatFlags.Right : TextFormatFlags.HorizontalCenter) |
                TextFormatFlags.VerticalCenter);
        }
    }

    public class Card : Panel
    {
        public int Radius = 12;
        public Color Fill = Theme.Panel;
        public Color Edge = Theme.Border;
        public bool Soft;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, Fill, r, Theme.P(Radius));
            if (!Soft) Theme.StrokeRound(g, Edge, r, Theme.P(Radius));
            base.OnPaint(e);
        }
    }

    public class FlatButton : Button
    {
        public Color BackNormal = Theme.PanelAlt;
        public Color BackHover = Theme.PanelHover;
        public Color BackDown = Theme.Border;
        public Color EdgeColor = Theme.Border;
        public Color TextColor = Theme.Text;
        public bool Accent;
        public bool Ghost;
        public int Radius = 8;

        bool hover, down;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = Theme.F(12.5f, false);
            UseMnemonic = false;
            BackColor = Theme.Panel;
            TabStop = false;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; down = false; Invalidate(); };
            MouseDown += delegate { down = true; Invalidate(); };
            MouseUp += delegate { down = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color fill;
            if (Accent) fill = down ? Theme.AccentHover : (hover ? Theme.AccentHover : Theme.Accent);
            else if (Ghost) fill = down ? Theme.PanelAlt : (hover ? Theme.PanelAlt : Theme.Bg);
            else fill = down ? BackDown : (hover ? BackHover : BackNormal);

            if (fill != Color.Transparent)
                Theme.FillRound(g, fill, r, Theme.P(Radius));

            if (!Accent && !Ghost)
                Theme.StrokeRound(g, EdgeColor, r, Theme.P(Radius));

            Color tc = Accent ? Color.FromArgb(24, 16, 10) : TextColor;
            Theme.RenderText(g, Text, Font, tc, r, StringAlignment.Center);
            if (Focused && !Accent) Theme.StrokeRound(g, Theme.Accent, r, Theme.P(Radius));
        }
    }

    public class Chip : Control
    {
        public int Radius = 9;
        public bool Checked;
        public int Count = -1;

        public Chip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.F(12f, false);
            Cursor = Cursors.Hand;
            BackColor = Theme.Panel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, Checked ? Theme.AccentSoft : Theme.PanelAlt, r, Theme.P(Radius));
            if (Checked) Theme.StrokeRound(g, Color.FromArgb(120, 226, 112, 58), r, Theme.P(Radius));
            Color tc = Checked ? Theme.AccentHover : Theme.Sub;
            Theme.RenderText(g, Text, Font, tc, r, StringAlignment.Center);
        }
    }

    // 圆形序号 / 状态点
    public static class Draw
    {
        public static void Dot(Graphics g, Color c, int x, int y, int d)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x, y, d, d);
        }

        public static void Badge(Graphics g, string text, Color back, Color fore, Rectangle r, Font f)
        {
            Theme.FillRound(g, back, r, Theme.P(6));
            Theme.RenderText(g, text, f, fore, r, StringAlignment.Center);
        }

        public static Image Tile(string name, int size)
        {
            try
            {
                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var b = new SolidBrush(Theme.PanelAlt)) g.FillRectangle(b, 0, 0, size, size);
                    string t = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper();
                    using (var f = new Font(Theme.Family, size * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel))
                        Theme.RenderText(g, t, f, Theme.Dim, new Rectangle(0, 0, size, size), StringAlignment.Center);
                }
                return bmp;
            }
            catch { return null; }
        }

        public static Image Thumb(string path, int size)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
            try
            {
                using (var img = Image.FromFile(path))
                {
                    int w = size, h = size;
                    var bmp = new Bitmap(w, h);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        // 等比裁剪填满
                        float scale = Math.Max((float)w / img.Width, (float)h / img.Height);
                        int dw = (int)(img.Width * scale), dh = (int)(img.Height * scale);
                        g.DrawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);
                    }
                    return bmp;
                }
            }
            catch { return null; }
        }
    }
}

namespace GoHMM
{
    // 原生滚动条推到可视区外，右上盖一层自绘细滚动条
    public class ScrollHost : Panel
    {
        public readonly ListBox List;
        readonly ScrollBarLite bar;
        Timer tick;
        int lastTop = -1;
        int lastCount = -1;

        public ScrollHost(ListBox lb)
        {
            List = lb;
            BackColor = lb.BackColor;
            lb.BorderStyle = BorderStyle.None;
            lb.IntegralHeight = false;
            lb.HorizontalScrollbar = false;
            Controls.Add(lb);
            bar = new ScrollBarLite(this);
            Controls.Add(bar);
            bar.BringToFront();
            tick = new Timer();
            tick.Interval = 60;
            tick.Tick += delegate
            {
                if (List.TopIndex != lastTop || List.Items.Count != lastCount)
                {
                    lastTop = List.TopIndex;
                    lastCount = List.Items.Count;
                    bar.Invalidate();
                }
            };
            tick.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            try { tick.Stop(); tick.Dispose(); } catch { }
            base.OnHandleDestroyed(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            List.Bounds = new Rectangle(0, 0, Width + Theme.P(20), Height);
            bar.Bounds = new Rectangle(Width - Theme.P(11), 0, Theme.P(11), Height);
            bar.BringToFront();
            bar.Invalidate();
        }

        public int Total { get { return List.Items.Count * List.ItemHeight; } }
        public bool Need { get { return Total > Height + 1; } }

        public void SetOffset(int off)
        {
            int maxScroll = Math.Max(0, Total - Height);
            if (off < 0) off = 0;
            if (off > maxScroll) off = maxScroll;
            int idx = off / Math.Max(1, List.ItemHeight);
            if (idx > List.Items.Count - 1) idx = List.Items.Count - 1;
            if (idx >= 0) List.TopIndex = idx;
            bar.Invalidate();
        }
    }

    public class ScrollBarLite : Control
    {
        readonly ScrollHost host;
        bool drag;
        int grabDy;

        public ScrollBarLite(ScrollHost h)
        {
            host = h;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = h.BackColor;
        }

        Rectangle Thumb()
        {
            int h = Height;
            int thumbH = Math.Max(Theme.P(30), (int)((float)h * h / Math.Max(1, host.Total)));
            if (thumbH > h) thumbH = h;
            int maxScroll = Math.Max(1, host.Total - h);
            int off = Math.Min(maxScroll, host.List.TopIndex * host.List.ItemHeight);
            int y = (int)((float)(h - thumbH) * off / maxScroll);
            return new Rectangle(Theme.P(3), y, Theme.P(5), thumbH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
            if (!host.Need) return;
            Theme.FillRound(g, Theme.BorderSoft, new Rectangle(Theme.P(3), Theme.P(2), Theme.P(5), Height - Theme.P(4)), Theme.P(3));
            Theme.FillRound(g, Theme.Border, Thumb(), Theme.P(3));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!host.Need) return;
            var t = Thumb();
            if (t.Contains(e.Location)) { drag = true; grabDy = e.Y - t.Y; }
            else host.SetOffset(host.List.TopIndex * host.List.ItemHeight + (e.Y < t.Y ? -Height : Height));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!drag) return;
            int thumbH = Thumb().Height;
            int maxScroll = Math.Max(1, host.Total - Height);
            int y = Math.Max(0, Math.Min(Height - thumbH, e.Y - grabDy));
            host.SetOffset((int)((float)y * maxScroll / Math.Max(1, Height - thumbH)));
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = false; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            host.SetOffset(host.List.TopIndex * host.List.ItemHeight - Math.Sign(e.Delta) * host.List.ItemHeight * 3);
        }
    }

    // 只读多行文本 + 自绘滚动条
    public class TextPanel : Control
    {
        string text = "";
        int scroll;
        int totalH;
        bool dragThumb;
        int grabDy;

        public TextPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.PanelAlt;
            ForeColor = Theme.Sub;
            Font = Theme.F(12.5f, false);
        }

        public string Value
        {
            get { return text; }
            set { text = value ?? ""; scroll = 0; Measure(); Invalidate(); }
        }

        int TextW { get { return Math.Max(Theme.P(40), Width - Theme.P(14)); } }

        void Measure()
        {
            if (string.IsNullOrEmpty(text) || Width <= 0) { totalH = 0; return; }
            var sz = TextRenderer.MeasureText(text, Font, new Size(TextW, 200000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            totalH = sz.Height + Theme.P(2);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Measure(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
            if (string.IsNullOrEmpty(text)) return;

            var state = g.Save();
            g.SetClip(new Rectangle(0, 0, TextW, Height));
            g.TranslateTransform(0, -scroll);
            TextRenderer.DrawText(g, text, Font, new Rectangle(0, 0, TextW, totalH), ForeColor,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            g.Restore(state);

            if (totalH > Height + 2)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int barW = Theme.P(5);
                int x = Width - Theme.P(9);
                Theme.FillRound(g, Theme.BorderSoft, new Rectangle(x, 0, barW, Height - 1), Theme.P(3));
                int thumbH = Math.Max(Theme.P(26), (int)((float)Height * Height / totalH));
                int maxScroll = Math.Max(1, totalH - Height);
                int y = (int)((float)(Height - thumbH) * scroll / maxScroll);
                Theme.FillRound(g, Theme.Border, new Rectangle(x, y, barW, thumbH), Theme.P(3));
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll -= Math.Sign(e.Delta) * Theme.P(48);
            Clamp();
            Invalidate();
        }

        void Clamp()
        {
            int max = Math.Max(0, totalH - Height);
            if (scroll < 0) scroll = 0;
            if (scroll > max) scroll = max;
        }

        int ThumbH()
        {
            return Math.Max(Theme.P(26), (int)((float)Height * Height / Math.Max(1, totalH)));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (totalH > Height + 2 && e.X > Width - Theme.P(14))
            {
                int maxScroll = Math.Max(1, totalH - Height);
                int y = (int)((float)(Height - ThumbH()) * scroll / maxScroll);
                if (e.Y >= y && e.Y <= y + ThumbH()) { dragThumb = true; grabDy = e.Y - y; }
                else { scroll += (e.Y < y ? -1 : 1) * Height; Clamp(); Invalidate(); }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragThumb)
            {
                int maxScroll = Math.Max(1, totalH - Height);
                int y = Math.Max(0, Math.Min(Height - ThumbH(), e.Y - grabDy));
                scroll = (int)((float)y * maxScroll / Math.Max(1, Height - ThumbH()));
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { dragThumb = false; base.OnMouseUp(e); }
    }
}
