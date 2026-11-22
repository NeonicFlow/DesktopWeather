using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using DesktopWeather.Models;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;
using Color = System.Windows.Media.Color;
using Matrix = System.Windows.Media.Matrix;

namespace DesktopWeather.Engine;

public class WeatherEngine
{
    private readonly Random _rand = new();
    private readonly List<Particle> _particles = new();
    private readonly Particle[] _splashPool = new Particle[80];
    private int _splashPoolIndex = 0;

    public WeatherSettings Settings { get; }
    public double ScreenWidth { get; private set; } = 1920;
    public double ScreenHeight { get; private set; } = 1080;

    // Lightning event & effect
    public event Action? LightningTriggered;
    private double _lightningTimer = 0;
    private double _lightningNextInterval = 18;
    private double _lightningFlashAlpha = 0;
    private List<Point>? _lightningBolt = null;
    private WeatherType _currentType;

    // Animation time
    private double _totalTime = 0;

    // Cached geometries
    private static readonly StreamGeometry SakuraGeometry;
    private static readonly StreamGeometry MapleGeometry;
    private static readonly StreamGeometry SnowflakeGeometry;
    private static readonly StreamGeometry WaterTeardropGeometry;
    private static readonly StreamGeometry FlameOuterGeometry;
    private static readonly StreamGeometry FlameCoreGeometry;

    // Cached frozen palettes (Zero GC allocations per frame, NO PushOpacity overhead)
    private static readonly Brush[] WhiteAlphaBrushes = new Brush[256];
    private static readonly Brush[,] SakuraAlphaBrushes = new Brush[5, 256];
    private static readonly Brush[,] LeafAlphaBrushes = new Brush[5, 256];
    private static readonly Brush[] FireYellowBrushes = new Brush[256];
    private static readonly Brush[] FireOrangeBrushes = new Brush[256];
    private static readonly Brush[] FireRedBrushes = new Brush[256];
    private static readonly Brush[] FireSmokeBrushes = new Brush[256];
    private static readonly Brush[] FireEmberBrushes = new Brush[256];
    private static readonly Brush[] FireCoreBrushes = new Brush[256];
    private static readonly Brush[] WaterDropBrushes = new Brush[256];
    private static readonly Brush[] FireflyHaloBrushes = new Brush[256];
    private static readonly Brush[] FireflyCoreBrushes = new Brush[256];
    private static readonly Brush[] MistBrushes = new Brush[256];

    private static readonly Pen RainNearPen;
    private static readonly Pen RainFarPen;
    private static readonly Pen LightningPen;
    private static readonly Pen WaterDropBorderPen;
    private static readonly Pen WaterTracePen;
    private static readonly Brush WaterGlintBrush;
    private static readonly Brush WaterCausticBrush;
    private static readonly Pen[] WindPens = new Pen[256];
    private static readonly Pen[] WindEchoPens = new Pen[256];
    private static readonly Pen SnowflakePen;

    private static readonly RadialGradientBrush StormCloudBrush;
    private static readonly RadialGradientBrush SoftMistBrush;
    private static readonly LinearGradientBrush FireBaseGlowBrush;

    // Cached tint brush for optional color filter
    private Brush? _cachedTintBrush;
    private ColorFilterMode _lastFilterMode = ColorFilterMode.Off;
    private WeatherType _lastFilterWeather = WeatherType.Snow;
    private double _lastFilterIntensity = -1;

    static WeatherEngine()
    {
        // 1. Sakura Petal Geometry
        SakuraGeometry = new StreamGeometry();
        using (var ctx = SakuraGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, -12), true, true);
            ctx.BezierTo(new Point(-5, -13), new Point(-9, -8), new Point(-9, -2), true, true);
            ctx.BezierTo(new Point(-9, 5), new Point(-4, 11), new Point(0, 14), true, true);
            ctx.BezierTo(new Point(4, 11), new Point(9, 5), new Point(9, -2), true, true);
            ctx.BezierTo(new Point(9, -8), new Point(5, -13), new Point(0, -12), true, true);
        }
        SakuraGeometry.Freeze();

        // 2. Maple Leaf Geometry
        MapleGeometry = new StreamGeometry();
        using (var ctx = MapleGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, -15), true, true);
            ctx.LineTo(new Point(4, -8), true, true);
            ctx.LineTo(new Point(12, -12), true, true);
            ctx.LineTo(new Point(8, -3), true, true);
            ctx.LineTo(new Point(15, 3), true, true);
            ctx.LineTo(new Point(7, 5), true, true);
            ctx.LineTo(new Point(2, 14), true, true);
            ctx.LineTo(new Point(0, 16), true, true);
            ctx.LineTo(new Point(-2, 14), true, true);
            ctx.LineTo(new Point(-7, 5), true, true);
            ctx.LineTo(new Point(-15, 3), true, true);
            ctx.LineTo(new Point(-8, -3), true, true);
            ctx.LineTo(new Point(-12, -12), true, true);
            ctx.LineTo(new Point(-4, -8), true, true);
        }
        MapleGeometry.Freeze();

        // 3. Snowflake Crystal Geometry
        SnowflakeGeometry = new StreamGeometry();
        using (var ctx = SnowflakeGeometry.Open())
        {
            for (int i = 0; i < 3; i++)
            {
                double angle = i * Math.PI / 3.0;
                double dx = Math.Cos(angle) * 10;
                double dy = Math.Sin(angle) * 10;
                ctx.BeginFigure(new Point(-dx, -dy), false, false);
                ctx.LineTo(new Point(dx, dy), true, true);
            }
        }
        SnowflakeGeometry.Freeze();

        // 4. Realistic Water Droplet Teardrop Geometry (narrow apex, rounded wide base)
        WaterTeardropGeometry = new StreamGeometry();
        using (var ctx = WaterTeardropGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, -18), true, true);
            // Right flank: tapering smoothly from top apex down to wide bottom bulge
            ctx.BezierTo(new Point(4, -14), new Point(12, -2), new Point(12, 8), true, true);
            // Bulbous rounded bottom
            ctx.BezierTo(new Point(12, 17), new Point(6, 20), new Point(0, 20), true, true);
            // Left bulbous bottom
            ctx.BezierTo(new Point(-6, 20), new Point(-12, 17), new Point(-12, 8), true, true);
            // Left flank returning to apex
            ctx.BezierTo(new Point(-12, -2), new Point(-4, -14), new Point(0, -18), true, true);
        }
        WaterTeardropGeometry.Freeze();

        // 5. Realistic Flame Geometries (pre-aligned outer mantle and inner core)
        FlameOuterGeometry = new StreamGeometry();
        using (var ctx = FlameOuterGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, -26), true, true);
            ctx.BezierTo(new Point(3, -18), new Point(14, -2), new Point(13, 10), true, true);
            ctx.BezierTo(new Point(12, 17), new Point(6, 19), new Point(0, 19), true, true);
            ctx.BezierTo(new Point(-6, 19), new Point(-12, 17), new Point(-13, 10), true, true);
            ctx.BezierTo(new Point(-14, -2), new Point(-3, -18), new Point(0, -26), true, true);
        }
        FlameOuterGeometry.Freeze();

        FlameCoreGeometry = new StreamGeometry();
        using (var ctx = FlameCoreGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, -11), true, true);
            ctx.BezierTo(new Point(2, -6), new Point(7, 2), new Point(6, 8), true, true);
            ctx.BezierTo(new Point(6, 13), new Point(3, 15), new Point(0, 15), true, true);
            ctx.BezierTo(new Point(-3, 15), new Point(-6, 13), new Point(-6, 8), true, true);
            ctx.BezierTo(new Point(-7, 2), new Point(-2, -6), new Point(0, -11), true, true);
        }
        FlameCoreGeometry.Freeze();

        // 6. Base Palettes
        Color[] sakuraColors = new Color[]
        {
            Color.FromRgb(255, 183, 197),
            Color.FromRgb(255, 165, 185),
            Color.FromRgb(255, 195, 208),
            Color.FromRgb(250, 150, 175),
            Color.FromRgb(255, 215, 225)
        };

        Color[] leafColors = new Color[]
        {
            Color.FromRgb(211, 47, 47),
            Color.FromRgb(230, 81, 0),
            Color.FromRgb(245, 124, 0),
            Color.FromRgb(251, 192, 45),
            Color.FromRgb(160, 64, 0)
        };

        // 7. Pre-allocated Frozen 256-step Alpha Brushes and Pens (Instant O(1) lookup, 0 GC, NO PushOpacity)
        for (int i = 0; i < 256; i++)
        {
            byte a = (byte)i;
            WhiteAlphaBrushes[i] = new SolidColorBrush(Color.FromArgb(a, 255, 255, 255));
            WhiteAlphaBrushes[i].Freeze();

            FireYellowBrushes[i] = new SolidColorBrush(Color.FromArgb(a, 255, 235, 95));
            FireYellowBrushes[i].Freeze();

            FireOrangeBrushes[i] = new SolidColorBrush(Color.FromArgb(a, 255, 125, 20));
            FireOrangeBrushes[i].Freeze();

            FireRedBrushes[i] = new SolidColorBrush(Color.FromArgb(a, 230, 45, 15));
            FireRedBrushes[i].Freeze();

            FireSmokeBrushes[i] = new SolidColorBrush(Color.FromArgb((byte)(a * 0.45), 50, 42, 42));
            FireSmokeBrushes[i].Freeze();

            FireEmberBrushes[i] = new SolidColorBrush(Color.FromArgb(a, 255, 220, 140));
            FireEmberBrushes[i].Freeze();

            FireCoreBrushes[i] = new SolidColorBrush(Color.FromArgb(a, 255, 250, 215));
            FireCoreBrushes[i].Freeze();

            // Water droplet: soft translucent watery cyan-blue
            WaterDropBrushes[i] = new SolidColorBrush(Color.FromArgb((byte)(a * 0.55), 180, 218, 245));
            WaterDropBrushes[i].Freeze();

            // Firefly glow
            FireflyHaloBrushes[i] = new SolidColorBrush(Color.FromArgb((byte)(a * 0.28), 180, 255, 120));
            FireflyHaloBrushes[i].Freeze();

            FireflyCoreBrushes[i] = new SolidColorBrush(Color.FromArgb(a, 255, 250, 190));
            FireflyCoreBrushes[i].Freeze();

            // Mist puffs
            MistBrushes[i] = new SolidColorBrush(Color.FromArgb((byte)(a * 0.22), 215, 222, 230));
            MistBrushes[i].Freeze();

            // Wind Pens: smooth rounded caps for aerodynamic '~' waves
            var windBrush = new SolidColorBrush(Color.FromArgb((byte)(a * 0.85), 220, 240, 255));
            windBrush.Freeze();
            var windPen = new Pen(windBrush, 2.4)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            windPen.Freeze();
            WindPens[i] = windPen;

            var windEchoBrush = new SolidColorBrush(Color.FromArgb((byte)(a * 0.45), 190, 225, 255));
            windEchoBrush.Freeze();
            var windEchoPen = new Pen(windEchoBrush, 1.4)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            windEchoPen.Freeze();
            WindEchoPens[i] = windEchoPen;

            for (int k = 0; k < 5; k++)
            {
                var sCol = sakuraColors[k];
                SakuraAlphaBrushes[k, i] = new SolidColorBrush(Color.FromArgb(a, sCol.R, sCol.G, sCol.B));
                SakuraAlphaBrushes[k, i].Freeze();

                var lCol = leafColors[k];
                LeafAlphaBrushes[k, i] = new SolidColorBrush(Color.FromArgb(a, lCol.R, lCol.G, lCol.B));
                LeafAlphaBrushes[k, i].Freeze();
            }
        }

        // 7. Pens
        var rainNearBrush = new SolidColorBrush(Color.FromArgb(180, 205, 235, 255));
        rainNearBrush.Freeze();
        RainNearPen = new Pen(rainNearBrush, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        RainNearPen.Freeze();

        var rainFarBrush = new SolidColorBrush(Color.FromArgb(115, 175, 210, 245));
        rainFarBrush.Freeze();
        RainFarPen = new Pen(rainFarBrush, 1.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        RainFarPen.Freeze();

        var lightningBrush = new SolidColorBrush(Color.FromArgb(240, 240, 255, 255));
        lightningBrush.Freeze();
        LightningPen = new Pen(lightningBrush, 3.0);
        LightningPen.Freeze();

        var waterBorderBrush = new SolidColorBrush(Color.FromArgb(95, 70, 115, 160));
        waterBorderBrush.Freeze();
        WaterDropBorderPen = new Pen(waterBorderBrush, 0.85);
        WaterDropBorderPen.Freeze();

        var waterTraceBrush = new SolidColorBrush(Color.FromArgb(45, 180, 220, 250));
        waterTraceBrush.Freeze();
        WaterTracePen = new Pen(waterTraceBrush, 1.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        WaterTracePen.Freeze();

        var glintBrush = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
        glintBrush.Freeze();
        WaterGlintBrush = glintBrush;

        var causticBrush = new SolidColorBrush(Color.FromArgb(145, 205, 245, 255));
        causticBrush.Freeze();
        WaterCausticBrush = causticBrush;

        SnowflakePen = new Pen(WhiteAlphaBrushes[220], 1.2);
        SnowflakePen.Freeze();

        // 8. Gradients
        StormCloudBrush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.2),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(220, 18, 20, 26), 0.0),
                new GradientStop(Color.FromArgb(160, 25, 28, 36), 0.55),
                new GradientStop(Color.FromArgb(0, 25, 28, 36), 1.0)
            }
        };
        StormCloudBrush.Freeze();

        SoftMistBrush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(52, 225, 232, 240), 0.0),
                new GradientStop(Color.FromArgb(32, 220, 228, 236), 0.35),
                new GradientStop(Color.FromArgb(12, 215, 222, 230), 0.72),
                new GradientStop(Color.FromArgb(0, 210, 218, 226), 1.0)
            }
        };
        SoftMistBrush.Freeze();

        FireBaseGlowBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0.0),
            EndPoint = new Point(0.5, 1.0),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(0, 255, 170, 30), 0.0),
                new GradientStop(Color.FromArgb(25, 255, 180, 50), 0.5),
                new GradientStop(Color.FromArgb(55, 255, 205, 70), 1.0)
            }
        };
        FireBaseGlowBrush.Freeze();
    }

    public WeatherEngine(WeatherSettings settings)
    {
        Settings = settings;
        _currentType = settings.WeatherType;
        _lightningNextInterval = _currentType == WeatherType.Storm ? _rand.Next(4, 9) : _rand.Next(14, 28);
        for (int i = 0; i < _splashPool.Length; i++)
        {
            _splashPool[i] = new Particle { Life = 0 };
        }
    }

    public void Resize(double width, double height)
    {
        ScreenWidth = Math.Max(100, width);
        ScreenHeight = Math.Max(100, height);
    }

    public void Reset()
    {
        _particles.Clear();
        for (int i = 0; i < _splashPool.Length; i++)
        {
            _splashPool[i].Life = 0;
        }
        _lightningFlashAlpha = 0;
        _lightningBolt = null;
        _lightningTimer = 0;
        _lightningNextInterval = Settings.WeatherType == WeatherType.Storm ? _rand.Next(4, 9) : _rand.Next(14, 28);
    }

    public void Update(double dt)
    {
        if (Settings.IsPaused) return;

        _totalTime += dt;

        if (_currentType != Settings.WeatherType)
        {
            _currentType = Settings.WeatherType;
            Reset();
        }

        dt = Math.Min(dt, 0.05);

        // Storm has 0 rain drops (pure lightning + storm clouds)
        int targetCount = Settings.WeatherType == WeatherType.Storm ? 0 : Math.Max(5, Settings.ParticleCount);

        while (_particles.Count < targetCount)
        {
            _particles.Add(CreateParticle(randomY: true));
        }
        while (_particles.Count > targetCount)
        {
            _particles.RemoveAt(_particles.Count - 1);
        }

        double windForce = Settings.Wind * 120.0;
        double speedMult = Settings.Speed;

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            p.Time += dt;

            switch (Settings.WeatherType)
            {
                case WeatherType.Snow:
                    UpdateSnowParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.Rain:
                    UpdateRainParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.Sakura:
                    UpdatePetalParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.Leaves:
                    UpdateLeafParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.Fireflies:
                    UpdateFireflyParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.Mist:
                    UpdateMistParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.Fire:
                    UpdateFireParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.WaterDrops:
                    UpdateWaterDropParticle(p, dt, speedMult, windForce);
                    break;
                case WeatherType.Wind:
                    UpdateWindParticle(p, dt, speedMult, windForce);
                    break;
            }

            // Boundary recycling (in-place reset, 0 allocations)
            if (Settings.WeatherType == WeatherType.Mist)
            {
                double rX = p.Size;
                if (p.X > ScreenWidth + rX + 60)
                {
                    p.X = -rX - 60;
                    p.Y = _rand.NextDouble() * (ScreenHeight * 0.85) + (ScreenHeight * 0.05);
                }
            }
            else if (Settings.WeatherType == WeatherType.Fire)
            {
                if (p.Life >= p.MaxLife || p.Y < -50)
                {
                    ResetParticle(p, randomY: false);
                }
            }
            else if (Settings.WeatherType == WeatherType.Wind)
            {
                bool isMovingRight = Settings.Wind >= 0;
                if (isMovingRight && p.X > ScreenWidth + p.Size + 60)
                {
                    ResetParticle(p, randomY: false);
                }
                else if (!isMovingRight && p.X < -p.Size - 60)
                {
                    ResetParticle(p, randomY: false);
                }
            }
            else if (Settings.WeatherType == WeatherType.WaterDrops)
            {
                if (p.Y > ScreenHeight + 20)
                {
                    ResetParticle(p, randomY: false);
                }
            }
            else
            {
                if (p.Y > ScreenHeight + 50 || p.X < -150 || p.X > ScreenWidth + 150)
                {
                    ResetParticle(p, randomY: false);
                }
            }
        }

        // Splashes (Rain only) - in-place update with 0 allocations
        if (Settings.WeatherType == WeatherType.Rain && Settings.EnableSplashes)
        {
            for (int i = 0; i < _splashPool.Length; i++)
            {
                var s = _splashPool[i];
                if (s.Life <= 0) continue;

                s.Life -= dt;
                if (s.Life <= 0) continue;

                s.X += s.Vx * dt;
                s.Y += s.Vy * dt;
                s.Vy += 280 * dt;
                s.Alpha = (s.Life / s.MaxLife) * Settings.Opacity;
            }
        }

        // Lightning logic
        bool isLightningWeather = Settings.WeatherType == WeatherType.Storm ||
                                  (Settings.WeatherType == WeatherType.Rain && Settings.EnableLightning);

        if (isLightningWeather)
        {
            _lightningTimer += dt;
            if (_lightningTimer >= _lightningNextInterval)
            {
                _lightningTimer = 0;
                _lightningNextInterval = Settings.WeatherType == WeatherType.Storm ? _rand.Next(4, 9) : _rand.Next(14, 28);
                TriggerLightning();
            }
        }

        if (_lightningFlashAlpha > 0)
        {
            _lightningFlashAlpha -= dt * 2.5;
            if (_lightningFlashAlpha <= 0)
            {
                _lightningFlashAlpha = 0;
                _lightningBolt = null;
            }
        }
    }

    private void TriggerLightning()
    {
        _lightningFlashAlpha = 0.85;
        _lightningBolt = GenerateLightningBolt();
        LightningTriggered?.Invoke();
    }

    private List<Point> GenerateLightningBolt()
    {
        var points = new List<Point>();
        double startX = _rand.NextDouble() * (ScreenWidth * 0.7) + (ScreenWidth * 0.15);
        double currentX = startX;
        double currentY = 0;
        points.Add(new Point(currentX, currentY));

        double endY = ScreenHeight * (_rand.NextDouble() * 0.4 + 0.5);

        while (currentY < endY)
        {
            currentY += _rand.NextDouble() * 25 + 15;
            currentX += (_rand.NextDouble() * 50 - 25);
            points.Add(new Point(currentX, currentY));
        }

        return points;
    }

    private Particle CreateParticle(bool randomY)
    {
        var p = new Particle();
        ResetParticle(p, randomY);
        return p;
    }

    private void ResetParticle(Particle p, bool randomY)
    {
        p.Time = _rand.NextDouble() * 100;
        p.SwaySpeed = _rand.NextDouble() * 2.0 + 1.0;
        p.SwayAmplitude = _rand.NextDouble() * 25.0 + 10.0;
        p.SwayPhase = _rand.NextDouble() * Math.PI * 2;
        p.Rotation = _rand.NextDouble() * 360;
        p.RotationSpeed = (_rand.NextDouble() * 120 - 60);
        p.FlipAngle = _rand.NextDouble() * Math.PI * 2;
        p.FlipSpeed = _rand.NextDouble() * 2.5 + 1.0;
        p.IsSplash = false;

        switch (Settings.WeatherType)
        {
            case WeatherType.Snow:
                p.X = _rand.NextDouble() * (ScreenWidth + 200) - 100;
                p.Y = randomY ? _rand.NextDouble() * ScreenHeight : -_rand.NextDouble() * 50 - 10;
                p.Vy = _rand.NextDouble() * 60 + 50;
                p.Vx = _rand.NextDouble() * 20 - 10;
                p.Size = _rand.NextDouble() * 4.0 + 2.0;
                p.BaseAlpha = _rand.NextDouble() * 0.4 + 0.6;
                break;

            case WeatherType.Rain:
                p.X = _rand.NextDouble() * (ScreenWidth + 200) - 100;
                p.Y = randomY ? _rand.NextDouble() * ScreenHeight : -_rand.NextDouble() * 100 - 20;
                p.Vy = _rand.NextDouble() * 450 + 600;
                p.Vx = _rand.NextDouble() * 30 - 15;
                p.Size = _rand.NextDouble() * 18.0 + 16.0;
                p.BaseAlpha = _rand.NextDouble() * 0.35 + 0.55;
                break;

            case WeatherType.Sakura:
                p.X = _rand.NextDouble() * (ScreenWidth + 200) - 100;
                p.Y = randomY ? _rand.NextDouble() * ScreenHeight : -_rand.NextDouble() * 60 - 20;
                p.Vy = _rand.NextDouble() * 50 + 40;
                p.Vx = _rand.NextDouble() * 25 - 12;
                p.Size = _rand.NextDouble() * 6.0 + 9.0;
                p.BaseAlpha = _rand.NextDouble() * 0.25 + 0.75;
                p.Variant = _rand.Next(5);
                break;

            case WeatherType.Leaves:
                p.X = _rand.NextDouble() * (ScreenWidth + 200) - 100;
                p.Y = randomY ? _rand.NextDouble() * ScreenHeight : -_rand.NextDouble() * 60 - 20;
                p.Vy = _rand.NextDouble() * 60 + 45;
                p.Vx = _rand.NextDouble() * 30 - 15;
                p.Size = _rand.NextDouble() * 8.0 + 12.0;
                p.BaseAlpha = _rand.NextDouble() * 0.2 + 0.8;
                p.Variant = _rand.Next(5);
                break;

            case WeatherType.Fireflies:
                p.X = _rand.NextDouble() * ScreenWidth;
                p.Y = randomY ? _rand.NextDouble() * ScreenHeight : ScreenHeight + _rand.NextDouble() * 30;
                p.Vy = -(_rand.NextDouble() * 25 + 15);
                p.Vx = _rand.NextDouble() * 20 - 10;
                p.Size = _rand.NextDouble() * 2.5 + 2.0;
                p.BaseAlpha = _rand.NextDouble() * 0.3 + 0.7;
                break;

            case WeatherType.Mist:
                p.X = randomY ? _rand.NextDouble() * ScreenWidth : -350 - _rand.NextDouble() * 150;
                p.Y = _rand.NextDouble() * (ScreenHeight * 0.85) + (ScreenHeight * 0.05);
                p.Vx = _rand.NextDouble() * 25 + 20;
                p.Vy = 0;
                p.Size = _rand.NextDouble() * 180 + 260; // broad, soft volumetric fog bank
                p.BaseAlpha = _rand.NextDouble() * 0.35 + 0.65;
                p.SwayPhase = _rand.NextDouble() * Math.PI * 2;
                break;

            case WeatherType.Fire:
                p.X = _rand.NextDouble() * ScreenWidth;
                p.Y = randomY ? ScreenHeight - _rand.NextDouble() * (ScreenHeight * 0.35) : ScreenHeight + _rand.NextDouble() * 10;
                p.IsSplash = (_rand.NextDouble() < 0.30); // true = dancing spark/ember, false = flame tongue
                if (p.IsSplash)
                {
                    // Ember spark: gentle upward float
                    p.Vy = -(_rand.NextDouble() * 140 + 90);
                    p.Vx = (_rand.NextDouble() * 40 - 20);
                    p.Size = _rand.NextDouble() * 2.0 + 1.2;
                    p.MaxLife = _rand.NextDouble() * 1.6 + 1.0;
                }
                else
                {
                    // Flame tongue: smooth buoyant rise
                    p.Vy = -(_rand.NextDouble() * 120 + 80);
                    p.Vx = (_rand.NextDouble() * 20 - 10);
                    p.Size = _rand.NextDouble() * 16.0 + 13.0;
                    p.MaxLife = _rand.NextDouble() * 1.6 + 1.1;
                }
                p.Life = randomY ? _rand.NextDouble() * p.MaxLife : 0;
                p.SwaySpeed = _rand.NextDouble() * 0.8 + 2.5; // smooth organic breathing pace
                p.SwayPhase = _rand.NextDouble() * Math.PI * 2;
                p.BaseAlpha = 1.0;
                break;

            case WeatherType.WaterDrops:
                p.X = _rand.NextDouble() * ScreenWidth;
                p.Y = randomY ? _rand.NextDouble() * ScreenHeight : -_rand.NextDouble() * 40 - 10;
                p.Variant = _rand.Next(10);
                // 0..4 (50%): Small round condensation dew bead (size 2.4 - 4.2)
                // 5..8 (40%): Medium trickling teardrop (size 5.5 - 8.5)
                // 9 (10%): Large heavy teardrop (size 10.0 - 13.5)
                p.Size = p.Variant switch
                {
                    < 5 => _rand.NextDouble() * 1.8 + 2.4,
                    < 9 => _rand.NextDouble() * 3.0 + 5.5,
                    _ => _rand.NextDouble() * 3.5 + 10.0
                };
                p.BaseAlpha = _rand.NextDouble() * 0.25 + 0.70;
                p.Life = _rand.NextDouble() * 3.2 + 0.6; // Stick-slip hesitation phase timer
                p.MaxLife = p.Life;
                p.Vx = 0;
                p.Vy = (p.Variant < 5 ? 4.0 : 14.0);
                break;

            case WeatherType.Wind:
                bool right = Settings.Wind >= 0;
                p.X = randomY ? _rand.NextDouble() * (ScreenWidth + 400) - 200 : (right ? -260 - _rand.NextDouble() * 100 : ScreenWidth + 260 + _rand.NextDouble() * 100);
                p.Y = _rand.NextDouble() * (ScreenHeight * 0.90) + (ScreenHeight * 0.05);
                double baseWindSpeed = 260 + Math.Abs(Settings.Wind) * 160;
                p.Vx = right ? baseWindSpeed : -baseWindSpeed;
                p.Vy = 0;
                p.Size = _rand.NextDouble() * 110.0 + 200.0; // 200px - 310px flowing ribbon length
                p.SwayAmplitude = _rand.NextDouble() * 8.0 + 10.0; // 10px - 18px wave amplitude
                p.SwaySpeed = _rand.NextDouble() * 1.5 + 4.5; // traveling wave ripple speed
                p.Variant = _rand.Next(3); // 0 = single stream, 1 = double stream with companion echo, 2 = sparkling gust
                p.SwayPhase = _rand.NextDouble() * Math.PI * 2;
                p.BaseAlpha = _rand.NextDouble() * 0.28 + 0.65;
                break;
        }

        p.Alpha = p.BaseAlpha * Settings.Opacity;
    }

    private void UpdateSnowParticle(Particle p, double dt, double speedMult, double windForce)
    {
        double sway = Math.Sin(p.Time * p.SwaySpeed + p.SwayPhase) * p.SwayAmplitude;
        p.X += (sway * 0.5 + p.Vx + windForce) * dt;
        p.Y += (p.Vy * speedMult) * dt;
        p.Rotation += p.RotationSpeed * dt;
        p.Alpha = p.BaseAlpha * Settings.Opacity;
    }

    private void UpdateRainParticle(Particle p, double dt, double speedMult, double windForce)
    {
        p.X += (p.Vx + windForce * 1.5) * dt;
        p.Y += (p.Vy * speedMult) * dt;
        p.Alpha = p.BaseAlpha * Settings.Opacity;

        if (p.Y >= ScreenHeight - 15 && Settings.EnableSplashes)
        {
            SpawnSplash(p.X, windForce);
        }
    }

    private void SpawnSplash(double x, double windForce)
    {
        int splashCount = _rand.Next(1, 3);
        for (int k = 0; k < splashCount; k++)
        {
            var s = _splashPool[_splashPoolIndex];
            _splashPoolIndex = (_splashPoolIndex + 1) % _splashPool.Length;

            s.X = x;
            s.Y = ScreenHeight - _rand.Next(2, 10);
            s.Vx = (_rand.NextDouble() * 100 - 50) + (windForce * 0.15);
            s.Vy = -(_rand.NextDouble() * 70 + 35);
            s.Size = _rand.NextDouble() * 1.8 + 1.0;
            s.MaxLife = _rand.NextDouble() * 0.22 + 0.12;
            s.Life = s.MaxLife;
            s.IsSplash = true;
        }
    }

    private void UpdatePetalParticle(Particle p, double dt, double speedMult, double windForce)
    {
        double sway = Math.Sin(p.Time * p.SwaySpeed + p.SwayPhase) * p.SwayAmplitude;
        p.X += (sway + p.Vx + windForce) * dt;
        p.Y += (p.Vy * speedMult) * dt;
        p.Rotation += p.RotationSpeed * dt;
        p.FlipAngle += p.FlipSpeed * dt;
        p.Alpha = p.BaseAlpha * Settings.Opacity;
    }

    private void UpdateLeafParticle(Particle p, double dt, double speedMult, double windForce)
    {
        double sway = Math.Sin(p.Time * p.SwaySpeed + p.SwayPhase) * (p.SwayAmplitude * 1.5);
        p.X += (sway + p.Vx + windForce * 1.2) * dt;
        p.Y += (p.Vy * speedMult) * dt;
        p.Rotation += p.RotationSpeed * dt;
        p.FlipAngle += p.FlipSpeed * dt;
        p.Alpha = p.BaseAlpha * Settings.Opacity;
    }

    private void UpdateFireflyParticle(Particle p, double dt, double speedMult, double windForce)
    {
        p.X += (Math.Sin(p.Time * p.SwaySpeed) * 35 + windForce * 0.3) * dt;
        p.Y += (Math.Cos(p.Time * (p.SwaySpeed * 0.7)) * 25 - 10 * speedMult) * dt;

        if (p.Y < -30) p.Y = ScreenHeight + 20;

        double pulse = 0.5 + 0.5 * Math.Sin(p.Time * 3.5 + p.SwayPhase);
        p.Alpha = (0.2 + 0.8 * pulse) * p.BaseAlpha * Settings.Opacity;
    }

    private void UpdateMistParticle(Particle p, double dt, double speedMult, double windForce)
    {
        p.X += (p.Vx * speedMult + windForce * 0.5) * dt;
        p.Y += Math.Sin(p.Time * 0.3 + p.SwayPhase) * 8 * dt;
        p.Alpha = p.BaseAlpha * Settings.Opacity;
    }

    private void UpdateFireParticle(Particle p, double dt, double speedMult, double windForce)
    {
        p.Life += dt;
        double progress = p.Life / p.MaxLife;

        if (p.IsSplash)
        {
            // Ember spark: gentle thermal drift and swirl
            double sparkDrift = Math.Sin(p.Time * 3.5 + p.SwayPhase) * 16.0;
            p.X += (p.Vx * 0.6 + sparkDrift + windForce * 0.6) * dt;
            p.Y += (p.Vy * speedMult) * dt;

            // Organic glowing pulse
            double pulse = 0.75 + 0.25 * Math.Sin(p.Time * 8.0 + p.SwayPhase);
            double fade = Math.Sin(Math.Clamp(progress, 0.0, 1.0) * Math.PI);
            p.Alpha = fade * pulse * Settings.Opacity;
        }
        else
        {
            // Flame tongue: smooth buoyant rise with gentle thermal sway (no rigid rotation, no shaking)
            double thermalSway = Math.Sin(p.Time * p.SwaySpeed + p.SwayPhase) * 10.0;
            p.X += (p.Vx * 0.4 + thermalSway + windForce * 0.4) * dt;
            p.Y += (p.Vy * speedMult) * dt;

            // Smooth envelope: fades in quickly at base, gently vanishes at top
            double fade = Math.Sin(Math.Clamp(progress, 0.0, 1.0) * Math.PI);
            p.Alpha = fade * p.BaseAlpha * Settings.Opacity;
        }
    }

    private void UpdateWaterDropParticle(Particle p, double dt, double speedMult, double windForce)
    {
        p.Life -= dt;
        if (p.Life <= 0)
        {
            // Reset stick-slip cycle: drops hesitate, then slip downwards
            p.Life = _rand.NextDouble() * 3.0 + 0.8;
        }

        // Realistic stick-slip physics:
        // When surface tension holds (Life >= 0.38): drop is resting or creeping very slowly (4-12 px/sec)
        // When surface tension breaks (Life < 0.38): drop slips/spurts down quickly (110-180 px/sec)
        bool isSlipping = (p.Life < 0.38) && (p.Variant >= 5);
        double targetVy = isSlipping
            ? (_rand.NextDouble() * 50 + 130)
            : (p.Variant < 5 ? 3.5 : 12.0);

        // Smooth velocity response (no instant jarring jump)
        p.Vy += (targetVy - p.Vy) * Math.Min(1.0, dt * 10.0);

        p.X += (windForce * 0.06) * dt;
        p.Y += (p.Vy * speedMult) * dt;
        p.Alpha = p.BaseAlpha * Settings.Opacity;
    }

    private void UpdateWindParticle(Particle p, double dt, double speedMult, double windForce)
    {
        bool isMovingRight = Settings.Wind >= 0;
        double baseWindSpeed = 260.0 + Math.Abs(Settings.Wind) * 160.0;
        p.Vx = isMovingRight ? baseWindSpeed : -baseWindSpeed;
        p.X += (p.Vx * speedMult) * dt;
        p.Y += Math.Sin(p.Time * 1.2 + p.SwayPhase) * 8.0 * dt;

        // Smooth edge fade to prevent popping at screen boundaries
        double edgeMargin = 180.0;
        double distFromEdge = isMovingRight
            ? (p.X < edgeMargin ? Math.Max(0, p.X) : Math.Max(0, ScreenWidth - p.X))
            : (p.X > ScreenWidth - edgeMargin ? Math.Max(0, ScreenWidth - p.X) : Math.Max(0, p.X));
        double edgeFade = Math.Clamp(distFromEdge / edgeMargin, 0.0, 1.0);
        p.Alpha = p.BaseAlpha * Settings.Opacity * (0.25 + 0.75 * edgeFade);
    }

    public void Render(DrawingContext dc)
    {
        if (Settings.IsPaused) return;

        double scale = Settings.ParticleScale;

        // 1. Optional subtle screen tint filter (ONLY when explicitly chosen by user)
        if (Settings.ColorFilter != ColorFilterMode.Off && Settings.FilterIntensity > 0.005)
        {
            RenderColorFilter(dc);
        }

        // 2. Render current atmospheric weather (100% direct draw, NO PushOpacity, 0 GC)
        switch (Settings.WeatherType)
        {
            case WeatherType.Storm:
                RenderStorm(dc, scale);
                break;
            case WeatherType.Snow:
                RenderSnow(dc, scale);
                break;
            case WeatherType.Rain:
                RenderRain(dc, scale);
                break;
            case WeatherType.Sakura:
                RenderSakura(dc, scale);
                break;
            case WeatherType.Leaves:
                RenderLeaves(dc, scale);
                break;
            case WeatherType.Fireflies:
                RenderFireflies(dc, scale);
                break;
            case WeatherType.Mist:
                RenderMist(dc, scale);
                break;
            case WeatherType.Fire:
                RenderFire(dc, scale);
                break;
            case WeatherType.WaterDrops:
                RenderWaterDrops(dc, scale);
                break;
            case WeatherType.Wind:
                RenderWind(dc, scale);
                break;
        }

        // Lightning flash illumination & bolt (for Storm or Rain with lightning)
        if (_lightningFlashAlpha > 0.001)
        {
            byte flashA = (byte)(Math.Clamp(_lightningFlashAlpha, 0.0, 1.0) * 255);
            dc.DrawRectangle(WhiteAlphaBrushes[flashA], null, new Rect(0, 0, ScreenWidth, ScreenHeight));

            if (_lightningBolt != null && _lightningBolt.Count > 1)
            {
                var boltGeo = new StreamGeometry();
                using (var ctx = boltGeo.Open())
                {
                    ctx.BeginFigure(_lightningBolt[0], false, false);
                    for (int i = 1; i < _lightningBolt.Count; i++)
                    {
                        ctx.LineTo(_lightningBolt[i], true, true);
                    }
                }
                boltGeo.Freeze();
                dc.DrawGeometry(null, LightningPen, boltGeo);
            }
        }

        // Splashes (Rain only) - pooled, 0 GC allocations
        if (Settings.WeatherType == WeatherType.Rain && Settings.EnableSplashes)
        {
            for (int i = 0; i < _splashPool.Length; i++)
            {
                var s = _splashPool[i];
                if (s.Life <= 0) continue;

                byte a = (byte)Math.Clamp(s.Alpha * 255, 0, 255);
                double r = s.Size * scale;
                dc.DrawEllipse(WhiteAlphaBrushes[a], null, new Point(s.X, s.Y), r, r);
            }
        }
    }

    private void RenderColorFilter(DrawingContext dc)
    {
        if (_cachedTintBrush == null ||
            _lastFilterMode != Settings.ColorFilter ||
            _lastFilterWeather != Settings.WeatherType ||
            Math.Abs(_lastFilterIntensity - Settings.FilterIntensity) > 0.002)
        {
            _lastFilterMode = Settings.ColorFilter;
            _lastFilterWeather = Settings.WeatherType;
            _lastFilterIntensity = Settings.FilterIntensity;

            var targetMode = _lastFilterMode == ColorFilterMode.Auto ? _lastFilterWeather switch
            {
                WeatherType.Snow => ColorFilterMode.Snow,
                WeatherType.Rain => ColorFilterMode.Rain,
                WeatherType.Storm => ColorFilterMode.Storm,
                WeatherType.Sakura => ColorFilterMode.Sakura,
                WeatherType.Leaves => ColorFilterMode.Leaves,
                WeatherType.Fireflies => ColorFilterMode.Fireflies,
                WeatherType.Mist => ColorFilterMode.Mist,
                WeatherType.Fire => ColorFilterMode.Fire, // Restored in Auto mode!
                WeatherType.WaterDrops => ColorFilterMode.WaterDrops,
                WeatherType.Wind => ColorFilterMode.Wind,
                _ => ColorFilterMode.Snow
            } : _lastFilterMode;

            if (targetMode == ColorFilterMode.Off)
            {
                _cachedTintBrush = null;
                return;
            }

            Color tint = targetMode switch
            {
                ColorFilterMode.Snow => Color.FromRgb(30, 85, 175),
                ColorFilterMode.Rain => Color.FromRgb(40, 55, 75),
                ColorFilterMode.Storm => Color.FromRgb(15, 18, 32),
                ColorFilterMode.Sakura => Color.FromRgb(190, 80, 115),
                ColorFilterMode.Leaves => Color.FromRgb(185, 95, 25),
                ColorFilterMode.Fireflies => Color.FromRgb(18, 22, 55),
                ColorFilterMode.Mist => Color.FromRgb(65, 75, 90),
                ColorFilterMode.Fire => Color.FromRgb(255, 175, 65), // Soft pleasant golden-amber ambiance (NOT harsh red)
                ColorFilterMode.WaterDrops => Color.FromRgb(35, 100, 145),
                ColorFilterMode.Wind => Color.FromRgb(45, 95, 135),
                _ => Color.FromRgb(30, 85, 175)
            };

            double intensity = targetMode == ColorFilterMode.Fire ? _lastFilterIntensity * 0.70 : _lastFilterIntensity;
            byte a = (byte)Math.Clamp(intensity * 255, 0, 255);
            var brush = new SolidColorBrush(Color.FromArgb(a, tint.R, tint.G, tint.B));
            brush.Freeze();
            _cachedTintBrush = brush;
        }

        if (_cachedTintBrush != null)
        {
            dc.DrawRectangle(_cachedTintBrush, null, new Rect(0, 0, ScreenWidth, ScreenHeight));
        }
    }

    private void RenderStorm(DrawingContext dc, double scale)
    {
        if (Settings.EnableStormClouds)
        {
            int cloudClusters = 9;
            double step = ScreenWidth / (cloudClusters - 1);

            for (int i = 0; i < cloudClusters; i++)
            {
                double cx = i * step + Math.Sin(_totalTime * 0.4 + i) * 25;
                double cy = 20 + Math.Cos(_totalTime * 0.3 + i * 1.5) * 15;
                double rX = step * 1.1;
                double rY = 110 + Math.Sin(_totalTime * 0.5 + i) * 20;

                dc.DrawEllipse(StormCloudBrush, null, new Point(cx, cy), rX, rY);
            }
        }
    }

    private void RenderSnow(DrawingContext dc, double scale)
    {
        var style = Settings.SnowStyle;

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            byte a = (byte)Math.Clamp(p.Alpha * 255, 0, 255);
            double radius = p.Size * scale;

            bool isCrystal = style switch
            {
                SnowStyle.Crystals => true,
                SnowStyle.Flakes => false,
                SnowStyle.Both => (i % 2 == 0),
                _ => (i % 2 == 0)
            };

            if (isCrystal)
            {
                double crystalRadius = Math.Max(3.2, radius);
                var mat = Matrix.Identity;
                mat.Scale(crystalRadius / 10.0, crystalRadius / 10.0);
                mat.Rotate(p.Rotation);
                mat.Translate(p.X, p.Y);

                dc.PushTransform(new MatrixTransform(mat));
                dc.DrawGeometry(null, SnowflakePen, SnowflakeGeometry);
                dc.Pop();
            }
            else
            {
                dc.DrawEllipse(WhiteAlphaBrushes[a], null, new Point(p.X, p.Y), radius, radius);
            }
        }
    }

    private void RenderRain(DrawingContext dc, double scale)
    {
        double speedMult = Settings.Speed;
        double windForce = Settings.Wind * 120.0;

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            double vx = p.Vx + windForce * 1.5;
            double vy = p.Vy * speedMult;
            double vel = Math.Sqrt(vx * vx + vy * vy);
            if (vel < 1.0) vel = 1.0;

            // Velocity-proportional motion streak bridges the frame-to-frame jump seamlessly
            double streakLen = Math.Max(26.0, vel * 0.038) * scale;
            double dirX = vx / vel;
            double dirY = vy / vel;

            double startX = p.X;
            double startY = p.Y;
            double endX = startX + dirX * streakLen;
            double endY = startY + dirY * streakLen;

            Pen pen = (i % 2 == 0) ? RainNearPen : RainFarPen;
            dc.DrawLine(pen, new Point(startX, startY), new Point(endX, endY));
        }
    }

    private void RenderSakura(DrawingContext dc, double scale)
    {
        foreach (var p in _particles)
        {
            byte a = (byte)Math.Clamp(p.Alpha * 255, 0, 255);
            var brush = SakuraAlphaBrushes[p.Variant, a];

            double s = (p.Size / 12.0) * scale;
            double flipScale = Math.Cos(p.FlipAngle);

            var mat = Matrix.Identity;
            mat.Scale(flipScale * s, s);
            mat.Rotate(p.Rotation);
            mat.Translate(p.X, p.Y);

            dc.PushTransform(new MatrixTransform(mat));
            dc.DrawGeometry(brush, null, SakuraGeometry);
            dc.Pop();
        }
    }

    private void RenderLeaves(DrawingContext dc, double scale)
    {
        foreach (var p in _particles)
        {
            byte a = (byte)Math.Clamp(p.Alpha * 255, 0, 255);
            var brush = LeafAlphaBrushes[p.Variant, a];

            double s = (p.Size / 14.0) * scale;
            double flipScale = Math.Cos(p.FlipAngle);

            var mat = Matrix.Identity;
            mat.Scale(flipScale * s, s);
            mat.Rotate(p.Rotation);
            mat.Translate(p.X, p.Y);

            dc.PushTransform(new MatrixTransform(mat));
            dc.DrawGeometry(brush, null, MapleGeometry);
            dc.Pop();
        }
    }

    private void RenderFireflies(DrawingContext dc, double scale)
    {
        foreach (var p in _particles)
        {
            byte a = (byte)Math.Clamp(p.Alpha * 255, 0, 255);
            double r = p.Size * scale;
            dc.DrawEllipse(FireflyHaloBrushes[a], null, new Point(p.X, p.Y), r * 3.2, r * 3.2);
            dc.DrawEllipse(FireflyCoreBrushes[a], null, new Point(p.X, p.Y), r, r);
        }
    }

    private void RenderMist(DrawingContext dc, double scale)
    {
        // Real atmospheric volumetric mist with feathered radial gradient clouds
        foreach (var p in _particles)
        {
            double rX = p.Size * scale;
            double rY = p.Size * 0.42 * scale;
            dc.DrawEllipse(SoftMistBrush, null, new Point(p.X, p.Y), rX, rY);
        }
    }

    private void RenderFire(DrawingContext dc, double scale)
    {
        // 1. Soft warm breathing incandescent glow at the hearth base
        double baseH = 30.0 + Math.Sin(_totalTime * 2.5) * 3.0;
        dc.DrawRectangle(FireBaseGlowBrush, null, new Rect(0, ScreenHeight - baseH, ScreenWidth, baseH));

        // 2. Rising flame tongues and dancing embers
        foreach (var p in _particles)
        {
            byte a = (byte)Math.Clamp(p.Alpha * 255, 0, 255);
            if (a == 0) continue;

            double progress = p.Life / p.MaxLife;

            if (p.IsSplash)
            {
                // Glowing bright spark / ember with miniature soft halo
                double r = p.Size * scale;
                dc.DrawEllipse(FireOrangeBrushes[(byte)(a * 0.40)], null, new Point(p.X, p.Y), r * 2.2, r * 2.2);
                dc.DrawEllipse(FireEmberBrushes[a], null, new Point(p.X, p.Y), r, r);
            }
            else
            {
                // Flame tongue: smooth organic stretch along height as it rises
                double rX = p.Size * (1.0 - progress * 0.35) * scale;
                double rY = p.Size * (1.4 + (1.0 - progress) * 0.4) * scale;

                Brush outerBrush = progress switch
                {
                    < 0.35 => FireOrangeBrushes[a],
                    < 0.70 => FireRedBrushes[a],
                    _ => FireSmokeBrushes[a]
                };

                var mat = Matrix.Identity;
                mat.Scale(rX / 13.0, rY / 22.0);
                mat.Translate(p.X, p.Y);

                dc.PushTransform(new MatrixTransform(mat));
                // 1. Outer flame mantle
                dc.DrawGeometry(outerBrush, null, FlameOuterGeometry);

                // 2. Inner incandescent hot core (white-yellow)
                if (progress < 0.55)
                {
                    byte coreA = (byte)Math.Clamp((1.0 - progress / 0.55) * a, 0, 255);
                    Brush coreBrush = progress < 0.22 ? FireCoreBrushes[coreA] : FireYellowBrushes[coreA];
                    dc.DrawGeometry(coreBrush, null, FlameCoreGeometry);
                }
                dc.Pop();
            }
        }
    }

    private void RenderWaterDrops(DrawingContext dc, double scale)
    {
        foreach (var p in _particles)
        {
            byte a = (byte)Math.Clamp(p.Alpha * 255, 0, 255);
            if (a == 0) continue;

            double rX = p.Size * scale;
            double rY = rX * 1.35;

            if (p.Variant < 5)
            {
                // 1. Small resting condensation dew bead on glass
                dc.DrawEllipse(WaterDropBrushes[a], WaterDropBorderPen, new Point(p.X, p.Y), rX, rX);

                // Specular primary glint
                double glintR = Math.Max(0.7, rX * 0.32);
                dc.DrawEllipse(WaterGlintBrush, null, new Point(p.X - rX * 0.30, p.Y - rX * 0.30), glintR, glintR);

                // Lower-right caustic refraction reflection
                dc.DrawEllipse(WaterCausticBrush, null, new Point(p.X + rX * 0.22, p.Y + rX * 0.22), glintR * 0.65, glintR * 0.65);
            }
            else
            {
                // 2. Realistic trickling teardrop drop on glass (clean drop without trail lines)
                // Scaled teardrop geometry
                var mat = Matrix.Identity;
                mat.Scale(rX / 12.0, rY / 18.0);
                mat.Translate(p.X, p.Y);

                dc.PushTransform(new MatrixTransform(mat));
                dc.DrawGeometry(WaterDropBrushes[a], WaterDropBorderPen, WaterTeardropGeometry);
                dc.Pop();

                // Primary crisp specular glint curve near upper waist
                double glintW = Math.Max(0.8, rX * 0.32);
                double glintH = glintW * 1.35;
                dc.DrawEllipse(WaterGlintBrush, null, new Point(p.X - rX * 0.28, p.Y - rY * 0.20), glintW, glintH);

                // Lower bulbous caustic rim highlight
                dc.DrawEllipse(WaterCausticBrush, null, new Point(p.X + rX * 0.18, p.Y + rY * 0.38), glintW * 0.9, glintW * 0.55);
            }
        }
    }

    private void RenderWind(DrawingContext dc, double scale)
    {
        // Realistic aerodynamic wind ribbons (полоски) dynamically bending and waving as they fly
        bool isMovingRight = Settings.Wind >= 0;
        int segments = 22;

        foreach (var p in _particles)
        {
            byte a = (byte)Math.Clamp(p.Alpha * 255, 0, 255);
            if (a == 0) continue;

            Pen pen = WindPens[a];
            Pen echoPen = WindEchoPens[a];

            double len = p.Size * scale;

            // Build dynamic flowing wind stream geometry
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                Point headPt = new Point(p.X, p.Y);
                ctx.BeginFigure(headPt, false, false);

                for (int k = 1; k <= segments; k++)
                {
                    double t = (double)k / segments;
                    double curX = isMovingRight ? (p.X - t * len) : (p.X + t * len);

                    // Smooth sinusoidal envelope: tapers wave to 0 at head and tail
                    double envelope = Math.Sin(t * Math.PI);

                    // Traveling wave: actively propagates and bends along the ribbon
                    double waveAngle = (t * 2.2 * Math.PI) - (p.Time * p.SwaySpeed) + p.SwayPhase;
                    double curY = p.Y + Math.Sin(waveAngle) * (p.SwayAmplitude * envelope * scale);

                    ctx.LineTo(new Point(curX, curY), true, true);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);

            // Double stream companion wisp for variety
            if (p.Variant == 1)
            {
                var echoGeo = new StreamGeometry();
                using (var ctx = echoGeo.Open())
                {
                    double echoLen = len * 0.75;
                    Point echoHead = new Point(p.X, p.Y - 4.5);
                    ctx.BeginFigure(echoHead, false, false);

                    for (int k = 1; k <= 16; k++)
                    {
                        double t = (double)k / 16;
                        double curX = isMovingRight ? (p.X - t * echoLen) : (p.X + t * echoLen);
                        double envelope = Math.Sin(t * Math.PI);
                        double waveAngle = (t * 2.4 * Math.PI) - (p.Time * p.SwaySpeed) + p.SwayPhase + 0.5;
                        double curY = (p.Y - 4.5) + Math.Sin(waveAngle) * (p.SwayAmplitude * 0.75 * envelope * scale);

                        ctx.LineTo(new Point(curX, curY), true, true);
                    }
                }
                echoGeo.Freeze();
                dc.DrawGeometry(null, echoPen, echoGeo);
            }
            else if (p.Variant == 2)
            {
                // Air glint sparkle mote riding the crest of the gust
                double midT = 0.42;
                double glintX = isMovingRight ? (p.X - midT * len) : (p.X + midT * len);
                double glintAngle = (midT * 2.2 * Math.PI) - (p.Time * p.SwaySpeed) + p.SwayPhase;
                double glintY = p.Y + Math.Sin(glintAngle) * (p.SwayAmplitude * Math.Sin(midT * Math.PI) * scale);
                dc.DrawEllipse(WhiteAlphaBrushes[a], null, new Point(glintX, glintY), 1.5 * scale, 1.5 * scale);
            }
        }
    }
}
