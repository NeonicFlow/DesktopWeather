using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using DesktopWeather.Engine;
using DesktopWeather.Models;
using DesktopWeather.Services;
using DesktopWeather.Views;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace DesktopWeather;

public partial class App : WpfApplication
{
    private const string MutexName = "DesktopWeather_SingleInstance_Mutex_9E71D7CC";
    public const string CustomMsgName = "DesktopWeather_ShowInstance_Message";
    public static uint CustomShowMessage { get; private set; }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

    private static Mutex? _instanceMutex;
    private WeatherSettings? _settings;
    private WeatherEngine? _engine;
    private WeatherOverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private TrayIconManager? _trayManager;
    private WeatherAudioManager? _audioManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CustomShowMessage = RegisterWindowMessage(CustomMsgName);

        _instanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            // Another instance is already running! Broadcast message to bring it to foreground
            PostMessage(HWND_BROADCAST, CustomShowMessage, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (s, args) =>
        {
            WpfMessageBox.Show($"Произошла ошибка при работе приложения:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "Ошибка погоды на рабочем столе", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            // 1. Load Settings
            _settings = WeatherSettings.Load();

            // 2. Initialize Weather Engine
            _engine = new WeatherEngine(_settings);

            // 3. Create Weather Overlay Window
            _overlay = new WeatherOverlayWindow(_engine);
            _overlay.Show();

            // 4. Create Settings Window
            _settingsWindow = new SettingsWindow(_settings, _overlay);

            // 5. Initialize Ambient Audio Manager
            _audioManager = new WeatherAudioManager(_settings, _engine);

            // 6. Initialize System Tray Icon
            _trayManager = new TrayIconManager(_settings, _settingsWindow);

            // 7. Show Settings Window upon launch so user immediately sees controls
            _settingsWindow.Show();
            string bTitle = AppStrings.Get("BalloonTitle", _settings.Language);
            string bText = AppStrings.Get("BalloonText", _settings.Language);
            _trayManager.ShowBalloon(bTitle, bText);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Ошибка запуска: {ex.Message}\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _audioManager?.Dispose();
        _trayManager?.Dispose();
        _overlay?.Close();
        _settings?.Save();

        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch { }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
