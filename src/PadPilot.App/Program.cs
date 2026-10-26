using Avalonia;
using Avalonia.Controls;

// PadPilot usa HID, registro y Core Audio de Windows: solo funciona en Windows.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace PadPilot.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Una sola instancia: si ya está corriendo en la bandeja, no abrimos otra.
        using var mutex = new Mutex(initiallyOwned: true, "PadPilot.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            // Al reiniciar como administrador, la copia anterior tarda un momento en cerrarse.
            if (!args.Contains("--restart")) return;
            try { if (!mutex.WaitOne(TimeSpan.FromSeconds(10))) return; }
            catch (AbandonedMutexException) { }
        }

        // La app vive en la bandeja: cerrar la ventana no la termina, solo "Salir" lo hace.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    // También lo usa el previsualizador de Avalonia en el editor.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
