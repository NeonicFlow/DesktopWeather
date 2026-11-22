using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DesktopWeather.Services;

public static class ShortcutHelper
{
    public static bool CreateDesktopShortcut(out string message)
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                message = "Не удалось определить путь к файлу приложения.";
                return false;
            }

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktopPath, "Desktop Weather.lnk");

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                message = "WScript.Shell недоступен в операционной системе.";
                return false;
            }

            object? shell = Activator.CreateInstance(shellType);
            if (shell == null)
            {
                message = "Не удалось инициализировать интерфейс создания ярлыка.";
                return false;
            }

            object? shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            if (shortcut == null)
            {
                message = "Не удалось создать объект ярлыка.";
                return false;
            }

            Type scType = shortcut.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
            string workDir = Path.GetDirectoryName(exePath) ?? "";
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Погода на рабочем столе (Desktop Weather)" });
            scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { $"{exePath},0" });
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

            message = "Ярлык успешно создан!";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }
}
