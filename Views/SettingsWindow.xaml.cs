using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using DesktopWeather.Models;
using DesktopWeather.Services;

namespace DesktopWeather.Views;

public partial class SettingsWindow : Window
{
    private readonly WeatherSettings _settings;
    private readonly WeatherOverlayWindow _overlay;
    private bool _isUpdatingUi = true;

    private static readonly int[] FpsPresets = new[] { 30, 60, 120, 144, 0 };

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public SettingsWindow(WeatherSettings settings, WeatherOverlayWindow overlay)
    {
        _settings = settings;
        _overlay = overlay;
        _isUpdatingUi = true;

        InitializeComponent();

        _isUpdatingUi = false;
        LoadSettingsToUi();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
            DwmSetWindowAttribute(hwnd, 19, ref darkMode, sizeof(int));

            // Single instance restore hook
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == App.CustomShowMessage)
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void LoadSettingsToUi()
    {
        if (_settings == null) return;
        _isUpdatingUi = true;

        try
        {
            // Language radio buttons
            if (BtnLangRu != null) BtnLangRu.IsChecked = _settings.Language == AppLanguage.Russian;
            if (BtnLangEn != null) BtnLangEn.IsChecked = _settings.Language == AppLanguage.English;

            // Apply all localized texts
            ApplyLanguageToUi();

            // Weather type radio buttons (7 types without sunny)
            if (BtnSnow != null) BtnSnow.IsChecked = _settings.WeatherType == WeatherType.Snow;
            if (BtnRain != null) BtnRain.IsChecked = _settings.WeatherType == WeatherType.Rain;
            if (BtnStorm != null) BtnStorm.IsChecked = _settings.WeatherType == WeatherType.Storm;
            if (BtnSakura != null) BtnSakura.IsChecked = _settings.WeatherType == WeatherType.Sakura;
            if (BtnLeaves != null) BtnLeaves.IsChecked = _settings.WeatherType == WeatherType.Leaves;
            if (BtnFireflies != null) BtnFireflies.IsChecked = _settings.WeatherType == WeatherType.Fireflies;
            if (BtnMist != null) BtnMist.IsChecked = _settings.WeatherType == WeatherType.Mist;
            if (BtnFire != null) BtnFire.IsChecked = _settings.WeatherType == WeatherType.Fire;
            if (BtnWaterDrops != null) BtnWaterDrops.IsChecked = _settings.WeatherType == WeatherType.WaterDrops;
            if (BtnWind != null) BtnWind.IsChecked = _settings.WeatherType == WeatherType.Wind;

            // Conditional cards visibility
            if (CardSnowStyle != null)
                CardSnowStyle.Visibility = _settings.WeatherType == WeatherType.Snow ? Visibility.Visible : Visibility.Collapsed;
            if (CardStormStyle != null)
                CardStormStyle.Visibility = _settings.WeatherType == WeatherType.Storm ? Visibility.Visible : Visibility.Collapsed;

            // Snow style buttons
            if (BtnSnowBoth != null) BtnSnowBoth.IsChecked = _settings.SnowStyle == SnowStyle.Both;
            if (BtnSnowCrystals != null) BtnSnowCrystals.IsChecked = _settings.SnowStyle == SnowStyle.Crystals;
            if (BtnSnowFlakes != null) BtnSnowFlakes.IsChecked = _settings.SnowStyle == SnowStyle.Flakes;

            // Storm specific toggle
            if (ChkStormClouds != null) ChkStormClouds.IsChecked = _settings.EnableStormClouds;

            // Color filter dropdown
            if (CmbColorFilter != null)
            {
                int filterIndex = (int)_settings.ColorFilter;
                if (filterIndex >= 0 && filterIndex < CmbColorFilter.Items.Count)
                {
                    CmbColorFilter.SelectedIndex = filterIndex;
                }
            }

            if (SliderFilterIntensity != null) SliderFilterIntensity.Value = _settings.FilterIntensity;
            if (LblFilterIntensity != null) LblFilterIntensity.Text = $"{(int)Math.Round(_settings.FilterIntensity * 100)}%";

            // Sliders
            if (SliderCount != null) SliderCount.Value = _settings.ParticleCount;
            if (LblCount != null) LblCount.Text = _settings.ParticleCount.ToString();

            if (SliderSpeed != null) SliderSpeed.Value = _settings.Speed;
            if (LblSpeed != null) LblSpeed.Text = $"{_settings.Speed:0.0}x";

            if (SliderWind != null) SliderWind.Value = _settings.Wind;
            UpdateWindLabel();

            if (SliderScale != null) SliderScale.Value = _settings.ParticleScale;
            if (LblScale != null) LblScale.Text = $"{_settings.ParticleScale:0.0}x";

            if (SliderOpacity != null) SliderOpacity.Value = _settings.Opacity;
            if (LblOpacity != null) LblOpacity.Text = $"{(int)Math.Round(_settings.Opacity * 100)}%";

            // FPS
            if (SliderFps != null) SliderFps.Value = FpsToIndex(_settings.TargetFps);
            if (LblFps != null) LblFps.Text = GetFpsLabel(_settings.TargetFps);

            // Audio
            if (ChkSound != null) ChkSound.IsChecked = _settings.EnableSound;
            if (SliderVolume != null) SliderVolume.Value = _settings.SoundVolume;
            if (LblVolume != null) LblVolume.Text = $"{(int)Math.Round(_settings.SoundVolume * 100)}%";

            // Checkboxes
            if (ChkClickThrough != null) ChkClickThrough.IsChecked = _settings.ClickThrough;
            if (ChkAutostart != null) ChkAutostart.IsChecked = _settings.StartWithWindows;

            // Pause button
            if (BtnPause != null) BtnPause.Content = _settings.IsPaused
                ? AppStrings.Get("ResumeBtn", _settings.Language)
                : AppStrings.Get("PauseBtn", _settings.Language);
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void ApplyLanguageToUi()
    {
        var lang = _settings.Language;

        Title = AppStrings.Get("WindowTitle", lang);
        if (TxtTitleBar != null) TxtTitleBar.Text = AppStrings.Get("WindowTitle", lang);
        if (TxtHeaderTitle != null) TxtHeaderTitle.Text = AppStrings.Get("HeaderTitle", lang);
        if (TxtHeaderSubtitle != null) TxtHeaderSubtitle.Text = AppStrings.Get("HeaderSubtitle", lang);

        if (TxtWeatherSection != null) TxtWeatherSection.Text = AppStrings.Get("WeatherSection", lang);
        if (TxtSnow != null) TxtSnow.Text = AppStrings.Get("Snow", lang);
        if (TxtRain != null) TxtRain.Text = AppStrings.Get("Rain", lang);
        if (TxtStorm != null) TxtStorm.Text = AppStrings.Get("Storm", lang);
        if (TxtSakura != null) TxtSakura.Text = AppStrings.Get("Sakura", lang);
        if (TxtLeaves != null) TxtLeaves.Text = AppStrings.Get("Leaves", lang);
        if (TxtFireflies != null) TxtFireflies.Text = AppStrings.Get("Fireflies", lang);
        if (TxtMist != null) TxtMist.Text = AppStrings.Get("Mist", lang);
        if (TxtFire != null) TxtFire.Text = AppStrings.Get("Fire", lang);
        if (TxtWaterDrops != null) TxtWaterDrops.Text = AppStrings.Get("WaterDrops", lang);
        if (TxtWind != null) TxtWind.Text = AppStrings.Get("Wind", lang);

        if (TxtSnowStyleLabel != null) TxtSnowStyleLabel.Text = AppStrings.Get("SnowStyleLabel", lang);
        if (BtnSnowBoth != null) BtnSnowBoth.Content = AppStrings.Get("SnowBoth", lang);
        if (BtnSnowCrystals != null) BtnSnowCrystals.Content = AppStrings.Get("SnowCrystals", lang);
        if (BtnSnowFlakes != null) BtnSnowFlakes.Content = AppStrings.Get("SnowFlakes", lang);

        if (ChkStormClouds != null) ChkStormClouds.Content = AppStrings.Get("StormClouds", lang);

        if (TxtColorSection != null) TxtColorSection.Text = AppStrings.Get("ColorSection", lang);
        if (TxtColorFilterLabel != null) TxtColorFilterLabel.Text = AppStrings.Get("ColorFilterLabel", lang);
        if (TxtFilterIntensityLabel != null) TxtFilterIntensityLabel.Text = AppStrings.Get("TintIntensityLabel", lang);

        if (CmbItemOff != null) CmbItemOff.Content = AppStrings.Get("FilterOff", lang);
        if (CmbItemAuto != null) CmbItemAuto.Content = AppStrings.Get("FilterAuto", lang);
        if (CmbItemSnow != null) CmbItemSnow.Content = AppStrings.Get("FilterSnow", lang);
        if (CmbItemRain != null) CmbItemRain.Content = AppStrings.Get("FilterRain", lang);
        if (CmbItemStorm != null) CmbItemStorm.Content = AppStrings.Get("FilterStorm", lang);
        if (CmbItemSakura != null) CmbItemSakura.Content = AppStrings.Get("FilterSakura", lang);
        if (CmbItemLeaves != null) CmbItemLeaves.Content = AppStrings.Get("FilterLeaves", lang);
        if (CmbItemFireflies != null) CmbItemFireflies.Content = AppStrings.Get("FilterFireflies", lang);
        if (CmbItemMist != null) CmbItemMist.Content = AppStrings.Get("FilterMist", lang);
        if (CmbItemFire != null) CmbItemFire.Content = AppStrings.Get("FilterFire", lang);
        if (CmbItemWaterDrops != null) CmbItemWaterDrops.Content = AppStrings.Get("FilterWaterDrops", lang);
        if (CmbItemWind != null) CmbItemWind.Content = AppStrings.Get("FilterWind", lang);

        if (TxtParticlesSection != null) TxtParticlesSection.Text = AppStrings.Get("ParticlesSection", lang);
        if (TxtCountLabel != null) TxtCountLabel.Text = AppStrings.Get("CountLabel", lang);
        if (TxtSpeedLabel != null) TxtSpeedLabel.Text = AppStrings.Get("SpeedLabel", lang);
        if (TxtWindLabel != null) TxtWindLabel.Text = AppStrings.Get("WindLabel", lang);
        if (TxtScaleLabel != null) TxtScaleLabel.Text = AppStrings.Get("ScaleLabel", lang);
        if (TxtOpacityLabel != null) TxtOpacityLabel.Text = AppStrings.Get("OpacityLabel", lang);

        if (ChkSound != null) ChkSound.Content = AppStrings.Get("SoundLabel", lang);
        if (TxtFpsLabel != null) TxtFpsLabel.Text = AppStrings.Get("FpsLabel", lang);
        if (LblFps != null) LblFps.Text = GetFpsLabel(_settings.TargetFps);

        if (ChkClickThrough != null) ChkClickThrough.Content = AppStrings.Get("ClickThrough", lang);
        if (ChkAutostart != null) ChkAutostart.Content = AppStrings.Get("Autostart", lang);
        if (BtnCreateShortcut != null) BtnCreateShortcut.Content = AppStrings.Get("CreateShortcutBtn", lang);

        if (BtnReset != null) BtnReset.Content = AppStrings.Get("ResetBtn", lang);
        if (BtnPause != null) BtnPause.Content = _settings.IsPaused
            ? AppStrings.Get("ResumeBtn", lang)
            : AppStrings.Get("PauseBtn", lang);
        if (BtnMinimize != null) BtnMinimize.Content = AppStrings.Get("MinimizeBtn", lang);

        if (TxtAuthor != null) TxtAuthor.Text = AppStrings.Get("Author", lang);
        if (TxtTrayTip != null) TxtTrayTip.Text = AppStrings.Get("TrayTip", lang);

        UpdateWindLabel();
    }

    private void UpdateWindLabel()
    {
        if (LblWind == null || _settings == null) return;
        double w = Math.Round(_settings.Wind, 1);
        var lang = _settings.Language;
        LblWind.Text = Math.Abs(w) < 0.05
            ? AppStrings.Get("WindCalm", lang)
            : (w > 0
                ? string.Format(AppStrings.Get("WindRight", lang), w)
                : string.Format(AppStrings.Get("WindLeft", lang), Math.Abs(w)));
    }

    private void OnLanguageChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null) return;
        _settings.Language = BtnLangEn?.IsChecked == true ? AppLanguage.English : AppLanguage.Russian;
        ApplyLanguageToUi();
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnWeatherChecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null) return;

        WeatherType selected = WeatherType.Snow;
        if (BtnRain?.IsChecked == true) selected = WeatherType.Rain;
        else if (BtnStorm?.IsChecked == true) selected = WeatherType.Storm;
        else if (BtnSakura?.IsChecked == true) selected = WeatherType.Sakura;
        else if (BtnLeaves?.IsChecked == true) selected = WeatherType.Leaves;
        else if (BtnFireflies?.IsChecked == true) selected = WeatherType.Fireflies;
        else if (BtnMist?.IsChecked == true) selected = WeatherType.Mist;
        else if (BtnFire?.IsChecked == true) selected = WeatherType.Fire;
        else if (BtnWaterDrops?.IsChecked == true) selected = WeatherType.WaterDrops;
        else if (BtnWind?.IsChecked == true) selected = WeatherType.Wind;

        _settings.SwitchWeather(selected);
        LoadSettingsToUi();
    }

    private void OnSnowStyleChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null) return;

        if (BtnSnowCrystals?.IsChecked == true) _settings.SnowStyle = SnowStyle.Crystals;
        else if (BtnSnowFlakes?.IsChecked == true) _settings.SnowStyle = SnowStyle.Flakes;
        else _settings.SnowStyle = SnowStyle.Both;

        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnStormCloudsChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null) return;
        _settings.EnableStormClouds = ChkStormClouds?.IsChecked == true;
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnColorFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null || CmbColorFilter == null) return;

        if (CmbColorFilter.SelectedIndex >= 0)
        {
            _settings.ColorFilter = (ColorFilterMode)CmbColorFilter.SelectedIndex;
            _settings.NotifyChanged();
            _settings.Save();
        }
    }

    private void OnFilterIntensityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderFilterIntensity == null || LblFilterIntensity == null) return;
        _settings.FilterIntensity = Math.Round(SliderFilterIntensity.Value, 2);
        LblFilterIntensity.Text = $"{(int)Math.Round(_settings.FilterIntensity * 100)}%";
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderCount == null || LblCount == null) return;
        _settings.ParticleCount = (int)Math.Round(SliderCount.Value);
        LblCount.Text = _settings.ParticleCount.ToString();
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderSpeed == null || LblSpeed == null) return;
        _settings.Speed = Math.Round(SliderSpeed.Value, 1);
        LblSpeed.Text = $"{_settings.Speed:0.0}x";
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnWindChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderWind == null || LblWind == null) return;
        _settings.Wind = Math.Round(SliderWind.Value, 1);
        UpdateWindLabel();
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderScale == null || LblScale == null) return;
        _settings.ParticleScale = Math.Round(SliderScale.Value, 1);
        LblScale.Text = $"{_settings.ParticleScale:0.0}x";
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderOpacity == null || LblOpacity == null) return;
        _settings.Opacity = Math.Round(SliderOpacity.Value, 2);
        LblOpacity.Text = $"{(int)Math.Round(_settings.Opacity * 100)}%";
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnFpsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderFps == null || LblFps == null) return;
        int idx = (int)Math.Clamp(Math.Round(SliderFps.Value), 0, FpsPresets.Length - 1);
        _settings.TargetFps = FpsPresets[idx];
        LblFps.Text = GetFpsLabel(_settings.TargetFps);
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnSoundToggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null) return;
        _settings.EnableSound = ChkSound?.IsChecked == true;
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _settings == null || SliderVolume == null || LblVolume == null) return;
        _settings.SoundVolume = Math.Round(SliderVolume.Value, 2);
        LblVolume.Text = $"{(int)Math.Round(_settings.SoundVolume * 100)}%";
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnClickThroughChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null || _overlay == null) return;
        _settings.ClickThrough = ChkClickThrough?.IsChecked == true;
        _overlay.Dispatcher.InvokeAsync(() => _overlay.ApplyWindowStyles());
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _settings == null) return;
        _settings.SetAutostart(ChkAutostart?.IsChecked == true);
    }

    private async void OnCreateShortcutClick(object sender, RoutedEventArgs e)
    {
        if (BtnCreateShortcut == null || _settings == null) return;

        bool success = ShortcutHelper.CreateDesktopShortcut(out string error);
        var lang = _settings.Language;

        if (success)
        {
            BtnCreateShortcut.Content = AppStrings.Get("ShortcutCreated", lang);
            BtnCreateShortcut.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 220, 120));
            await Task.Delay(2500);
            BtnCreateShortcut.Content = AppStrings.Get("CreateShortcutBtn", lang);
            BtnCreateShortcut.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
        }
        else
        {
            System.Windows.MessageBox.Show(error, "Desktop Weather", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.ResetCurrentProfile();
        _settings.SnowStyle = SnowStyle.Both;
        _settings.EnableStormClouds = true;
        _settings.ColorFilter = ColorFilterMode.Auto;
        _settings.FilterIntensity = 0.08;
        _settings.SoundVolume = 0.18;
        LoadSettingsToUi();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.IsPaused = !_settings.IsPaused;
        if (BtnPause != null) BtnPause.Content = _settings.IsPaused
            ? AppStrings.Get("ResumeBtn", _settings.Language)
            : AppStrings.Get("PauseBtn", _settings.Language);
        _settings.NotifyChanged();
        _settings.Save();
    }

    private void OnMinimizeToTrayClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnTitleBarMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnTitleBarCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch { }
        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private string GetFpsLabel(int fps) => fps switch
    {
        30 => AppStrings.Get("Fps30", _settings.Language),
        60 => AppStrings.Get("Fps60", _settings.Language),
        120 => AppStrings.Get("Fps120", _settings.Language),
        144 => AppStrings.Get("Fps144", _settings.Language),
        0 => AppStrings.Get("FpsMax", _settings.Language),
        _ => $"{fps} FPS"
    };

    private static int FpsToIndex(int fps) => fps switch
    {
        30 => 0,
        60 => 1,
        120 => 2,
        144 => 3,
        0 => 4,
        _ => 1
    };
}
