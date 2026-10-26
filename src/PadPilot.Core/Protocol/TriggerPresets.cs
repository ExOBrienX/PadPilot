namespace PadPilot.Core.Protocol;

/// <summary>Combinación lista para usar de efectos en L2 y R2. R2 suele ser "disparar" y L2 "apuntar".</summary>
public sealed record TriggerPreset(string Id, string DefaultName, string DefaultDescription, TriggerEffect Left, TriggerEffect Right)
{
    /// <summary>Nombre en el idioma de la interfaz (si la app registró un traductor).</summary>
    public string Name => TriggerPresets.Localizer?.Invoke($"preset.{Id}") ?? DefaultName;

    public string Description => TriggerPresets.Localizer?.Invoke($"preset.{Id}.desc") ?? DefaultDescription;
}

public static class TriggerPresets
{
    /// <summary>Traductor de textos (clave → texto). Lo registra la interfaz; sin él se usan los nombres en español.</summary>
    public static Func<string, string>? Localizer { get; set; }

    public static IReadOnlyList<TriggerPreset> All { get; } =
    [
        new("pistol", "Pistola", "R2 con clic a media carrera; L2 suave para apuntar.",
            new(TriggerMode.Feedback, Position: 3, Strength: 2),
            new(TriggerMode.Weapon, Position: 4, End: 6, Strength: 6)),
        new("shotgun", "Escopeta", "R2 firme con clic al final del recorrido.",
            new(TriggerMode.Feedback, Position: 2, Strength: 3),
            new(TriggerMode.Weapon, Position: 2, End: 7, Strength: 8)),
        new("smg", "Ametralladora", "R2 vibra rápido al apretar.",
            new(TriggerMode.Feedback, Position: 3, Strength: 2),
            new(TriggerMode.Vibration, Position: 2, Strength: 8, Frequency: 12)),
        new("rifle", "Rifle automático", "R2 vibra más fino y desde más abajo.",
            new(TriggerMode.Feedback, Position: 3, Strength: 3),
            new(TriggerMode.Vibration, Position: 4, Strength: 6, Frequency: 25)),
        new("bow", "Arco", "Ambos gatillos tensos desde el inicio.",
            new(TriggerMode.Feedback, Position: 0, Strength: 5),
            new(TriggerMode.Feedback, Position: 0, Strength: 8)),
        new("car", "Coche", "L2 como freno firme; R2 vibra como un motor.",
            new(TriggerMode.Feedback, Position: 1, Strength: 7),
            new(TriggerMode.Vibration, Position: 1, Strength: 3, Frequency: 40)),
        new("off", "Sin efecto", "Gatillos libres.", TriggerEffect.Off, TriggerEffect.Off),
    ];

    /// <summary>El preset que coincide exactamente con estos efectos, o null si es una combinación propia.</summary>
    public static TriggerPreset? Find(TriggerEffect left, TriggerEffect right) =>
        All.FirstOrDefault(p => p.Left == left && p.Right == right);
}
