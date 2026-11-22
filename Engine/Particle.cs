using System;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;

namespace DesktopWeather.Engine;

public class Particle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Size { get; set; }
    public double BaseAlpha { get; set; } = 1.0;
    public double Alpha { get; set; } = 1.0;

    // Swaying & Physics
    public double Time { get; set; }
    public double SwaySpeed { get; set; }
    public double SwayAmplitude { get; set; }
    public double SwayPhase { get; set; }

    // 3D Tumbling for Petals & Leaves
    public double Rotation { get; set; }
    public double RotationSpeed { get; set; }
    public double FlipAngle { get; set; }
    public double FlipSpeed { get; set; }

    // Color & Styling
    public Brush? FillBrush { get; set; }
    public Pen? StrokePen { get; set; }
    public int Variant { get; set; }

    // Splashes for rain
    public bool IsSplash { get; set; }
    public double Life { get; set; } = 1.0;
    public double MaxLife { get; set; } = 1.0;
}
