using Nefarius.Drivers.HidHide;
using PadPilot.Core.Devices;

namespace PadPilot.App.Services;

/// <summary>
/// Oculta los mandos físicos a los juegos con el driver HidHide, para que con la emulación activa
/// solo vean el Xbox virtual y no el mando dos veces. PadPilot se agrega a la lista de apps permitidas
/// para poder seguir leyéndolos. Solo se quitan del bloqueo los mandos que ocultó PadPilot.
/// </summary>
public sealed class HidHideService
{
    public const string DownloadUrl = "https://github.com/nefarius/HidHide/releases/latest";

    private readonly HidHideControlService _service = new();
    private readonly HashSet<string> _hiddenByUs = new(StringComparer.OrdinalIgnoreCase);

    public enum State { NotInstalled, NeedsAdmin, Ready }

    /// <summary>Por qué falló la última operación, para mostrarlo en la interfaz.</summary>
    public string? LastError { get; private set; }

    public State Check()
    {
        try
        {
            if (!_service.IsInstalled || !_service.IsOperational) return State.NotInstalled;
            _ = _service.IsActive; // abrir el dispositivo de control: falla sin permisos de administrador
            return State.Ready;
        }
        catch (Exception)
        {
            return _service.IsInstalled ? State.NeedsAdmin : State.NotInstalled;
        }
    }

    /// <summary>Oculta estos mandos (y deja ver a los que ya no estén en la lista).</summary>
    public bool Apply(IEnumerable<GamepadController> controllers)
    {
        LastError = null;
        try
        {
            if (Environment.ProcessPath is { } exe && !_service.ApplicationPaths.Contains(exe, StringComparer.OrdinalIgnoreCase))
                _service.AddApplicationPath(exe, true);

            var wanted = controllers.Select(c => InstanceId(c.DevicePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var blocked = _service.BlockedInstanceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var id in wanted.Where(id => !blocked.Contains(id)))
            {
                _service.AddBlockedInstanceId(id);
                _hiddenByUs.Add(id);
            }
            foreach (var id in _hiddenByUs.Where(id => !wanted.Contains(id)).ToList())
            {
                _service.RemoveBlockedInstanceId(id);
                _hiddenByUs.Remove(id);
            }

            if (!_service.IsActive) _service.IsActive = true;
            return true;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            return false;
        }
    }

    /// <summary>Vuelve a mostrar todo lo que ocultó PadPilot (al apagar la opción o salir).</summary>
    public void RestoreAll()
    {
        try
        {
            foreach (var id in _hiddenByUs) _service.RemoveBlockedInstanceId(id);
        }
        catch (Exception) { /* sin permisos: no había nada oculto por nosotros */ }
        _hiddenByUs.Clear();
    }

    /// <summary>
    /// Convierte la ruta de HidSharp (\\?\hid#vid_054c&amp;pid_0ce6#7&amp;...#{guid}) en el id de instancia
    /// de Windows (HID\VID_054C&amp;PID_0CE6\7&amp;...), que es lo que bloquea HidHide.
    /// </summary>
    public static string InstanceId(string devicePath)
    {
        var path = devicePath.StartsWith(@"\\?\", StringComparison.Ordinal) ? devicePath[4..] : devicePath;
        int guid = path.LastIndexOf("#{", StringComparison.Ordinal);
        if (guid > 0) path = path[..guid];
        int suffix = path.IndexOf('\\');
        if (suffix > 0) path = path[..suffix]; // "\kbd" y similares al final
        return path.Replace('#', '\\').ToUpperInvariant();
    }
}
