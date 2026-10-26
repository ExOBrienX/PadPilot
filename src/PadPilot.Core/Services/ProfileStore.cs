using System.Text.Json;
using System.Text.Json.Serialization;
using PadPilot.Core.Devices;
using PadPilot.Core.Models;

namespace PadPilot.Core.Services;

/// <summary>
/// Guarda los perfiles y los ajustes globales en %AppData%\PadPilot\profiles.json. Las escrituras se agrupan
/// (debounce de 500 ms) para no tocar el disco con cada movimiento de un slider.
/// </summary>
public sealed class ProfileStore : IDisposable
{
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Timer _saveTimer;
    private readonly PadPilotSettings _data;

    public ProfileStore(string path)
    {
        _path = path;
        MigrateFromPadLink(path);
        _data = Load(path);
        _saveTimer = new Timer(_ => SaveNow(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadPilot", "profiles.json");

    /// <summary>La app se llamó PadLink en sus primeras versiones: se trae su configuración una sola vez.</summary>
    private static void MigrateFromPadLink(string path)
    {
        try
        {
            var old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadLink", "profiles.json");
            if (File.Exists(path) || !File.Exists(old)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(old, path);
        }
        catch (Exception) { }
    }

    public ControllerProfile GetOrCreate(GamepadController controller)
    {
        lock (_gate)
        {
            if (!_data.Controllers.TryGetValue(controller.Id, out var profile))
            {
                profile = new ControllerProfile { Name = controller.DisplayName };
                _data.Controllers[controller.Id] = profile;
            }
            return profile;
        }
    }

    public Rgb GetSlotColor(int slot)
    {
        if (slot is < 1 or > PlayerSlots.Count) return PlayerSlots.NoSlotColor;
        lock (_gate) return _data.SlotColors[slot - 1];
    }

    public void SetSlotColor(int slot, Rgb color)
    {
        if (slot is < 1 or > PlayerSlots.Count) return;
        lock (_gate) _data.SlotColors[slot - 1] = color;
        SaveDeferred();
    }

    /// <summary>Ajustes de la interfaz (idioma, tema, diseño). Tras cambiarlos llamar a <see cref="SaveDeferred"/>.</summary>
    public PadPilotSettings Settings => _data;

    /// <summary>Avisos de batería en la barra (globales). Tras cambiarlos llamar a <see cref="SaveDeferred"/>.</summary>
    public LightingSettings Lighting => _data.Lighting;

    public bool HideControllers
    {
        get { lock (_gate) return _data.HideControllers; }
        set
        {
            lock (_gate) _data.HideControllers = value;
            SaveDeferred();
        }
    }

    /// <summary>Lista de macros. Se modifica en el hilo de UI; tras cambiarla llamar a <see cref="SaveDeferred"/>.</summary>
    public List<MacroDefinition> Macros => _data.Macros;

    public MacroDefinition? FindMacro(string id)
    {
        lock (_gate) return _data.Macros.FirstOrDefault(m => m.Id == id);
    }

    public GenericLayout? GetGenericLayout(string modelKey)
    {
        lock (_gate) return _data.GenericLayouts.GetValueOrDefault(modelKey);
    }

    /// <summary>Guarda la asignación de un modelo genérico; null vuelve a la estándar.</summary>
    public void SetGenericLayout(string modelKey, GenericLayout? layout)
    {
        lock (_gate)
        {
            if (layout is null) _data.GenericLayouts.Remove(modelKey);
            else _data.GenericLayouts[modelKey] = layout;
        }
        SaveDeferred();
    }

    public bool EmulateXbox
    {
        get { lock (_gate) return _data.EmulateXbox; }
        set
        {
            lock (_gate) _data.EmulateXbox = value;
            SaveDeferred();
        }
    }

    public void SaveDeferred() => _saveTimer.Change(500, Timeout.Infinite);

    public void SaveNow()
    {
        try
        {
            string json;
            lock (_gate)
                json = JsonSerializer.Serialize(_data, PadPilotJsonContext.Default.PadPilotSettings);

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, overwrite: true); // escritura atómica: nunca queda un JSON a medias
        }
        catch (Exception)
        {
            // No es crítico: se reintenta en el próximo cambio.
        }
    }

    private static PadPilotSettings Load(string path)
    {
        PadPilotSettings? data = null;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty(nameof(PadPilotSettings.Controllers), out _))
                {
                    data = JsonSerializer.Deserialize(json, PadPilotJsonContext.Default.PadPilotSettings);
                }
                else
                {
                    // Formato de la v0.1: el archivo era solo el diccionario de mandos.
                    var controllers = JsonSerializer.Deserialize(json, PadPilotJsonContext.Default.DictionaryStringControllerProfile);
                    if (controllers is not null) data = new PadPilotSettings { Controllers = controllers };
                }
            }
        }
        catch (Exception) { /* archivo corrupto: se parte de cero */ }

        data ??= new PadPilotSettings();
        if (data.SlotColors is not { Length: PlayerSlots.Count })
            data.SlotColors = PlayerSlots.DefaultColors();
        data.Controllers ??= new();
        data.GenericLayouts ??= new();
        data.Macros ??= new();
        data.Lighting ??= new();
        foreach (var profile in data.Controllers.Values)
        {
            profile.ButtonMap ??= new();
            profile.LeftStick ??= new();
            profile.RightStick ??= new();
        }
        return data;
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        SaveNow();
    }
}

/// <summary>Serialización generada en compilación: sin reflexión, compatible con trimming/AOT.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PadPilotSettings))]
[JsonSerializable(typeof(Dictionary<string, ControllerProfile>))]
internal partial class PadPilotJsonContext : JsonSerializerContext;
