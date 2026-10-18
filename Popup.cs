using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexLimitTray;

internal sealed class Popup : Form
{
    private readonly FlowLayoutPanel cards = new() { AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Palette.Background };
    private readonly SmoothLabel status = new() { AutoSize = false, ForeColor = Palette.Muted };
    private readonly Button refresh, mode;
    private readonly ModernToggle startup = new() { BackColor = Palette.Background };
    private bool settingStartup;
    private string subtitle = L10n.T("Codex oturumuna bağlanıyor");
    private bool live;
    private string contentKey = "";
    public Popup(Action refreshAction, Action startupAction, Action modeAction, Action? settingsAction = null)
    {
        Text = "CLT · CodexLimitTray";
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; ClientSize = new Size(420, 560);
        BackColor = Palette.Background; ForeColor = Palette.Text; Font = Palette.Font(10);
        DoubleBuffered = true; KeyPreview = true;
        Padding = new Padding(1);
        Icon = TrayDrawing.Create(100);
        var close = MakeButton("×", () => Hide());
        close.SetBounds(365, 23, 32, 32);
        close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(close);
        if (settingsAction is not null)
        {
            var options = MakeButton("⚙", settingsAction); options.SetBounds(328, 23, 32, 32);
            options.AccessibleName = L10n.Text("Ayarlar", "Settings"); Controls.Add(options);
        }
        Controls.Add(cards);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 164, BackColor = Palette.Background };
        refresh = MakeButton(L10n.T("Şimdi yenile"), refreshAction);
        mode = MakeButton(L10n.T("Tray: 5 saatlik"), modeAction);
        refresh.SetBounds(24, 10, 178, 40); mode.SetBounds(212, 10, 184, 40);
        startup.SetBounds(25, 61, 370, 35);
        startup.CheckedChanged += (_, _) => { if (!settingStartup) startupAction(); };
        status.SetBounds(24, 112, 372, 43); status.Font = Palette.Font(8.25f);
        footer.Controls.AddRange([refresh, mode, startup, status]); Controls.Add(footer);
        Resize += (_, _) => { cards.SetBounds(24, 108, ClientSize.Width - 36, Math.Max(40, ClientSize.Height - 280)); };
        cards.SetBounds(24, 108, 384, 340);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        Deactivate += (_, _) => Hide();
    }
    private static Button MakeButton(string text, Action click)
    {
        var button = new ModernButton { Text = text, ForeColor = Palette.Text, TabStop = true };
        button.Click += (_, _) => click(); return button;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var round = 2; DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
        var dark = Palette.Light ? 0 : 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        var margins = new Margins { Left = 1, Right = 1, Top = 1, Bottom = 1 };
        DwmExtendFrameIntoClientArea(Handle, ref margins);
        if (Environment.OSVersion.Version.Build < 22000)
        {
            using var shape = ModernShape.Round(ClientRectangle, 20); Region = new Region(shape);
        }
    }
    public void SetState(UsageSnapshot? snapshot, string? error, bool busy, bool weekly, bool fresh)
    {
        live = fresh;
        subtitle = snapshot is null ? L10n.T("Kalan kullanım hakkın") : $"{L10n.T("Kalan kullanım hakkın")}  ·  {snapshot.Main.Plan?.ToUpperInvariant() ?? "CODEX"}";
        refresh.Enabled = !busy; refresh.Text = busy ? L10n.T("Yenileniyor…") : L10n.T("Şimdi yenile");
        var selected = snapshot is null ? null : weekly ? snapshot.Main.Secondary ?? snapshot.Main.Primary : snapshot.Main.Primary ?? snapshot.Main.Secondary;
        mode.Text = selected is not null ? "Tray: " + selected.Duration : weekly ? L10n.T("Tray: haftalık") : L10n.T("Tray: kısa dönem");
        settingStartup = true; startup.Checked = Startup.Enabled; settingStartup = false;
        status.Text = error is not null ? error : snapshot is null ? L10n.T("Codex'ten kullanım verisi alınıyor…") : L10n.T("{0} · Son kontrol {1:HH:mm:ss}\n60 saniyede bir otomatik yenilenir.", fresh ? L10n.T("Güncel") : L10n.T("Eski veri"), snapshot.FetchedAt);
        status.ForeColor = error is not null ? Palette.For(0) : Palette.Muted;
        var key = $"{snapshot?.FetchedAt.Ticks}:{fresh}:{DateTimeOffset.Now.ToUnixTimeSeconds() / 30}";
        if (contentKey != key)
        {
            contentKey = key;
            cards.SuspendLayout();
            while (cards.Controls.Count > 0) { var control = cards.Controls[0]; cards.Controls.RemoveAt(0); control.Dispose(); }
            if (snapshot is not null)
                foreach (var bucket in snapshot.Buckets.OrderBy(b => b.Id == "codex" ? 0 : 1))
                    foreach (var w in new[] { bucket.Primary, bucket.Secondary }.OfType<UsageWindow>())
                        cards.Controls.Add(new WindowCard(w, bucket.Id == "codex" ? null : bucket.Name, fresh));
            else
                cards.Controls.Add(new Label { Text = L10n.T("CLT\n\nKullanım bilgilerin burada görünecek.\nCodex oturumun açık olmalı."), Size = new Size(368, 230), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Palette.Muted, BackColor = Palette.Card });
            cards.ResumeLayout();
        }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; ModernShape.Prepare(g);
        using var title = Palette.Font(13, FontStyle.Regular);
        using var sub = Palette.Font(9);
        using var text = new SolidBrush(Palette.Text);
        using var muted = new SolidBrush(Palette.Muted);
        g.DrawString("CodexLimitTray", title, text, 24, 23);
        var plan = subtitle.Contains('·') ? subtitle.Split('·')[^1].Trim() : "CODEX";
        g.DrawString(L10n.T("Kalan kullanım hakkın") + " · " + plan, sub, muted, 24, 53);
        g.DrawString(live ? L10n.T("Güncel") : L10n.T("Bekleniyor"), sub, muted, 24, 79);
        using var rule = new Pen(Palette.Border);
        g.DrawLine(rule, 24, 102, Width - 24, 102);
    }
    protected override void Dispose(bool disposing) { if (disposing) { Icon?.Dispose(); Font.Dispose(); } base.Dispose(disposing); }
}

internal sealed class WindowCard : Control
{
    private readonly UsageWindow window;
    private readonly string? bucketName;
    private readonly bool fresh;
    public WindowCard(UsageWindow window, string? bucketName, bool fresh)
    {
        this.window = window; this.bucketName = bucketName; this.fresh = fresh;
        Size = new Size(372, bucketName is null ? 128 : 146); Margin = new Padding(0, 0, 0, 8);
        DoubleBuffered = true; BackColor = Palette.Background;
        AccessibleName = L10n.T("{0} {1} yüzde {2} kaldı. Yenilenme {3}", bucketName, window.Duration, window.Remaining, window.ResetText);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; ModernShape.Prepare(g); g.Clear(BackColor);
        using var path = ModernShape.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 7);
        using var fill = new SolidBrush(Palette.Card);
        g.FillPath(fill, path);
        using var border = new Pen(Palette.Border); g.DrawPath(border, path);
        using var small = Palette.Font(8.5f);
        using var big = Palette.Font(25);
        using var label = Palette.Font(10);
        using var muted = new SolidBrush(Palette.Muted);
        using var text = new SolidBrush(Palette.Text);
        using var accent = new SolidBrush(fresh ? Palette.For(window.Remaining) : Palette.Muted);
        var y = 13;
        if (bucketName is not null) { g.DrawString(bucketName, small, muted, 16, y); y += 18; }
        g.DrawString(window.Duration, label, text, 16, y + 5);
        using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        g.DrawString(L10n.Percent(window.Remaining), big, fresh ? text : muted, new RectangleF(160, y - 7, Width - 176, 46), right);
        var bar = new RectangleF(16, y + 43, Width - 32, 4);
        using var track = new SolidBrush(Palette.Track);
        using var trackPath = ModernShape.Round(bar, 2); g.FillPath(track, trackPath);
        if (window.Remaining > 0)
        {
            using var progress = ModernShape.Round(new RectangleF(bar.X, bar.Y, bar.Width * window.Remaining / 100f, bar.Height), 2);
            g.FillPath(accent, progress);
        }
        g.DrawString((fresh ? "" : L10n.T("Eski veri") + " · ") + window.Countdown(DateTimeOffset.Now), small, text, 16, y + 57);
        g.DrawString(L10n.T("Yenilenme  ·  {0}", window.ResetText), small, muted, 16, y + 79);
    }
}
