using Microsoft.Win32;

namespace PadPilot.App.Services;

/// <summary>Arranque con Windows mediante la clave Run del usuario (no requiere permisos de admin).</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PadPilot";
    private const string LegacyValueName = "PadLink"; // nombre anterior de la app

    /// <summary>
    /// Instalada desde la Microsoft Store (paquete MSIX), la clave Run del registro queda aislada y no sirve;
    /// ahí el inicio automático se gestiona desde Configuración de Windows → Aplicaciones → Inicio.
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsWindows() && !IsPackaged;

    public static bool IsPackaged { get; } = DetectPackage();

    private static bool DetectPackage()
    {
        try
        {
            uint length = 0;
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }
        catch (Exception) { return false; }
    }

    private const int AppModelErrorNoPackage = 15700;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint length, char[]? name);

    public static bool IsEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        if (!IsSupported) return;

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string Command => $"\"{Environment.ProcessPath}\" --tray";

    /// <summary>Si el .exe se movió de carpeta, actualiza la ruta guardada para que siga arrancando.</summary>
    public static void RefreshPath()
    {
        if (!IsSupported) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string current && current != Command)
                key.SetValue(ValueName, Command);
        }
        catch (Exception) { }
    }
}
