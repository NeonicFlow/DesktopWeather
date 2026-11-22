using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DesktopWeather.Models;
using DesktopWeather.Views;

namespace DesktopWeather.Services;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly WeatherSettings _settings;
    private readonly SettingsWindow _settingsWindow;
    private readonly ContextMenuStrip _contextMenu;

    private readonly ToolStripMenuItem _titleItem;
    private readonly ToolStripMenuItem _itemSnow;
    private readonly ToolStripMenuItem _itemRain;
    private readonly ToolStripMenuItem _itemStorm;
    private readonly ToolStripMenuItem _itemSakura;
    private readonly ToolStripMenuItem _itemLeaves;
    private readonly ToolStripMenuItem _itemFireflies;
    private readonly ToolStripMenuItem _itemMist;
    private readonly ToolStripMenuItem _itemFire;
    private readonly ToolStripMenuItem _itemWaterDrops;
    private readonly ToolStripMenuItem _itemWind;
    private readonly ToolStripMenuItem _itemSound;
    private readonly ToolStripMenuItem _itemPause;
    private readonly ToolStripMenuItem _itemSettings;
    private readonly ToolStripMenuItem _itemShortcut;
    private readonly ToolStripMenuItem _itemExit;

    public TrayIconManager(WeatherSettings settings, SettingsWindow settingsWindow)
    {
        _settings = settings;
        _settingsWindow = settingsWindow;

        _contextMenu = new ContextMenuStrip();

        // Header
        _titleItem = new ToolStripMenuItem("🌦️ Погода на экране") { Enabled = false };
        _titleItem.Font = new Font(_titleItem.Font, FontStyle.Bold);
        _contextMenu.Items.Add(_titleItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Weather items
        _itemSnow = new ToolStripMenuItem("❄️ Снег", null, (s, e) => SwitchWeather(WeatherType.Snow));
        _itemRain = new ToolStripMenuItem("🌧️ Дождь", null, (s, e) => SwitchWeather(WeatherType.Rain));
        _itemStorm = new ToolStripMenuItem("⚡ Гроза", null, (s, e) => SwitchWeather(WeatherType.Storm));
        _itemSakura = new ToolStripMenuItem("🌸 Сакура", null, (s, e) => SwitchWeather(WeatherType.Sakura));
        _itemLeaves = new ToolStripMenuItem("🍁 Осенний листопад", null, (s, e) => SwitchWeather(WeatherType.Leaves));
        _itemFireflies = new ToolStripMenuItem("✨ Светлячки", null, (s, e) => SwitchWeather(WeatherType.Fireflies));
        _itemMist = new ToolStripMenuItem("☁️ Туман", null, (s, e) => SwitchWeather(WeatherType.Mist));
        _itemFire = new ToolStripMenuItem("🔥 Огонь", null, (s, e) => SwitchWeather(WeatherType.Fire));
        _itemWaterDrops = new ToolStripMenuItem("💧 Капли", null, (s, e) => SwitchWeather(WeatherType.WaterDrops));
        _itemWind = new ToolStripMenuItem("💨 Ветер", null, (s, e) => SwitchWeather(WeatherType.Wind));

        _contextMenu.Items.Add(_itemSnow);
        _contextMenu.Items.Add(_itemRain);
        _contextMenu.Items.Add(_itemStorm);
        _contextMenu.Items.Add(_itemSakura);
        _contextMenu.Items.Add(_itemLeaves);
        _contextMenu.Items.Add(_itemFireflies);
        _contextMenu.Items.Add(_itemMist);
        _contextMenu.Items.Add(_itemFire);
        _contextMenu.Items.Add(_itemWaterDrops);
        _contextMenu.Items.Add(_itemWind);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Sound toggle
        _itemSound = new ToolStripMenuItem("🔊 Звук", null, OnToggleSound);
        _contextMenu.Items.Add(_itemSound);

        // Pause
        _itemPause = new ToolStripMenuItem("⏸️ Приостановить", null, OnTogglePause);
        _contextMenu.Items.Add(_itemPause);

        // Settings
        _itemSettings = new ToolStripMenuItem("⚙️ Настройки...", null, (s, e) => ShowSettings());
        _itemSettings.Font = new Font(_itemSettings.Font, FontStyle.Bold);
        _contextMenu.Items.Add(_itemSettings);

        // Shortcut
        _itemShortcut = new ToolStripMenuItem("📌 Создать ярлык на рабочем столе", null, OnCreateShortcut);
        _contextMenu.Items.Add(_itemShortcut);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        _itemExit = new ToolStripMenuItem("❌ Выход", null, (s, e) => ExitApplication());
        _contextMenu.Items.Add(_itemExit);

        // Initialize NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadOrGenerateIcon(),
            Text = AppStrings.Get("WindowTitle", _settings.Language),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowSettings();
            }
        };

        UpdateMenuChecks();

        _settings.OnChanged += UpdateMenuChecks;
    }

    private void SwitchWeather(WeatherType type)
    {
        _settings.SwitchWeather(type);
        _settingsWindow.LoadSettingsToUi();
        UpdateMenuChecks();
    }

    private void OnToggleSound(object? sender, EventArgs e)
    {
        _settings.EnableSound = !_settings.EnableSound;
        _settings.NotifyChanged();
        _settings.Save();
        _settingsWindow.LoadSettingsToUi();
        UpdateMenuChecks();
    }

    private void OnTogglePause(object? sender, EventArgs e)
    {
        _settings.IsPaused = !_settings.IsPaused;
        _settings.NotifyChanged();
        _settings.Save();
        _settingsWindow.LoadSettingsToUi();
        UpdateMenuChecks();
    }

    public void UpdateMenuChecks()
    {
        var lang = _settings.Language;

        _titleItem.Text = AppStrings.Get("TrayTitle", lang);
        _itemSnow.Text = "❄️ " + AppStrings.Get("Snow", lang);
        _itemRain.Text = "🌧️ " + AppStrings.Get("Rain", lang);
        _itemStorm.Text = "⚡ " + AppStrings.Get("Storm", lang);
        _itemSakura.Text = "🌸 " + AppStrings.Get("Sakura", lang);
        _itemLeaves.Text = "🍁 " + AppStrings.Get("Leaves", lang);
        _itemFireflies.Text = "✨ " + AppStrings.Get("Fireflies", lang);
        _itemMist.Text = "☁️ " + AppStrings.Get("Mist", lang);
        _itemFire.Text = "🔥 " + AppStrings.Get("Fire", lang);
        _itemWaterDrops.Text = "💧 " + AppStrings.Get("WaterDrops", lang);
        _itemWind.Text = "💨 " + AppStrings.Get("Wind", lang);

        _itemSnow.Checked = _settings.WeatherType == WeatherType.Snow;
        _itemRain.Checked = _settings.WeatherType == WeatherType.Rain;
        _itemStorm.Checked = _settings.WeatherType == WeatherType.Storm;
        _itemSakura.Checked = _settings.WeatherType == WeatherType.Sakura;
        _itemLeaves.Checked = _settings.WeatherType == WeatherType.Leaves;
        _itemFireflies.Checked = _settings.WeatherType == WeatherType.Fireflies;
        _itemMist.Checked = _settings.WeatherType == WeatherType.Mist;
        _itemFire.Checked = _settings.WeatherType == WeatherType.Fire;
        _itemWaterDrops.Checked = _settings.WeatherType == WeatherType.WaterDrops;
        _itemWind.Checked = _settings.WeatherType == WeatherType.Wind;

        _itemSound.Text = _settings.EnableSound ? AppStrings.Get("TraySoundOn", lang) : AppStrings.Get("TraySoundOff", lang);
        _itemPause.Text = _settings.IsPaused ? AppStrings.Get("ResumeBtn", lang) : AppStrings.Get("PauseBtn", lang);
        _itemSettings.Text = AppStrings.Get("TraySettings", lang);
        _itemShortcut.Text = AppStrings.Get("ShortcutTray", lang);
        _itemExit.Text = AppStrings.Get("TrayExit", lang);

        string title = AppStrings.Get("WindowTitle", lang);
        _notifyIcon.Text = title.Length > 63 ? title[..63] : title;
    }

    public void ShowSettings()
    {
        _settingsWindow.Show();
        _settingsWindow.WindowState = System.Windows.WindowState.Normal;
        _settingsWindow.Activate();
    }

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        System.Windows.Application.Current.Shutdown();
    }

    private static Icon LoadOrGenerateIcon()
    {
        try
        {
            if (!string.IsNullOrEmpty(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
            {
                var exeIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                if (exeIcon != null) return exeIcon;
            }

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }
        }
        catch { }

        // Fallback procedural high-contrast icon
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(25, 27, 34));
            g.FillEllipse(bgBrush, 1, 1, 30, 30);
            using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 85, 100), 1.5f);
            g.DrawEllipse(borderPen, 1, 1, 30, 30);

            using var cloudBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 255, 255));
            g.FillEllipse(cloudBrush, 6, 12, 12, 11);
            g.FillEllipse(cloudBrush, 12, 7, 13, 13);
            g.FillEllipse(cloudBrush, 17, 12, 9, 10);
            g.FillRectangle(cloudBrush, 10, 14, 12, 8);

            using var boltBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 195, 30));
            System.Drawing.PointF[] bolt = new System.Drawing.PointF[]
            {
                new System.Drawing.PointF(16, 15),
                new System.Drawing.PointF(12, 22),
                new System.Drawing.PointF(16, 22),
                new System.Drawing.PointF(14, 28),
                new System.Drawing.PointF(20, 20),
                new System.Drawing.PointF(16, 20)
            };
            g.FillPolygon(boltBrush, bolt);
        }

        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private void OnCreateShortcut(object? sender, EventArgs e)
    {
        bool success = ShortcutHelper.CreateDesktopShortcut(out string msg);
        if (success)
        {
            ShowBalloon("Desktop Weather", AppStrings.Get("ShortcutCreated", _settings.Language));
        }
        else
        {
            System.Windows.Forms.MessageBox.Show(msg, "Desktop Weather", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _itemShortcut.Dispose();
        _contextMenu.Dispose();
    }
}
