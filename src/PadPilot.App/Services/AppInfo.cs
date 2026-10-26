using System.Reflection;
using Microsoft.Win32;

namespace PadPilot.App.Services;

/// <summary>Datos del producto y enlaces públicos. Los enlaces vacíos ocultan su botón en "Acerca de".</summary>
public static class AppInfo
{
    public const string Name = "PadPilot";

    public const string Author = "ExO";

    // ---- Completar al publicar ----
    /// <summary>Página de donaciones, p. ej. https://ko-fi.com/tu_usuario</summary>
    public const string DonateUrl = "";

    /// <summary>Repositorio de GitHub, p. ej. https://github.com/tu_usuario/PadPilot</summary>
    public const string RepositoryUrl = "https://github.com/ExOBrienX/PadPilot";

    /// <summary>Sitio web (GitHub Pages), p. ej. https://tu_usuario.github.io/PadPilot</summary>
    public const string WebsiteUrl = "https://exobrienx.github.io/PadPilot/";

    public static string IssuesUrl => RepositoryUrl.Length > 0 ? RepositoryUrl.TrimEnd('/') + "/issues" : "";

    public const string ViGEmBusUrl = "https://github.com/nefarius/ViGEmBus/releases/latest";

    public const string HidHideUrl = "https://github.com/nefarius/HidHide/releases/latest";

    /// <summary>Versión del ensamblado sin metadatos de compilación (p. ej. "1.0.0").</summary>
    public static string Version { get; } =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0")
        .Split('+')[0];

    public static IReadOnlyList<(string Name, string License)> ThirdParty { get; } =
    [
        ("Avalonia UI", "MIT"),
        ("CommunityToolkit.Mvvm", "MIT"),
        ("HidSharp", "Apache-2.0"),
        ("NAudio", "MIT"),
        ("Nefarius.ViGEm.Client", "MIT"),
        ("Nefarius.Drivers.HidHide", "MIT"),
        ("SkiaSharp", "MIT"),
    ];

    /// <summary>¿Está instalado este controlador de Windows? (se mira su servicio en el registro).</summary>
    public static bool IsDriverInstalled(string service)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{service}");
            return key is not null;
        }
        catch (Exception) { return false; }
    }

    public static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { }
    }
}
