using PadPilot.Core.Models;
using PadPilot.Core.Protocol;

namespace PadPilot.Core.Services;

/// <summary>Configuración guardada de un mando concreto (identificado por su MAC).</summary>
public sealed class ControllerProfile
{
    public string Name { get; set; } = "";

    /// <summary>Slot preferido 1-4. 0 = el primero libre.</summary>
    public int PreferredSlot { get; set; }

    /// <summary>
    /// Si es true, la barra de luz usa el color del slot de jugador (editable y compartido por todos los mandos).
    /// Si es false, usa <see cref="Color"/>, propio de este mando.
    /// </summary>
    public bool UseSlotColor { get; set; } = true;

    public Rgb Color { get; set; } = new(0, 64, 255);

    /// <summary>Intensidad de la barra de luz en %. Bajarla ahorra batería.</summary>
    public int LightbarIntensity { get; set; } = 60;

    public LedBrightness PlayerLedBrightness { get; set; } = LedBrightness.Medium;

    public TriggerEffect LeftTrigger { get; set; } = TriggerEffect.Off;

    public TriggerEffect RightTrigger { get; set; } = TriggerEffect.Off;

    public StickSettings LeftStick { get; set; } = new();

    public StickSettings RightStick { get; set; } = new();

    /// <summary>
    /// Remapeo de botones: nombre del botón (<see cref="PadButtons"/>) → id de la acción.
    /// Los botones que no aparecen usan su acción por defecto.
    /// </summary>
    public Dictionary<string, string> ButtonMap { get; set; } = new();

    /// <summary>Por USB, el botón Mute silencia el micrófono del mando (y enciende su LED).</summary>
    public bool MuteButtonMic { get; set; } = true;
}

/// <summary>Un paso de una macro: apretar una acción (tecla o botón Xbox), mantenerla y esperar.</summary>
public sealed class MacroStep
{
    /// <summary>Id de la acción (las mismas del remapeo: "keys:…" o "xbox:…").</summary>
    public string Action { get; set; } = "";

    public int HoldMs { get; set; } = 60;

    public int DelayMs { get; set; } = 60;
}

/// <summary>Secuencia de pasos que se lanza con un botón (se asigna en el remapeo).</summary>
public sealed class MacroDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public string Name { get; set; } = "Macro";

    /// <summary>true: se repite mientras se mantenga el botón. false: se ejecuta una vez por pulsación.</summary>
    public bool RepeatWhileHeld { get; set; }

    public List<MacroStep> Steps { get; set; } = new();
}

/// <summary>Todo lo que se guarda en profiles.json.</summary>
public sealed class PadPilotSettings
{
    /// <summary>Color de cada slot de jugador (J1-J4) para los mandos que usan "color del jugador".</summary>
    public Rgb[] SlotColors { get; set; } = PlayerSlots.DefaultColors();

    /// <summary>Crear un mando Xbox 360 virtual (ViGEmBus) por cada mando conectado.</summary>
    public bool EmulateXbox { get; set; }

    public Dictionary<string, ControllerProfile> Controllers { get; set; } = new();

    /// <summary>Asignación de botones de los mandos genéricos, por modelo (VID:PID).</summary>
    public Dictionary<string, Devices.GenericLayout> GenericLayouts { get; set; } = new();

    /// <summary>Ocultar los mandos físicos con HidHide mientras la emulación Xbox está activa.</summary>
    public bool HideControllers { get; set; }

    public LightingSettings Lighting { get; set; } = new();

    /// <summary>Idioma de la interfaz: "es", "en" o vacío (el de Windows).</summary>
    public string Language { get; set; } = "";

    /// <summary>"system", "light" o "dark".</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>"vertical" (compacto) u "horizontal" (lista de mandos a la izquierda).</summary>
    public string Layout { get; set; } = "vertical";

    public List<MacroDefinition> Macros { get; set; } = new();
}
