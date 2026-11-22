using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace DesktopWeather.Models;

public enum AppLanguage
{
    Russian = 0,
    English = 1
}

public enum SnowStyle
{
    Both = 0,     // И кристаллы, и шарики
    Crystals = 1, // Только резные снежинки
    Flakes = 2    // Только снежные шарики / хлопья
}

public enum ColorFilterMode
{
    Off = 0,        // Отключить (без фильтра)
    Auto = 1,       // Автоматически по текущей погоде
    Snow = 2,       // Зимний (Холодный синий)
    Rain = 3,       // Дождевой (Пасмурный серо-синий)
    Storm = 4,      // Грозовой (Штормовой тёмный)
    Sakura = 5,     // Весенний (Нежный розовый)
    Leaves = 6,     // Осенний (Тёплый янтарный)
    Fireflies = 7,  // Ночной (Глубокий индиго)
    Mist = 8,       // Туманный (Дымчатый серый)
    Fire = 9,       // Огненный (Тёплый янтарно-красный)
    WaterDrops = 10,// Аквамарин (Стеклянно-водный)
    Wind = 11       // Ветреный (Прохладный лазурный)
}

public class WeatherProfile
{
    public int ParticleCount { get; set; } = 100;
    public double Speed { get; set; } = 1.0;
    public double Wind { get; set; } = 0.0;
    public double ParticleScale { get; set; } = 1.0;
    public double Opacity { get; set; } = 0.85;
    public bool EnableSplashes { get; set; } = true;
    public bool EnableLightning { get; set; } = true;
    public bool EnableStormClouds { get; set; } = true;
    public SnowStyle SnowStyle { get; set; } = SnowStyle.Both;
}

public class WeatherSettings
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopWeather");
    private static readonly string SettingsFilePath = Path.Combine(AppFolder, "settings.json");

    public WeatherType WeatherType { get; set; } = WeatherType.Snow;

    // Per-weather settings profiles (remembered independently for each weather)
    public Dictionary<WeatherType, WeatherProfile> Profiles { get; set; } = new();

    public WeatherSettings()
    {
    }

    public void EnsureProfiles()
    {
        Profiles ??= new Dictionary<WeatherType, WeatherProfile>();
        foreach (WeatherType wt in Enum.GetValues<WeatherType>())
        {
            if (!Profiles.ContainsKey(wt))
            {
                Profiles[wt] = CreateDefaultProfile(wt);
            }
        }
    }

    [JsonIgnore]
    public WeatherProfile CurrentProfile
    {
        get
        {
            EnsureProfiles();
            return Profiles[WeatherType];
        }
    }

    // Transparent per-weather profile delegates (preserves 100% compatibility with engine & UI)
    [JsonIgnore]
    public int ParticleCount
    {
        get => CurrentProfile.ParticleCount;
        set => CurrentProfile.ParticleCount = value;
    }

    [JsonIgnore]
    public double Speed
    {
        get => CurrentProfile.Speed;
        set => CurrentProfile.Speed = value;
    }

    [JsonIgnore]
    public double Wind
    {
        get => CurrentProfile.Wind;
        set => CurrentProfile.Wind = value;
    }

    [JsonIgnore]
    public double ParticleScale
    {
        get => CurrentProfile.ParticleScale;
        set => CurrentProfile.ParticleScale = value;
    }

    [JsonIgnore]
    public double Opacity
    {
        get => CurrentProfile.Opacity;
        set => CurrentProfile.Opacity = value;
    }

    [JsonIgnore]
    public bool EnableLightning
    {
        get => CurrentProfile.EnableLightning;
        set => CurrentProfile.EnableLightning = value;
    }

    [JsonIgnore]
    public bool EnableSplashes
    {
        get => CurrentProfile.EnableSplashes;
        set => CurrentProfile.EnableSplashes = value;
    }

    [JsonIgnore]
    public bool EnableStormClouds
    {
        get => CurrentProfile.EnableStormClouds;
        set => CurrentProfile.EnableStormClouds = value;
    }

    [JsonIgnore]
    public SnowStyle SnowStyle
    {
        get => CurrentProfile.SnowStyle;
        set => CurrentProfile.SnowStyle = value;
    }

    // Global App Preferences
    public bool EnableSunRays { get; set; } = true;
    public bool ClickThrough { get; set; } = true;
    public bool IsPaused { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public int TargetFps { get; set; } = 60;
    public bool EnableSound { get; set; } = true;
    public double SoundVolume { get; set; } = 0.18;

    // Color filter & Language
    public AppLanguage Language { get; set; } = AppLanguage.Russian;
    public ColorFilterMode ColorFilter { get; set; } = ColorFilterMode.Auto;
    public double FilterIntensity { get; set; } = 0.08;

    [JsonIgnore]
    public Action? OnChanged { get; set; }

    public void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(AppFolder))
            {
                Directory.CreateDirectory(AppFolder);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public static WeatherSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<WeatherSettings>(json);
                if (settings != null)
                {
                    bool hadProfiles = settings.Profiles != null && settings.Profiles.Count > 0;
                    settings.EnsureProfiles();
                    if (!hadProfiles && settings.Profiles != null)
                    {
                        // Seamless migration of legacy single-weather parameters
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (settings.Profiles.TryGetValue(settings.WeatherType, out var curProf) && curProf != null)
                            {
                                if (root.TryGetProperty("ParticleCount", out var pCount)) curProf.ParticleCount = pCount.GetInt32();
                                if (root.TryGetProperty("Speed", out var pSpeed)) curProf.Speed = pSpeed.GetDouble();
                                if (root.TryGetProperty("Wind", out var pWind)) curProf.Wind = pWind.GetDouble();
                                if (root.TryGetProperty("ParticleScale", out var pScale)) curProf.ParticleScale = pScale.GetDouble();
                                if (root.TryGetProperty("Opacity", out var pOpacity)) curProf.Opacity = pOpacity.GetDouble();
                                if (root.TryGetProperty("EnableSplashes", out var pSplashes)) curProf.EnableSplashes = pSplashes.GetBoolean();
                                if (root.TryGetProperty("EnableLightning", out var pLightning)) curProf.EnableLightning = pLightning.GetBoolean();
                                if (root.TryGetProperty("EnableStormClouds", out var pStormClouds)) curProf.EnableStormClouds = pStormClouds.GetBoolean();
                                if (root.TryGetProperty("SnowStyle", out var pSnowStyle)) curProf.SnowStyle = (SnowStyle)pSnowStyle.GetInt32();
                            }
                        }
                        catch { }
                        settings.Save();
                    }
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        var newSettings = new WeatherSettings();
        newSettings.EnsureProfiles();
        return newSettings;
    }

    public void SwitchWeather(WeatherType type)
    {
        WeatherType = type;
        EnsureProfiles();
        NotifyChanged();
        Save();
    }

    public void ResetCurrentProfile()
    {
        EnsureProfiles();
        Profiles[WeatherType] = CreateDefaultProfile(WeatherType);
        NotifyChanged();
        Save();
    }

    public void ApplyPreset(WeatherType type)
    {
        WeatherType = type;
        EnsureProfiles();
        Profiles[type] = CreateDefaultProfile(type);
        NotifyChanged();
        Save();
    }

    public static WeatherProfile CreateDefaultProfile(WeatherType type)
    {
        var profile = new WeatherProfile();
        switch (type)
        {
            case WeatherType.Snow:
                profile.ParticleCount = 140;
                profile.Speed = 1.0;
                profile.Wind = 0.3;
                profile.ParticleScale = 1.0;
                profile.Opacity = 0.85;
                profile.SnowStyle = SnowStyle.Both;
                break;
            case WeatherType.Rain:
                profile.ParticleCount = 180;
                profile.Speed = 1.4;
                profile.Wind = 0.5;
                profile.ParticleScale = 1.0;
                profile.Opacity = 0.70;
                profile.EnableLightning = false;
                profile.EnableSplashes = true;
                break;
            case WeatherType.Storm:
                profile.ParticleCount = 0; // Дождь отключен
                profile.Speed = 1.0;
                profile.Wind = 0.8;
                profile.ParticleScale = 1.0;
                profile.Opacity = 0.85;
                profile.EnableLightning = true;
                profile.EnableStormClouds = true;
                break;
            case WeatherType.Sakura:
                profile.ParticleCount = 90;
                profile.Speed = 0.9;
                profile.Wind = 0.7;
                profile.ParticleScale = 1.1;
                profile.Opacity = 0.90;
                break;
            case WeatherType.Leaves:
                profile.ParticleCount = 80;
                profile.Speed = 1.1;
                profile.Wind = 1.0;
                profile.ParticleScale = 1.2;
                profile.Opacity = 0.90;
                break;
            case WeatherType.Fireflies:
                profile.ParticleCount = 60;
                profile.Speed = 0.5;
                profile.Wind = 0.1;
                profile.ParticleScale = 1.2;
                profile.Opacity = 0.95;
                break;
            case WeatherType.Mist:
                profile.ParticleCount = 20;
                profile.Speed = 0.35;
                profile.Wind = 0.5;
                profile.ParticleScale = 2.0;
                profile.Opacity = 0.30;
                break;
            case WeatherType.Fire:
                profile.ParticleCount = 110;
                profile.Speed = 1.0;
                profile.Wind = 0.0;
                profile.ParticleScale = 1.1;
                profile.Opacity = 0.85;
                break;
            case WeatherType.WaterDrops:
                profile.ParticleCount = 65;
                profile.Speed = 0.8;
                profile.Wind = 0.0;
                profile.ParticleScale = 1.2;
                profile.Opacity = 0.80;
                break;
            case WeatherType.Wind:
                profile.ParticleCount = 35;
                profile.Speed = 1.0;
                profile.Wind = 0.6;
                profile.ParticleScale = 1.0;
                profile.Opacity = 0.70;
                break;
        }
        return profile;
    }

    public void SetAutostart(bool enable)
    {
        StartWithWindows = enable;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    if (enable)
                    {
                        key.SetValue("DesktopWeather", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("DesktopWeather", false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set autostart: {ex.Message}");
        }
        Save();
    }
}
