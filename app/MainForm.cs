// Interface do TapGlobalPatcher (WinForms, desenho próprio em tema escuro).
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TapGlobal
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(18, 20, 24);
        public static readonly Color Panel = Color.FromArgb(28, 31, 38);
        public static readonly Color PanelHover = Color.FromArgb(34, 38, 47);
        public static readonly Color Border = Color.FromArgb(55, 60, 72);
        public static readonly Color Text = Color.FromArgb(236, 238, 242);
        public static readonly Color Muted = Color.FromArgb(150, 156, 170);
        public static readonly Color Dim = Color.FromArgb(95, 100, 112);
        public static readonly Color Accent = Color.FromArgb(64, 156, 255);
        public static readonly Color AccentHover = Color.FromArgb(96, 174, 255);
        public static readonly Color Ok = Color.FromArgb(46, 204, 113);
        public static readonly Color OkBg = Color.FromArgb(22, 48, 36);
        public static readonly Color Err = Color.FromArgb(255, 99, 88);
        public static readonly Color ErrBg = Color.FromArgb(56, 26, 26);
        public static readonly Color Warn = Color.FromArgb(255, 184, 77);
        public static readonly Color Tap = Color.FromArgb(255, 70, 70);

        public static Font F(float size, FontStyle st) { return new Font("Segoe UI", size, st); }
        public static Font F(float size) { return new Font("Segoe UI", size); }
        public static Font Semi(float size) { return new Font("Segoe UI Semibold", size); }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawCheck(Graphics g, PointF c, float s, Color col, float w)
        {
            using (Pen p = new Pen(col, w)) {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawLines(p, new PointF[] { new PointF(c.X - s * 0.5f, c.Y), new PointF(c.X - s * 0.12f, c.Y + s * 0.38f), new PointF(c.X + s * 0.55f, c.Y - s * 0.42f) });
            }
        }

        public static void DrawX(Graphics g, PointF c, float s, Color col, float w)
        {
            using (Pen p = new Pen(col, w)) {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLine(p, c.X - s / 2, c.Y - s / 2, c.X + s / 2, c.Y + s / 2);
                g.DrawLine(p, c.X + s / 2, c.Y - s / 2, c.X - s / 2, c.Y + s / 2);
            }
        }
    }

    // Painel com double buffering para desenho sem cintilação
    class Canvas : Panel
    {
        public Canvas() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    }

    // Botão arredondado
    class FlatButton : Canvas
    {
        public bool Primary = true; public Color Surface = Color.Empty;
        bool hover, down;
        public FlatButton() { Cursor = Cursors.Hand; Font = Theme.Semi(10.5f); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Surface != Color.Empty ? Surface : (Parent != null ? Parent.BackColor : Theme.Bg));
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fill, fg, border;
            if (!Enabled) { fill = Color.FromArgb(40, 44, 52); fg = Theme.Dim; border = fill; }
            else if (Primary) { fill = down ? Theme.Accent : (hover ? Theme.AccentHover : Theme.Accent); fg = Color.White; border = fill; }
            else { fill = hover ? Theme.PanelHover : Theme.Panel; fg = Theme.Text; border = Theme.Border; }
            using (GraphicsPath p = Theme.Round(r, 8))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen pen = new Pen(border)) { g.FillPath(b, p); g.DrawPath(pen, p); }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // Cabeçalho com título e um "LED" de tap
    class Header : Canvas
    {
        public float Pulse;
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Theme.Bg);
            // pílula de versão
            string pill = "GP-200  •  FIRMWARE " + Patcher.FirmwareVersion;
            Font pf = Theme.Semi(8f);
            Size ps = TextRenderer.MeasureText(pill, pf);
            RectangleF pr = new RectangleF(28, 22, ps.Width + 14, 22);
            using (GraphicsPath p = Theme.Round(pr, 11))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, 64, 156, 255))) { g.FillPath(b, p); }
            TextRenderer.DrawText(g, pill, pf, Rectangle.Round(pr), Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "Tap Tempo Global", Theme.Semi(24f), new Point(22, 48), Theme.Text);
            TextRenderer.DrawText(g, L.T("Bata o tempo uma vez. Troque de preset sem perder o BPM.", "Tap the tempo once. Switch presets without losing the BPM."), Theme.F(10.5f), new Point(27, 94), Theme.Muted);

            // LED de tap
            float cx = Width - 70, cy = 66;
            float glow = 26 + 10 * Pulse;
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(cx - glow, cy - glow, glow * 2, glow * 2);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Color.FromArgb((int)(70 + 90 * Pulse), Theme.Tap);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, Theme.Tap) };
                    g.FillEllipse(pb, cx - glow, cy - glow, glow * 2, glow * 2);
                }
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 44, 52))) g.FillEllipse(b, cx - 22, cy - 22, 44, 44);
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(140 + 115 * Pulse), Theme.Tap))) g.FillEllipse(b, cx - 15, cy - 15, 30, 30);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(b, cx - 8, cy - 11, 9, 6);
            TextRenderer.DrawText(g, "TAP", Theme.Semi(8f), new Rectangle((int)cx - 30, (int)cy + 26, 60, 18), Theme.Muted, TextFormatFlags.HorizontalCenter);
        }
    }

    // Indicador de etapas
    class Steps : Canvas
    {
        public int Current = 0; // 0..2 ; 3 = tudo pronto
        static string[] Labels { get { return L.En ? new string[] { "Official firmware", "Generate file", "Flash the pedal" } : new string[] { "Firmware oficial", "Gerar arquivo", "Gravar na pedaleira" }; } }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Theme.Bg);
            int n = 3; float x0 = 28, w = (Width - 56f) / n, cy = 16;
            for (int i = 0; i < n; i++)
            {
                float cx = x0 + 15;
                float sx = x0 + i * w;
                bool done = i < Current, active = i == Current;
                Color c = done ? Theme.Ok : (active ? Theme.Accent : Theme.Border);
                if (i < n - 1)
                    using (Pen p = new Pen(i < Current ? Theme.Ok : Theme.Border, 2)) g.DrawLine(p, sx + 40, cy, sx + w - 8, cy);
                RectangleF circ = new RectangleF(sx + 2, cy - 13, 26, 26);
                if (done) { using (SolidBrush b = new SolidBrush(Theme.Ok)) g.FillEllipse(b, circ); Theme.DrawCheck(g, new PointF(circ.X + 13, circ.Y + 13), 12, Theme.Bg, 2.4f); }
                else
                {
                    using (Pen p = new Pen(c, 2)) g.DrawEllipse(p, circ);
                    if (active) using (SolidBrush b = new SolidBrush(Color.FromArgb(40, c))) g.FillEllipse(b, circ);
                    TextRenderer.DrawText(g, (i + 1).ToString(), Theme.Semi(9.5f), Rectangle.Round(circ), active ? Theme.Text : Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                TextRenderer.DrawText(g, Labels[i], Theme.F(9f), new Point((int)sx - 2, (int)cy + 18), done || active ? Theme.Text : Theme.Dim);
            }
        }
    }

    enum DropState { Empty, Official, AlreadyPatched, Error }

    // Área de arrastar e soltar
    class DropZone : Canvas
    {
        public DropState State = DropState.Empty;
        public string Title = "", Detail = "";
        public bool IsDragOver;
        bool hover;
        public DropZone() { Cursor = Cursors.Hand; AllowDrop = true; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Theme.Bg);
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            Color border = Theme.Border, fill = (hover || IsDragOver) ? Theme.PanelHover : Theme.Panel, icon = Theme.Muted;
            bool dashed = State == DropState.Empty;
            if (IsDragOver) { border = Theme.Accent; dashed = true; }
            else if (State == DropState.Official) { border = Theme.Ok; fill = Theme.OkBg; icon = Theme.Ok; }
            else if (State == DropState.AlreadyPatched) { border = Theme.Warn; icon = Theme.Warn; }
            else if (State == DropState.Error) { border = Theme.Err; fill = Theme.ErrBg; icon = Theme.Err; }
            using (GraphicsPath p = Theme.Round(r, 12))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen pen = new Pen(border, 1.6f))
            {
                if (dashed) pen.DashPattern = new float[] { 5, 4 };
                g.FillPath(b, p); g.DrawPath(pen, p);
            }

            float cx = 64, cy = Height / 2f;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(35, icon))) g.FillEllipse(b, cx - 26, cy - 26, 52, 52);
            if (State == DropState.Official) Theme.DrawCheck(g, new PointF(cx, cy), 20, Theme.Ok, 3.2f);
            else if (State == DropState.Error) Theme.DrawX(g, new PointF(cx, cy), 16, Theme.Err, 3.2f);
            else if (State == DropState.AlreadyPatched) { TextRenderer.DrawText(g, "!", Theme.Semi(18f), new Rectangle((int)cx - 20, (int)cy - 20, 40, 40), Theme.Warn, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
            else
            {
                // seta para baixo + bandeja
                using (Pen p = new Pen(IsDragOver ? Theme.Accent : Theme.Muted, 2.6f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                    g.DrawLine(p, cx, cy - 13, cx, cy + 4);
                    g.DrawLines(p, new PointF[] { new PointF(cx - 7, cy - 3), new PointF(cx, cy + 4), new PointF(cx + 7, cy - 3) });
                    g.DrawLines(p, new PointF[] { new PointF(cx - 12, cy + 5), new PointF(cx - 12, cy + 12), new PointF(cx + 12, cy + 12), new PointF(cx + 12, cy + 5) });
                }
            }

            string t = Title, d = Detail;
            if (State == DropState.Empty)
            {
                t = IsDragOver ? L.T("Solte o arquivo aqui", "Drop the file here") : L.T("Arraste o firmware oficial para cá", "Drag the official firmware here");
                d = L.T("ou clique para procurar  •  GP-200_USB_Firmware_", "or click to browse  •  GP-200_USB_Firmware_") + Patcher.FirmwareVersion + ".bin";
            }
            Rectangle tr = new Rectangle(110, (int)cy - 26, Width - 130, 26);
            Rectangle dr = new Rectangle(110, (int)cy + 2, Width - 130, 40);
            TextRenderer.DrawText(g, t, Theme.Semi(12f), tr, Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
            TextRenderer.DrawText(g, d, Theme.F(9f), dr, Theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.Left);
        }
    }

    // Cartão de resultado
    class ResultCard : Canvas
    {
        public bool Success = true;
        public string Title = "", Detail = "";
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Theme.Bg);
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            Color c = Success ? Theme.Ok : Theme.Err;
            using (GraphicsPath p = Theme.Round(r, 12))
            using (SolidBrush b = new SolidBrush(Success ? Theme.OkBg : Theme.ErrBg))
            using (Pen pen = new Pen(Color.FromArgb(120, c), 1.2f)) { g.FillPath(b, p); g.DrawPath(pen, p); }
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, 20, 18, 26, 26);
            if (Success) Theme.DrawCheck(g, new PointF(33, 31), 12, Theme.OkBg, 2.6f); else Theme.DrawX(g, new PointF(33, 31), 9, Theme.ErrBg, 2.6f);
            TextRenderer.DrawText(g, Title, Theme.Semi(11.5f), new Point(56, 14), Theme.Text);
            TextRenderer.DrawText(g, Detail, Theme.F(9f), new Rectangle(57, 38, Width - 70, 40), Theme.Muted, TextFormatFlags.WordBreak);
        }
    }

    // Janela de texto (detalhes e ajuda), no mesmo tema
    class InfoDialog : Form
    {
        public InfoDialog(string title, string body, bool mono)
        {
            Text = title; BackColor = Theme.Bg; ForeColor = Theme.Text; Width = 640; Height = 520;
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            Label h = new Label(); h.Text = title; h.Font = Theme.Semi(14f); h.ForeColor = Theme.Text; h.SetBounds(22, 16, 580, 34); Controls.Add(h);
            TextBox t = new TextBox(); t.Multiline = true; t.ReadOnly = true; t.ScrollBars = ScrollBars.Vertical; t.BorderStyle = BorderStyle.None;
            t.BackColor = Theme.Panel; t.ForeColor = Theme.Text; t.Font = mono ? new Font("Consolas", 9.5f) : Theme.F(10.5f);
            t.Text = body.Replace("\r\n", "\n").Replace("\n", "\r\n"); t.SetBounds(24, 60, 576, 352); t.SelectionStart = 0; Controls.Add(t);
            FlatButton ok = new FlatButton(); ok.Text = L.T("Fechar", "Close"); ok.SetBounds(480, 424, 120, 38); ok.Click += delegate { Close(); }; Controls.Add(ok);
            MainForm.DarkTitle(this);
        }
    }

    public class MainForm : Form
    {
        Header header; Steps steps; DropZone drop; Label lblOut; LinkLabel lnkChangeOut;
        FlatButton btnGo, btnOpenFolder, btnDetails; ResultCard result;
        Timer pulseTimer; float pulseT;

        string inputPath, outputPath, lastLog = "";
        bool lastResultOk;
        byte[] inputData;

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        public static void DarkTitle(Form f)
        {
            f.HandleCreated += delegate {
                try { int on = 1; if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4); } catch { }
            };
        }

        public MainForm()
        {
            Text = "Tap Tempo Global — GP-200";
            ClientSize = new Size(760, 640);
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            DarkTitle(this);
            AllowDrop = true; DragEnter += OnDragEnter; DragDrop += OnDragDrop;
            BuildUi();
            pulseTimer = new Timer(); pulseTimer.Interval = 30; pulseTimer.Tick += OnPulse; pulseTimer.Start();
        }

        // Monta (ou remonta, ao trocar de idioma) todos os controles
        void BuildUi()
        {
            SuspendLayout();
            while (Controls.Count > 0) { Control c = Controls[0]; Controls.RemoveAt(0); c.Dispose(); }

            header = new Header(); header.SetBounds(0, 0, 760, 128); Controls.Add(header);
            steps = new Steps(); steps.SetBounds(0, 132, 760, 52); Controls.Add(steps);

            drop = new DropZone(); drop.SetBounds(28, 196, 704, 120); Controls.Add(drop);
            drop.Click += delegate { Browse(); };
            drop.DragEnter += OnDragEnter; drop.DragLeave += delegate { drop.IsDragOver = false; drop.Invalidate(); }; drop.DragDrop += OnDragDrop;

            Label outCap = new Label(); outCap.Text = L.T("SALVAR EM", "SAVE TO"); outCap.Font = Theme.Semi(8f); outCap.ForeColor = Theme.Dim; outCap.SetBounds(30, 330, 120, 18); Controls.Add(outCap);
            lblOut = new Label(); lblOut.Text = "—"; lblOut.AutoEllipsis = true; lblOut.ForeColor = Theme.Muted; lblOut.SetBounds(30, 350, 600, 20); Controls.Add(lblOut);
            lnkChangeOut = MakeLink(L.T("alterar", "change"), 650, 350); lnkChangeOut.AutoSize = false; lnkChangeOut.TextAlign = ContentAlignment.TopRight; lnkChangeOut.SetBounds(632, 350, 100, 20); lnkChangeOut.Visible = false;
            lnkChangeOut.Click += delegate { ChooseOutput(); };

            btnGo = new FlatButton(); btnGo.Text = L.T("Gerar firmware com Tap Tempo Global", "Generate firmware with Global Tap Tempo"); btnGo.SetBounds(28, 384, 704, 48); btnGo.Enabled = false; btnGo.Click += delegate { Generate(); }; Controls.Add(btnGo);

            result = new ResultCard(); result.SetBounds(28, 446, 704, 92); result.Visible = false; Controls.Add(result);
            btnOpenFolder = new FlatButton(); btnOpenFolder.Primary = false; btnOpenFolder.Surface = Theme.OkBg; btnOpenFolder.Text = L.T("Abrir pasta", "Open folder"); btnOpenFolder.Font = Theme.Semi(9f);
            btnOpenFolder.SetBounds(462, 26, 112, 34); btnOpenFolder.Click += delegate { OpenFolder(); }; result.Controls.Add(btnOpenFolder);
            btnDetails = new FlatButton(); btnDetails.Primary = false; btnDetails.Surface = Theme.OkBg; btnDetails.Text = L.T("Detalhes", "Details"); btnDetails.Font = Theme.Semi(9f);
            btnDetails.SetBounds(582, 26, 100, 34); btnDetails.Click += delegate { new InfoDialog(L.T("Relatório da verificação", "Verification report"), lastLog, true).ShowDialog(this); }; result.Controls.Add(btnDetails);

            // rodapé
            Panel sep = new Panel(); sep.BackColor = Theme.Border; sep.SetBounds(28, 560, 704, 1); Controls.Add(sep);
            LinkLabel l1 = MakeLink(L.T("Como gravar na pedaleira", "How to flash the pedal"), 28, 574); l1.Click += delegate { ShowHelpFlash(); };
            LinkLabel l2 = MakeLink(L.T("Voltar ao firmware oficial", "Back to the official firmware"), l1.Right + 22, 574); l2.Click += delegate { ShowHelpRestore(); };
            LinkLabel l3 = MakeLink(L.T("O que muda?", "What changes?"), l2.Right + 22, 574); l3.Click += delegate { ShowHelpWhat(); };
            LinkLabel lang = MakeLink(L.En ? "PT  Português" : "EN  English", 0, 574);
            lang.Left = 732 - lang.Width; lang.Click += delegate { L.En = !L.En; BuildUi(); };
            Label disc = new Label(); disc.Text = L.T("Projeto independente, sem vínculo com a Valeton. Use por sua conta e risco.  •  v", "Independent project, not affiliated with Valeton. Use at your own risk.  •  v") + Patcher.Version;
            disc.ForeColor = Theme.Dim; disc.Font = Theme.F(8.5f); disc.SetBounds(28, 604, 704, 18); Controls.Add(disc);
            ResumeLayout();

            // restaura o estado depois de trocar de idioma
            if (inputPath != null)
            {
                string keepOut = outputPath; bool hadResult = lastResultOk;
                LoadInput(inputPath);
                if (keepOut != null && inputData != null) { outputPath = keepOut; lblOut.Text = outputPath; }
                if (hadResult && inputData != null && File.Exists(outputPath)) { ShowSuccess(); lastResultOk = true; }
            }
        }

        LinkLabel MakeLink(string text, int x, int y)
        {
            LinkLabel l = new LinkLabel(); l.Text = text; l.AutoSize = true; l.Location = new Point(x, y); l.Font = Theme.F(9.5f);
            l.LinkColor = Theme.Accent; l.ActiveLinkColor = Theme.AccentHover; l.DisabledLinkColor = Theme.Dim; l.LinkBehavior = LinkBehavior.HoverUnderline;
            Controls.Add(l); return l;
        }

        // LED pisca a 120 BPM (ou no ritmo de cliques no cabeçalho, de brincadeira)
        void OnPulse(object s, EventArgs e)
        {
            pulseT += pulseTimer.Interval / 1000f;
            float period = 0.5f; // 120 BPM
            float ph = (pulseT % period) / period;
            header.Pulse = (float)Math.Max(0, 1 - ph * 3.2);
            header.Invalidate(new Rectangle(header.Width - 140, 0, 140, header.Height));
        }

        void OnDragEnter(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; drop.IsDragOver = true; drop.Invalidate(); }
        }

        void OnDragDrop(object s, DragEventArgs e)
        {
            drop.IsDragOver = false;
            string[] f = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (f != null && f.Length > 0) LoadInput(f[0]); else drop.Invalidate();
        }

        void Browse()
        {
            OpenFileDialog d = new OpenFileDialog(); d.Filter = "Firmware GP-200 (*.bin)|*.bin|" + L.T("Todos os arquivos", "All files") + "|*.*"; d.Title = L.T("Selecione o firmware oficial GP-200 ", "Select the official GP-200 firmware ") + Patcher.FirmwareVersion;
            string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(dl)) d.InitialDirectory = dl;
            if (d.ShowDialog(this) == DialogResult.OK) LoadInput(d.FileName);
        }

        void LoadInput(string path)
        {
            result.Visible = false; inputData = null; inputPath = path; lastResultOk = false;
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Length > 64L * 1024 * 1024) throw new Exception(L.T("Arquivo grande demais para ser um firmware da GP-200.", "File too large to be a GP-200 firmware."));
                byte[] d = File.ReadAllBytes(path);
                string sha;
                FirmwareKind k = Patcher.Identify(d, out sha);
                if (k == FirmwareKind.Official)
                {
                    inputData = d;
                    SetDrop(DropState.Official, L.T("Firmware oficial ", "Official firmware ") + Patcher.FirmwareVersion + L.T(" verificado", " verified"), Path.GetFileName(path) + "\nSHA-256 " + sha.Substring(0, 16) + L.T("…  confere com o arquivo da Valeton", "…  matches the Valeton file"));
                    outputPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_TAPGLOBAL.bin");
                    lblOut.Text = outputPath; lnkChangeOut.Visible = true; btnGo.Enabled = true; steps.Current = 1;
                }
                else if (k == FirmwareKind.AlreadyPatched)
                    Fail(DropState.AlreadyPatched, L.T("Este arquivo já tem o Tap Tempo Global", "This file already has Global Tap Tempo"), Path.GetFileName(path) + L.T("\nPode gravar direto na pedaleira. Para gerar de novo, use o firmware oficial.", "\nYou can flash it directly. To generate it again, use the official firmware."));
                else if (k == FirmwareKind.WrongSize)
                    Fail(DropState.Error, L.T("Não é o firmware oficial GP-200 ", "Not the official GP-200 firmware ") + Patcher.FirmwareVersion, Path.GetFileName(path) + L.T("  •  tamanho ", "  •  size ") + d.Length.ToString("N0") + L.T(" bytes (esperado ", " bytes (expected ") + Patcher.ExpectedSize.ToString("N0") + ")");
                else
                    Fail(DropState.Error, L.T("Firmware diferente do oficial ", "Firmware differs from official ") + Patcher.FirmwareVersion, Path.GetFileName(path) + L.T("\nO SHA-256 não confere. Baixe novamente o arquivo no site da Valeton.", "\nThe SHA-256 does not match. Download the file again from the Valeton website."));
            }
            catch (Exception ex) { Fail(DropState.Error, L.T("Não foi possível ler o arquivo", "Could not read the file"), ex.Message); }
        }

        void SetDrop(DropState st, string title, string detail) { drop.State = st; drop.Title = title; drop.Detail = detail; drop.Invalidate(); steps.Invalidate(); }

        void Fail(DropState st, string title, string detail)
        {
            SetDrop(st, title, detail);
            btnGo.Enabled = false; lnkChangeOut.Visible = false; lblOut.Text = "—"; steps.Current = 0; steps.Invalidate();
        }

        void ChooseOutput()
        {
            SaveFileDialog d = new SaveFileDialog(); d.Filter = "Firmware (*.bin)|*.bin"; d.FileName = Path.GetFileName(outputPath);
            d.InitialDirectory = Path.GetDirectoryName(outputPath);
            if (d.ShowDialog(this) == DialogResult.OK) { outputPath = d.FileName; lblOut.Text = outputPath; }
        }

        void Generate()
        {
            if (inputData == null) return;
            StringBuilder log = new StringBuilder();
            try
            {
                if (Path.GetFullPath(outputPath).Equals(Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
                    throw new Exception(L.T("Escolha outro nome: o firmware original não deve ser sobrescrito.", "Choose another name: the original firmware must not be overwritten."));
                Cursor = Cursors.WaitCursor;
                byte[] patched = Patcher.Apply(inputData, log);
                File.WriteAllBytes(outputPath, patched);
                // relê do disco para confirmar o que foi gravado
                string sha; if (Patcher.Identify(File.ReadAllBytes(outputPath), out sha) != FirmwareKind.AlreadyPatched)
                    throw new Exception(L.T("O arquivo salvo não confere. Tente salvar em outra pasta.", "The saved file does not match. Try saving to another folder."));
                log.AppendLine(); log.AppendLine(L.T("Arquivo gerado: ", "File generated: ") + outputPath);
                lastLog = log.ToString();
                ShowSuccess(); lastResultOk = true;
            }
            catch (Exception ex)
            {
                log.AppendLine(L.T("ERRO: ", "ERROR: ") + ex.Message); lastLog = log.ToString();
                lastResultOk = false; result.Success = false; result.Title = L.T("Não foi possível gerar o arquivo", "Could not generate the file"); result.Detail = ex.Message;
                btnOpenFolder.Visible = false; btnDetails.Visible = true; btnDetails.Surface = Theme.ErrBg;
            }
            finally { Cursor = Cursors.Default; }
            result.Visible = true; result.Invalidate(); steps.Invalidate();
        }

        void ShowSuccess()
        {
            result.Success = true; result.Title = L.T("Pronto! Firmware gerado e verificado", "Done! Firmware generated and verified");
            result.Detail = Path.GetFileName(outputPath) + L.T("\n11 bytes alterados  •  SHA-256 confere com a versão testada", "\n11 bytes changed  •  SHA-256 matches the tested version");
            btnOpenFolder.Visible = true; btnDetails.Visible = true; btnDetails.Surface = Theme.OkBg; steps.Current = 2;
            result.Visible = true; result.Invalidate(); steps.Invalidate();
        }

        void OpenFolder()
        {
            try { Process.Start("explorer.exe", "/select,\"" + outputPath + "\""); } catch { }
        }

        void ShowHelpFlash()
        {
            new InfoDialog(L.T("Como gravar na pedaleira", "How to flash the pedal"), L.T(
                "1. Faça backup dos seus presets no app GP-200 (Patch List > exportar). A atualização apaga todos os presets do usuário.\n\n" +
                "2. Conecte a GP-200 ao computador pelo USB e abra o app GP-200.\n\n" +
                "3. Clique na chave inglesa (Settings) > Firmware Update.\n\n" +
                "4. Selecione o arquivo gerado (_TAPGLOBAL.bin) e clique em Update Now.\n\n" +
                "5. Aguarde até o fim. Não desligue a pedaleira nem o cabo durante a atualização.\n\n" +
                "6. Restaure seus presets pelo app, se necessário.\n\n" +
                "Dica: para o delay seguir o tempo, deixe o parâmetro Sync ligado no preset.",
                "1. Back up your presets in the GP-200 app (Patch List > export). Updating erases all user presets.\n\n" +
                "2. Connect the GP-200 to the computer via USB and open the GP-200 app.\n\n" +
                "3. Click the wrench icon (Settings) > Firmware Update.\n\n" +
                "4. Select the generated file (_TAPGLOBAL.bin) and click Update Now.\n\n" +
                "5. Wait until it finishes. Do not turn off the pedal or unplug the cable during the update.\n\n" +
                "6. Restore your presets in the app if needed.\n\n" +
                "Tip: for the delay to follow the tempo, keep the Sync parameter on in the preset."), false).ShowDialog(this);
        }

        void ShowHelpRestore()
        {
            new InfoDialog(L.T("Voltar ao firmware oficial", "Back to the official firmware"), L.T(
                "A gravação do firmware é feita por um modo de atualização próprio da pedaleira, que não é alterado por este patch.\n\n" +
                "Para entrar nele manualmente (mesmo que o firmware não inicie):\n\n" +
                "1. Desligue a fonte e deixe o cabo USB ligado ao computador.\n" +
                "2. Segure os footswitches 2 e 3 (\"+\" e \"CTRL\") e o knob Para.\n" +
                "3. Ainda segurando, ligue a fonte e aperte o botão liga/desliga.\n" +
                "4. Aparece a tela \"Firmware Update/Restore\".\n" +
                "5. No app GP-200, vá em Settings > Firmware Update e grave o .bin OFICIAL.\n\n" +
                "Para sair do modo sem gravar nada, basta reiniciar a pedaleira.",
                "Firmware flashing is done by the pedal's own update mode, which this patch does not change.\n\n" +
                "To enter it manually (even if the firmware does not boot):\n\n" +
                "1. Unplug the power supply and keep the USB cable connected to the computer.\n" +
                "2. Hold footswitches 2 and 3 (\"+\" and \"CTRL\") and the Para knob.\n" +
                "3. While holding them, plug in the power supply and press the power switch.\n" +
                "4. The \"Firmware Update/Restore\" screen appears.\n" +
                "5. In the GP-200 app, go to Settings > Firmware Update and flash the OFFICIAL .bin.\n\n" +
                "To leave this mode without flashing anything, just restart the pedal."), false).ShowDialog(this);
        }

        void ShowHelpWhat()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(L.T("No firmware original, o BPM fica salvo dentro de cada preset. Ao trocar de preset, o tempo que você bateu se perde.",
                              "In the original firmware, the BPM is stored inside each preset. When you switch presets, the tempo you tapped is lost."));
            sb.AppendLine();
            sb.AppendLine(L.T("Com o Tap Tempo Global:", "With Global Tap Tempo:"));
            sb.AppendLine(L.T("•  O tap atualiza o BPM global, que vale para todos os presets.", "•  Tapping updates the global BPM, which applies to all presets."));
            sb.AppendLine(L.T("•  Ao trocar de preset, o tempo atual é mantido.", "•  Switching presets keeps the current tempo."));
            sb.AppendLine(L.T("•  O tempo continua valendo depois de desligar e ligar.", "•  The tempo is kept after turning the pedal off and on."));
            sb.AppendLine(L.T("•  O BPM em Global Settings controla o tempo de todos os presets.", "•  The BPM in Global Settings controls the tempo of all presets."));
            sb.AppendLine(L.T("•  O preset não fica marcado como editado na troca.", "•  The preset is not marked as edited when switching."));
            sb.AppendLine();
            sb.AppendLine(L.T("São 11 bytes alterados no firmware (10 de código + o checksum):", "11 bytes are changed in the firmware (10 of code + the checksum):"));
            foreach (PatchEntry e in Patcher.Entries) sb.AppendLine(string.Format("   0x{0:X5}  {1}", e.Offset, e.Description));
            new InfoDialog(L.T("O que muda?", "What changes?"), sb.ToString(), false).ShowDialog(this);
        }
    }
}
