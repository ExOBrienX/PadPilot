namespace PadPilot.Core.Devices;

/// <summary>Usages HID que interesan en un gamepad (página &lt;&lt; 16 | id).</summary>
public static class HidUsages
{
    public const uint Joystick = 0x0001_0004, Gamepad = 0x0001_0005;
    public const uint X = 0x0001_0030, Y = 0x0001_0031, Z = 0x0001_0032;
    public const uint Rx = 0x0001_0033, Ry = 0x0001_0034, Rz = 0x0001_0035;
    public const uint Slider = 0x0001_0036, Dial = 0x0001_0037, HatSwitch = 0x0001_0039;
    public const uint Accelerator = 0x0002_00C4, Brake = 0x0002_00C5;
    public const uint ButtonPage = 0x0009;

    public static bool IsAxis(uint usage) =>
        usage is >= X and <= Dial || usage is Accelerator or Brake;

    public static string AxisName(uint usage) => usage switch
    {
        X => "X", Y => "Y", Z => "Z", Rx => "Rx", Ry => "Ry", Rz => "Rz",
        Slider => "Slider", Dial => "Dial", Accelerator => "Acelerador", Brake => "Freno",
        _ => $"0x{usage:X8}",
    };
}

/// <summary>
/// Asignación de un mando genérico: qué botón, eje o cruceta física corresponde a cada control.
/// Destinos: nombres de <see cref="Models.PadButtons"/> (Cross = A, Circle = B…), "LeftX", "LeftY",
/// "RightX", "RightY" y "DPad". Orígenes: "button:N", "axis:USAGE" (con ":inv" si va al revés) o "hat".
/// </summary>
public sealed class GenericLayout
{
    public const string LeftX = "LeftX", LeftY = "LeftY", RightX = "RightX", RightY = "RightY", DPad = "DPad";

    /// <summary>true si la armó el usuario con el asistente; false si es la estándar automática.</summary>
    public bool IsCustom { get; set; }

    public Dictionary<string, string> Map { get; set; } = new();

    public static string Button(int number) => $"button:{number}";

    public static string Axis(uint usage, bool inverted = false) => $"axis:{usage:X8}" + (inverted ? ":inv" : "");

    public const string Hat = "hat";

    /// <summary>
    /// Asignación estándar de los gamepads HID tipo Android (la mayoría de los genéricos por Bluetooth):
    /// X/Y stick izquierdo, Z/Rz (o Rx/Ry) stick derecho, Freno/Acelerador = LT/RT, y botones
    /// 1=A 2=B 4=X 5=Y 7=LB 8=RB 9=LT 10=RT 11=Select 12=Start 13=Home 14=LS 15=RS.
    /// Con 12 botones o menos se usa el orden DirectInput clásico (1=X 2=A 3=B 4=Y…).
    /// </summary>
    public static GenericLayout Standard(IReadOnlyCollection<uint> axes, int buttonCount, bool hasHat)
    {
        var map = new Dictionary<string, string>();
        void Set(string target, string source) => map[target] = source;

        if (axes.Contains(HidUsages.X)) Set(LeftX, Axis(HidUsages.X));
        if (axes.Contains(HidUsages.Y)) Set(LeftY, Axis(HidUsages.Y));
        if (axes.Contains(HidUsages.Z) && axes.Contains(HidUsages.Rz))
        {
            Set(RightX, Axis(HidUsages.Z));
            Set(RightY, Axis(HidUsages.Rz));
        }
        else if (axes.Contains(HidUsages.Rx) && axes.Contains(HidUsages.Ry))
        {
            Set(RightX, Axis(HidUsages.Rx));
            Set(RightY, Axis(HidUsages.Ry));
        }
        if (hasHat) Set(DPad, Hat);

        bool android = buttonCount >= 13;
        (string Target, int Button)[] buttons = android
            ?
            [
                ("Cross", 1), ("Circle", 2), ("Square", 4), ("Triangle", 5),
                ("L1", 7), ("R1", 8), ("L2", 9), ("R2", 10),
                ("Share", 11), ("Options", 12), ("PS", 13), ("L3", 14), ("R3", 15),
            ]
            :
            [
                ("Square", 1), ("Cross", 2), ("Circle", 3), ("Triangle", 4),
                ("L1", 5), ("R1", 6), ("L2", 7), ("R2", 8),
                ("Share", 9), ("Options", 10), ("L3", 11), ("R3", 12),
            ];
        foreach (var (target, button) in buttons)
            if (button <= buttonCount) Set(target, Button(button));

        // Gatillos analógicos: si existen, mejor que los botones digitales.
        if (axes.Contains(HidUsages.Brake)) Set("L2", Axis(HidUsages.Brake));
        if (axes.Contains(HidUsages.Accelerator)) Set("R2", Axis(HidUsages.Accelerator));

        return new GenericLayout { IsCustom = false, Map = map };
    }
}

/// <summary>Lectura cruda de un mando genérico, para el asistente de asignación.</summary>
public sealed record GenericRawState(uint[] AxisUsages, byte[] AxisValues, bool[] Buttons, int Hat)
{
    /// <summary>Botón presionado con su número HID (1 = el primero), o 0 si ninguno.</summary>
    public int FirstPressedButton(GenericRawState? baseline = null)
    {
        for (int i = 0; i < Buttons.Length; i++)
            if (Buttons[i] && (baseline is null || !baseline.Buttons[i])) return i + 1;
        return 0;
    }
}
