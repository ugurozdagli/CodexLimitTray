namespace CodexLimitTray;

internal sealed class SettingsForm : Form
{
    public SettingsForm(Settings settings, Action apply, Action checkUpdates, bool notificationIdentityAvailable = true, bool? previewStartup = null)
    {
        Text = L10n.Text("CLT · Ayarlar", "CLT · Settings");
        Font = Palette.Font(10); BackColor = Palette.Background; ForeColor = Palette.Text;
        FormBorderStyle = FormBorderStyle.None; MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false; StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(570, 650); AutoScaleMode = AutoScaleMode.Dpi;
        var heading = new SmoothLabel { Text = L10n.Text("Ayarlar", "Settings") + " · v" + AppInfo.Version, Font = Palette.Font(14, FontStyle.Bold), ForeColor = Palette.Text };
        heading.SetBounds(24, 18, 470, 34); heading.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) WindowChrome.Drag(this); }; Controls.Add(heading);
        var close = new ModernButton { Text = "×", Size = new Size(32, 32), Location = new Point(514, 18), DialogResult = DialogResult.Cancel }; Controls.Add(close);
        close.Click += (_, _) => Close();
        close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        var body = new Panel { Location = new Point(0, 64), Size = new Size(570, 524), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, AutoScroll = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(24, 0, 24, 0), AutoSize = true, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        body.Controls.Add(layout); Controls.Add(body);
        var view = Choice([L10n.T("Basit"), L10n.T("Detaylı")], settings.WidgetDetailed ? 1 : 0);
        var theme = Choice([L10n.Text("Sistem", "System"), L10n.Text("Koyu", "Dark"), L10n.Text("Açık", "Light")], (int)settings.Theme);
        var textSize = Choice([L10n.Text("Küçük", "Small"), L10n.Text("Normal", "Normal"), L10n.Text("Büyük", "Large")], settings.TextScale < .99f ? 0 : settings.TextScale > 1.01f ? 2 : 1);
        var reset = Choice([L10n.Text("Tarih ve saat", "Date and time"), L10n.Text("Geri sayım", "Countdown"), L10n.Text("Gizli", "Hidden")], (int)settings.ResetDisplay);
        var displays = new List<DisplayChoice> { new("", L10n.Text("Ana ekranı izle", "Follow primary display"), "") };
        displays.AddRange(DisplayCatalog.GetDisplays());
        if (!string.IsNullOrEmpty(settings.DisplayKey) && displays.All(d => d.Key != settings.DisplayKey))
            displays.Add(new(settings.DisplayKey, L10n.Text("Bağlı olmayan kayıtlı ekran", "Saved disconnected display"), ""));
        var monitor = Choice(displays.Cast<object>().ToArray(), Math.Max(0, displays.FindIndex(d => d.Key == (settings.DisplayKey ?? ""))));
        var low = Toggle(settings.LowQuotaAlerts);
        var recovery = Toggle(settings.ReplenishmentAlerts);
        var warning = Number(settings.WarningThreshold, 2, 99);
        var critical = Number(settings.CriticalThreshold, 1, 98);
        var startup = Toggle(previewStartup ?? Startup.Enabled);
        Row(L10n.Text("Gösterge görünümü", "Widget view"), view);
        Row(L10n.Text("Panel teması", "Panel theme"), theme);
        Row(L10n.Text("Gösterge yazı boyutu", "Widget text size"), textSize);
        Row(L10n.Text("Detaylı görünümde yenilenme", "Reset in Detailed view"), reset);
        Row(L10n.Text("Gösterilecek ekran", "Display"), monitor);
        Row(L10n.Text("Düşük kota bildirimi", "Low-quota notifications"), low);
        Row(L10n.Text("Uyarı · kalan %", "Warning · remaining %"), warning);
        Row(L10n.Text("Kritik · kalan %", "Critical · remaining %"), critical);
        Row(L10n.Text("Kota yenilendi bildirimi", "Quota replenished notification"), recovery);
        Row(L10n.T("Windows ile başlat"), startup);
        var help = new SmoothLabel
        {
            Text = L10n.Text("Bildirimler ilk okumada sessizdir; her eşik ve limit için bir kez gösterilir. Yenilenme, Codex tarafından doğrulanmalıdır.\nSeçilen ekranda görev çubuğu yoksa ana ekran kullanılır.", "Notifications start silently and appear once per threshold and window. Codex must confirm replenishment.\nUses the primary display if the selected display has no taskbar.") +
                (notificationIdentityAvailable ? "" : "\n" + L10n.Text("Codex'ten bildirimler için gerekli bilgi bekleniyor. Eski sürümlerde Codex'i güncellemek gerekebilir.", "Waiting for the information needed for notifications. Older Codex versions may need an update.")),
            Dock = DockStyle.Top, Height = 94, ForeColor = Palette.Muted, Margin = new Padding(0, 12, 0, 0), Font = Palette.Font(8.5f)
        };
        layout.Controls.Add(help, 0, layout.RowCount); layout.SetColumnSpan(help, 2); layout.RowCount++;
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12, 8, 12, 8) };
        var save = new ModernButton { Text = L10n.Text("Kaydet", "Save"), Primary = true, Size = new Size(100, 36) };
        var cancel = new ModernButton { Text = L10n.Text("İptal", "Cancel"), Size = new Size(100, 36), DialogResult = DialogResult.Cancel };
        cancel.Click += (_, _) => Close();
        var update = new ModernButton { Text = L10n.Text("Güncellemeleri kontrol et", "Check for updates"), Size = new Size(200, 36) };
        update.Click += (_, _) => checkUpdates();
        save.Click += (_, _) =>
        {
            if (critical.Value >= warning.Value)
            {
                MessageBox.Show(this, L10n.Text("Kritik eşik uyarı eşiğinden küçük olmalı.", "The critical threshold must be lower than the warning threshold."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }
            try { Startup.Set(startup.Checked); }
            catch { MessageBox.Show(this, L10n.T("Windows başlangıç ayarı kaydedilemedi."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            settings.WidgetDetailed = view.SelectedIndex == 1;
            settings.Theme = (ThemeMode)theme.SelectedIndex;
            settings.TextScale = new[] { .85f, 1f, 1.2f }[textSize.SelectedIndex];
            settings.ResetDisplay = (ResetDisplay)reset.SelectedIndex;
            settings.DisplayKey = ((DisplayChoice)monitor.SelectedItem!).Key;
            settings.LowQuotaAlerts = low.Checked; settings.ReplenishmentAlerts = recovery.Checked;
            settings.WarningThreshold = (int)warning.Value; settings.CriticalThreshold = (int)critical.Value;
            apply(); DialogResult = DialogResult.OK; Close();
        };
        footer.Controls.AddRange([save, cancel, update]); Controls.Add(footer);
        AcceptButton = save; CancelButton = cancel;

        void Row(string label, Control control)
        {
            var row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.Controls.Add(new SmoothLabel { Text = label, Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0), ForeColor = Palette.Text, Font = Palette.Font(9) }, 0, row);
            control.AccessibleName = label;
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 3, 0, 3); layout.Controls.Add(control, 1, row);
        }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); WindowChrome.Apply(this); }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Height = Math.Min(Height, Math.Max(300, Screen.FromControl(this).WorkingArea.Height - 24));
        if (Location.X > -10000 && Location.Y > -10000) CenterToScreen();
    }
    private static ModernSelect Choice(object[] values, int selected)
    {
        var box = new ModernSelect { BackColor = Palette.Background, ForeColor = Palette.Text };
        box.Items.AddRange(values); box.SelectedIndex = selected; return box;
    }
    private static CheckBox Toggle(bool value) => new ModernToggle { Checked = value, Text = "", BackColor = Palette.Background };
    private static NumericUpDown Number(int value, int min, int max) => new() { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), BackColor = Palette.Card, ForeColor = Palette.Text, BorderStyle = BorderStyle.FixedSingle };
}
