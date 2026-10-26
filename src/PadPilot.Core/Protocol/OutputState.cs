using PadPilot.Core.Models;

namespace PadPilot.Core.Protocol;

/// <summary>
/// Lo que queremos que el mando muestre. Se modifica y luego se envía con
/// <see cref="Devices.GamepadController.ApplyOutput"/>. No hay escritura periódica:
/// el mando solo recibe un reporte cuando la app cambia algo.
/// </summary>
public sealed class OutputState
{
    /// <summary>Motor izquierdo (grande, baja frecuencia).</summary>
    public byte RumbleStrong { get; set; }

    /// <summary>Motor derecho (pequeño, alta frecuencia).</summary>
    public byte RumbleWeak { get; set; }

    /// <summary>Color de la barra según el perfil (ya con la intensidad aplicada).</summary>
    public Rgb Lightbar { get; set; } = new(0, 0, 64);

    /// <summary>
    /// Color que manda sobre el del perfil: avisos de batería baja y de carga. Lo pone
    /// <see cref="Services.LightingService"/>; null = usar <see cref="Lightbar"/>.
    /// </summary>
    public Rgb? LightbarOverride { get; set; }

    /// <summary>Lo que realmente se envía a la barra.</summary>
    public Rgb EffectiveLightbar => LightbarOverride ?? Lightbar;

    /// <summary>Máscara de 5 bits de los LEDs de jugador (solo DualSense).</summary>
    public byte PlayerLeds { get; set; }

    public LedBrightness PlayerLedBrightness { get; set; } = LedBrightness.High;

    public TriggerEffect LeftTrigger { get; set; } = TriggerEffect.Off;

    public TriggerEffect RightTrigger { get; set; } = TriggerEffect.Off;

    public bool MuteLed { get; set; }
}
