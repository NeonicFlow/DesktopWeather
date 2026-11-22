using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using DesktopWeather.Engine;
using DesktopWeather.Models;

namespace DesktopWeather.Services;

public class WeatherAudioManager : IDisposable
{
    private readonly MediaPlayer _ambientPlayer;
    private readonly MediaPlayer _thunderPlayer;
    private readonly WeatherSettings _settings;
    private readonly WeatherEngine _engine;
    private readonly Random _rand = new();
    private WeatherType _currentWeather;
    private bool _isPlaying = false;
    private readonly string _soundsBaseDir;
    private readonly string[] _thunderFiles;

    public WeatherAudioManager(WeatherSettings settings, WeatherEngine engine)
    {
        _settings = settings;
        _engine = engine;
        _currentWeather = settings.WeatherType;

        _soundsBaseDir = ResolveAndEnsureSoundsDirectory();
        string thunderDir = Path.Combine(_soundsBaseDir, "Thunder");

        // Collect available thunder audio files
        if (Directory.Exists(thunderDir))
        {
            _thunderFiles = Directory.GetFiles(thunderDir, "*.mp3");
        }
        else
        {
            _thunderFiles = Array.Empty<string>();
        }

        _ambientPlayer = new MediaPlayer();
        _ambientPlayer.MediaEnded += OnMediaEnded;

        _thunderPlayer = new MediaPlayer();

        _settings.OnChanged += OnSettingsChanged;
        _engine.LightningTriggered += OnLightningTriggered;

        UpdateAudio();
    }

    private void OnLightningTriggered()
    {
        try
        {
            bool isRainOrStorm = _settings.WeatherType == WeatherType.Rain || _settings.WeatherType == WeatherType.Storm;
            if (!_settings.EnableSound || _settings.IsPaused || !isRainOrStorm)
                return;

            if (_thunderFiles.Length > 0)
            {
                // Randomly select one of the authentic thunder recordings
                int idx = _rand.Next(_thunderFiles.Length);
                string chosenThunder = _thunderFiles[idx];

                _thunderPlayer.Stop();
                // 100% on slider maps to 50% volume (0.50 max) as requested
                _thunderPlayer.Volume = Math.Clamp(_settings.SoundVolume * 0.50, 0.0, 0.50);
                _thunderPlayer.Open(new Uri(chosenThunder, UriKind.Absolute));
                _thunderPlayer.Play();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thunder audio error: {ex.Message}");
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (_isPlaying && _settings.EnableSound && !_settings.IsPaused)
        {
            _ambientPlayer.Position = TimeSpan.Zero;
            _ambientPlayer.Play();
        }
    }

    private void OnSettingsChanged()
    {
        UpdateAudio();
    }

    public void UpdateAudio()
    {
        try
        {
            // Sakura, Mist and WaterDrops are completely silent as requested
            bool isSilentWeather = _settings.WeatherType == WeatherType.Sakura || 
                                   _settings.WeatherType == WeatherType.Mist || 
                                   _settings.WeatherType == WeatherType.WaterDrops;

            if (!_settings.EnableSound || _settings.IsPaused || _settings.SoundVolume <= 0.001 || isSilentWeather)
            {
                if (_isPlaying)
                {
                    _ambientPlayer.Pause();
                    _isPlaying = false;
                }
                return;
            }

            // 100% on slider maps to 50% volume (0.50 max) as requested
            _ambientPlayer.Volume = Math.Clamp(_settings.SoundVolume * 0.50, 0.0, 0.50);

            // If weather changed or wasn't playing, load new track
            if (!_isPlaying || _currentWeather != _settings.WeatherType)
            {
                _currentWeather = _settings.WeatherType;
                string? soundFile = GetSoundFileForWeather(_currentWeather);

                if (!string.IsNullOrEmpty(soundFile) && File.Exists(soundFile))
                {
                    _ambientPlayer.Open(new Uri(soundFile, UriKind.Absolute));
                    _ambientPlayer.Play();
                    _isPlaying = true;
                }
                else
                {
                    _ambientPlayer.Stop();
                    _isPlaying = false;
                }
            }
            else
            {
                _ambientPlayer.Play();
                _isPlaying = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio error: {ex.Message}");
        }
    }

    private string? GetSoundFileForWeather(WeatherType type)
    {
        string? fileName = type switch
        {
            WeatherType.Snow => "snow.mp3",  // Пользовательский winter-light-breeze.mp3
            WeatherType.Rain => "rain.wav",
            WeatherType.Storm => null,        // Гроза без звука дождя (только раскаты грома при молнии)
            WeatherType.Leaves => "leaves.wav",
            WeatherType.Fireflies => "fireflies.wav",
            WeatherType.Sakura => null,       // Без звука
            WeatherType.Mist => null,         // Без звука
            WeatherType.Fire => "fire.wav",   // Потрескивание огня
            WeatherType.WaterDrops => null,        // Без звука по запросу пользователя
            WeatherType.Wind => "wind.wav",   // Порывы ветра
            _ => null
        };

        return fileName != null ? Path.Combine(_soundsBaseDir, fileName) : null;
    }

    private static string ResolveAndEnsureSoundsDirectory()
    {
        // 1. If user placed a custom "Sounds" folder right next to the executable, prefer it!
        string localSounds = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds");
        if (Directory.Exists(localSounds) && Directory.EnumerateFiles(localSounds, "*.*", SearchOption.AllDirectories).Any())
        {
            return localSounds;
        }

        // 2. Otherwise use the AppData folder for embedded sound assets
        string appDataSounds = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopWeather",
            "Sounds");

        try
        {
            EnsureEmbeddedSoundsExtracted(appDataSounds);
            return appDataSounds;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract embedded sounds: {ex.Message}");
            return localSounds;
        }
    }

    private static void EnsureEmbeddedSoundsExtracted(string targetDir)
    {
        var asm = Assembly.GetExecutingAssembly();
        string prefix = "DesktopWeather.Sounds.";
        var names = asm.GetManifestResourceNames();

        foreach (var resName in names)
        {
            if (!resName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string sub = resName.Substring(prefix.Length);
            string relativePath = sub.StartsWith("Thunder.", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine("Thunder", sub.Substring("Thunder.".Length))
                : sub;

            string fullDestPath = Path.Combine(targetDir, relativePath);
            string? destFolder = Path.GetDirectoryName(fullDestPath);
            if (!string.IsNullOrEmpty(destFolder) && !Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            // Extract if not exists or size is 0
            if (!File.Exists(fullDestPath) || new FileInfo(fullDestPath).Length == 0)
            {
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream != null)
                {
                    using var fileStream = File.Create(fullDestPath);
                    stream.CopyTo(fileStream);
                }
            }
        }
    }

    public void Dispose()
    {
        _settings.OnChanged -= OnSettingsChanged;
        _engine.LightningTriggered -= OnLightningTriggered;
        _ambientPlayer.MediaEnded -= OnMediaEnded;
        _ambientPlayer.Stop();
        _ambientPlayer.Close();
        _thunderPlayer.Stop();
        _thunderPlayer.Close();
    }
}
