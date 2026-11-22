using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopWeather.Engine;
using DesktopWeather.Models;

namespace DesktopWeather.Views;

public partial class WeatherOverlayWindow : Window
{
    private readonly WeatherEngine _engine;
    private readonly WeatherControl _control;
    private readonly Stopwatch _stopwatch = new();
    private double _lastTime = 0;
    private IntPtr _hwnd = IntPtr.Zero;

    #region Win32 API
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    #endregion

    public WeatherOverlayWindow(WeatherEngine engine)
    {
        InitializeComponent();
        _engine = engine;

        _control = new WeatherControl(_engine);
        RootGrid.Children.Add(_control);

        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += (s, e) => _engine.Resize(ActualWidth, ActualHeight);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        UpdateBounds();
        ApplyWindowStyles();

        _stopwatch.Start();
        _lastTime = _stopwatch.Elapsed.TotalSeconds;

        CompositionTarget.Rendering += OnRendering;

        // Listen for resolution changes
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateBounds();
            _engine.Reset();
        });
    }

    public void UpdateBounds()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _engine.Resize(Width, Height);
    }

    public void ApplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero) return;

        long exStyle = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();

        // Always tool window and layered
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;

        if (_engine.Settings.ClickThrough)
        {
            exStyle |= WS_EX_TRANSPARENT;
            IsHitTestVisible = false;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
            IsHitTestVisible = true;
        }

        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(exStyle));

        // Reassert topmost without activating
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double currentTime = _stopwatch.Elapsed.TotalSeconds;
        double dt = currentTime - _lastTime;

        // FPS limiting if configured
        int targetFps = _engine.Settings.TargetFps;
        if (targetFps > 0)
        {
            double minFrameTime = 1.0 / targetFps;
            if (dt < minFrameTime) return;
        }

        _lastTime = currentTime;

        _engine.Update(dt);
        _control.InvalidateVisual();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}

public class WeatherControl : FrameworkElement
{
    private readonly WeatherEngine _engine;

    public WeatherControl(WeatherEngine engine)
    {
        _engine = engine;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        _engine.Render(drawingContext);
    }
}
