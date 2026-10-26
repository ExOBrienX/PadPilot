using System.Collections.ObjectModel;
using PadPilot.Core.Models;
using PadPilot.Core.Services;

namespace PadPilot.App.Services;

public enum ActionKind { Default, None, Xbox, Keys, Macro }

/// <summary>Botones del mando Xbox 360 virtual. Los valores son los bits del reporte XUSB.</summary>
public enum XboxButton : ushort
{
    None = 0,
    Up = 0x0001, Down = 0x0002, Left = 0x0004, Right = 0x0008,
    Start = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080,
    LeftShoulder = 0x0100, RightShoulder = 0x0200, Guide = 0x0400,
    A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000,
    // Los gatillos no son bits: se tratan aparte.
    LeftTrigger = 0xFFFE, RightTrigger = 0xFFFF,
}

/// <summary>Algo que puede hacer un botón del mando: un botón Xbox, una tecla/combinación o nada.</summary>
public sealed record ButtonAction(string Id, string Name, ActionKind Kind, XboxButton Xbox = XboxButton.None, ushort[]? Keys = null, string? MacroId = null)
{
    public override string ToString() => Name;
}

/// <summary>Catálogo de acciones y la asignación por defecto (la de un mando de Xbox).</summary>
public static class ButtonActions
{
    /// <summary>Botones físicos que se pueden remapear, en el orden en que se muestran.</summary>
    public static IReadOnlyList<PadButtons> Remappable { get; } =
    [
        PadButtons.Cross, PadButtons.Circle, PadButtons.Square, PadButtons.Triangle,
        PadButtons.L1, PadButtons.R1, PadButtons.L2, PadButtons.R2, PadButtons.L3, PadButtons.R3,
        PadButtons.Share, PadButtons.Options, PadButtons.PS, PadButtons.Touchpad, PadButtons.Mute,
        PadButtons.DPadUp, PadButtons.DPadDown, PadButtons.DPadLeft, PadButtons.DPadRight,
    ];

    public static XboxButton DefaultXbox(PadButtons source) => source switch
    {
        PadButtons.Cross => XboxButton.A,
        PadButtons.Circle => XboxButton.B,
        PadButtons.Square => XboxButton.X,
        PadButtons.Triangle => XboxButton.Y,
        PadButtons.L1 => XboxButton.LeftShoulder,
        PadButtons.R1 => XboxButton.RightShoulder,
        PadButtons.L2 => XboxButton.LeftTrigger,
        PadButtons.R2 => XboxButton.RightTrigger,
        PadButtons.L3 => XboxButton.LeftThumb,
        PadButtons.R3 => XboxButton.RightThumb,
        PadButtons.Share => XboxButton.Back,
        PadButtons.Options => XboxButton.Start,
        PadButtons.PS => XboxButton.Guide,
        PadButtons.Touchpad => XboxButton.Back,
        PadButtons.DPadUp => XboxButton.Up,
        PadButtons.DPadDown => XboxButton.Down,
        PadButtons.DPadLeft => XboxButton.Left,
        PadButtons.DPadRight => XboxButton.Right,
        _ => XboxButton.None, // Mute no tiene equivalente
    };

    public static ButtonAction Default { get; } = new("default", Loc.T("action.default"), ActionKind.Default);

    /// <summary>Acciones fijas: por defecto, desactivado, botones Xbox y teclas.</summary>
    public static IReadOnlyList<ButtonAction> All { get; } = BuildAll();

    /// <summary>Lo que puede hacer un paso de macro: teclas y botones Xbox.</summary>
    public static IReadOnlyList<ButtonAction> StepActions { get; } =
        All.Where(a => a.Kind is ActionKind.Keys or ActionKind.Xbox).ToList();

    /// <summary>Catálogo completo para los combos del remapeo: las fijas + una por macro. Se toca solo en el hilo de UI.</summary>
    public static ObservableCollection<ButtonAction> Catalog { get; } = new(All);

    private static readonly Dictionary<string, ButtonAction> ById = All.ToDictionary(a => a.Id);
    private static volatile Dictionary<string, ButtonAction> _macrosById = new();

    public static ButtonAction Find(string? id)
    {
        if (id is null) return Default;
        if (ById.TryGetValue(id, out var action)) return action;
        return _macrosById.TryGetValue(id, out var macro) ? macro : Default;
    }

    public static string MacroActionId(string macroId) => "macro:" + macroId;

    /// <summary>Vuelve a armar las entradas de macros del catálogo (al crear, renombrar o borrar una).</summary>
    public static void SetMacros(IEnumerable<MacroDefinition> macros)
    {
        var actions = macros
            .Select(m => new ButtonAction(MacroActionId(m.Id), Loc.T("action.macro", m.Name), ActionKind.Macro, MacroId: m.Id))
            .ToList();
        _macrosById = actions.ToDictionary(a => a.Id);

        for (int i = Catalog.Count - 1; i >= 0; i--)
            if (Catalog[i].Kind == ActionKind.Macro && !actions.Contains(Catalog[i]))
                Catalog.RemoveAt(i);

        // Las macros van justo después de "Desactivado", para encontrarlas rápido.
        for (int i = 0; i < actions.Count; i++)
        {
            int at = 2 + i;
            if (at < Catalog.Count && Catalog[at] == actions[i]) continue;
            Catalog.Remove(actions[i]);
            Catalog.Insert(Math.Min(at, Catalog.Count), actions[i]);
        }
    }

    public static string XboxName(XboxButton button) => button switch
    {
        XboxButton.A => "A", XboxButton.B => "B", XboxButton.X => "X", XboxButton.Y => "Y",
        XboxButton.LeftShoulder => "LB", XboxButton.RightShoulder => "RB",
        XboxButton.LeftTrigger => "LT", XboxButton.RightTrigger => "RT",
        XboxButton.LeftThumb => Loc.T("xbox.ls"), XboxButton.RightThumb => Loc.T("xbox.rs"),
        XboxButton.Back => Loc.T("xbox.back"), XboxButton.Start => Loc.T("xbox.start"), XboxButton.Guide => Loc.T("xbox.guide"),
        XboxButton.Up => Loc.T("dpad.up"), XboxButton.Down => Loc.T("dpad.down"),
        XboxButton.Left => Loc.T("dpad.left"), XboxButton.Right => Loc.T("dpad.right"),
        _ => Loc.T("xbox.none"),
    };

    private static List<ButtonAction> BuildAll()
    {
        var list = new List<ButtonAction>
        {
            Default,
            new("none", Loc.T("action.none"), ActionKind.None),
        };

        XboxButton[] xbox =
        [
            XboxButton.A, XboxButton.B, XboxButton.X, XboxButton.Y,
            XboxButton.LeftShoulder, XboxButton.RightShoulder, XboxButton.LeftTrigger, XboxButton.RightTrigger,
            XboxButton.LeftThumb, XboxButton.RightThumb, XboxButton.Back, XboxButton.Start, XboxButton.Guide,
            XboxButton.Up, XboxButton.Down, XboxButton.Left, XboxButton.Right,
        ];
        foreach (var b in xbox)
            list.Add(new($"xbox:{b}", Loc.T("action.xbox", XboxName(b)), ActionKind.Xbox, b));

        // Atajos de Windows útiles para jugar
        AddKeys(list, Loc.T("action.gamebar"), Vk.LWin, Vk.G);
        AddKeys(list, Loc.T("action.screenshot"), Vk.LWin, Vk.Alt, Vk.Snapshot);
        AddKeys(list, Loc.T("action.clip"), Vk.LWin, Vk.Alt, Vk.G);
        AddKeys(list, Loc.T("action.snip"), Vk.LWin, Vk.Shift, Vk.S);
        AddKeys(list, Loc.T("action.alttab"), Vk.Alt, Vk.Tab);
        AddKeys(list, Loc.T("action.desktop"), Vk.LWin, Vk.D);

        // Multimedia
        AddKeys(list, Loc.T("action.playpause"), Vk.MediaPlayPause);
        AddKeys(list, Loc.T("action.next"), Vk.MediaNext);
        AddKeys(list, Loc.T("action.prev"), Vk.MediaPrev);
        AddKeys(list, Loc.T("action.volup"), Vk.VolumeUp);
        AddKeys(list, Loc.T("action.voldown"), Vk.VolumeDown);
        AddKeys(list, Loc.T("action.mute"), Vk.VolumeMute);

        // Teclas sueltas
        AddKeys(list, Loc.T("action.key", "Esc"), Vk.Escape);
        AddKeys(list, Loc.T("action.key", "Enter"), Vk.Return);
        AddKeys(list, Loc.T("action.key", Loc.T("key.space")), Vk.Space);
        AddKeys(list, Loc.T("action.key", "Tab"), Vk.Tab);
        AddKeys(list, Loc.T("action.key", "Shift"), Vk.Shift);
        AddKeys(list, Loc.T("action.key", "Ctrl"), Vk.Control);
        AddKeys(list, Loc.T("action.key", "Alt"), Vk.Alt);
        AddKeys(list, Loc.T("action.key", "↑"), Vk.Up);
        AddKeys(list, Loc.T("action.key", "↓"), Vk.Down);
        AddKeys(list, Loc.T("action.key", "←"), Vk.Left);
        AddKeys(list, Loc.T("action.key", "→"), Vk.Right);
        for (char c = 'A'; c <= 'Z'; c++) AddKeys(list, Loc.T("action.key", c), c);
        for (char c = '0'; c <= '9'; c++) AddKeys(list, Loc.T("action.key", c), c);
        for (int f = 1; f <= 12; f++) AddKeys(list, Loc.T("action.key", $"F{f}"), (ushort)(Vk.F1 + f - 1));

        return list;
    }

    private static void AddKeys(List<ButtonAction> list, string name, params ushort[] keys) =>
        list.Add(new("keys:" + string.Join('+', keys.Select(k => k.ToString("X2"))), name, ActionKind.Keys, Keys: keys));
}

/// <summary>Códigos de tecla virtual de Windows usados por el catálogo.</summary>
internal static class Vk
{
    public const ushort Tab = 0x09, Return = 0x0D, Shift = 0x10, Control = 0x11, Alt = 0x12, Escape = 0x1B, Space = 0x20;
    public const ushort Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28, Snapshot = 0x2C;
    public const ushort D = 0x44, G = 0x47, S = 0x53, LWin = 0x5B, F1 = 0x70;
    public const ushort VolumeMute = 0xAD, VolumeDown = 0xAE, VolumeUp = 0xAF;
    public const ushort MediaNext = 0xB0, MediaPrev = 0xB1, MediaPlayPause = 0xB3;
}
