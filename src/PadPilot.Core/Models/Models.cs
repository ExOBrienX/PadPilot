namespace PadPilot.Core.Models;

public enum ControllerKind { DualSense, DualSenseEdge, DualShock4, Generic }

public enum ConnectionKind { Usb, Bluetooth }

public enum ChargeStatus { Unknown, Discharging, Charging, Full, Error }

/// <summary>Brillo de los LEDs de jugador del DualSense.</summary>
public enum LedBrightness : byte { High = 0, Medium = 1, Low = 2 }

/// <summary>Estado de batería tal como lo reporta el mando.</summary>
/// <remarks>El mando reporta en pasos de 10 %, por eso los valores son 5, 15, 25… 95, 100.</remarks>
public readonly record struct BatteryInfo(int Percent, ChargeStatus Status)
{
    public static readonly BatteryInfo Unknown = new(0, ChargeStatus.Unknown);
}

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public Rgb Scale(double factor)
    {
        factor = Math.Clamp(factor, 0, 1);
        return new((byte)(R * factor), (byte)(G * factor), (byte)(B * factor));
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

[Flags]
public enum PadButtons : uint
{
    None = 0,
    DPadUp = 1 << 0,
    DPadRight = 1 << 1,
    DPadDown = 1 << 2,
    DPadLeft = 1 << 3,
    Square = 1 << 4,
    Cross = 1 << 5,
    Circle = 1 << 6,
    Triangle = 1 << 7,
    L1 = 1 << 8,
    R1 = 1 << 9,
    L2 = 1 << 10,
    R2 = 1 << 11,
    Share = 1 << 12,   // "Create" en DualSense
    Options = 1 << 13,
    L3 = 1 << 14,
    R3 = 1 << 15,
    PS = 1 << 16,
    Touchpad = 1 << 17,
    Mute = 1 << 18,    // solo DualSense
}

/// <summary>
/// Corrección de un stick: centro calibrado (para sticks que no vuelven a 128) y zona muerta radial
/// (para ignorar el drift). Se aplica a la salida de la emulación Xbox.
/// </summary>
public sealed class StickSettings
{
    public double CenterX { get; set; } = 128;

    public double CenterY { get; set; } = 128;

    /// <summary>Zona muerta en % del recorrido (0-60).</summary>
    public double Deadzone { get; set; } = 6;

    /// <summary>Devuelve el stick corregido en -1..1 (Y positiva hacia abajo, como el mando).</summary>
    public (double X, double Y) Apply(byte rawX, byte rawY)
    {
        double x = Normalize(rawX, CenterX);
        double y = Normalize(rawY, CenterY);
        double magnitude = Math.Sqrt(x * x + y * y);
        double dz = Math.Clamp(Deadzone, 0, 60) / 100.0;

        if (magnitude <= dz || magnitude == 0) return (0, 0);

        // Reescala para que justo fuera de la zona muerta empiece en 0 y no se pierda precisión.
        double scaled = Math.Min((magnitude - dz) / (1 - dz), 1);
        return (x / magnitude * scaled, y / magnitude * scaled);
    }

    /// <summary>Cada mitad del eje se escala por separado: un centro corrido no pierde el tope.</summary>
    private static double Normalize(byte value, double center)
    {
        center = Math.Clamp(center, 1, 254);
        double d = value - center;
        return Math.Clamp(d >= 0 ? d / (255 - center) : d / center, -1, 1);
    }
}

/// <summary>Un dedo sobre el touchpad. Coordenadas en la resolución del touchpad de cada modelo.</summary>
public readonly record struct TouchPoint(bool Active, ushort X, ushort Y)
{
    /// <summary>DualSense y DS4 comparten el formato: byte de contacto (bit 7 = sin dedo) + 12 bits X + 12 bits Y.</summary>
    public static TouchPoint Parse(ReadOnlySpan<byte> p) =>
        (p[0] & 0x80) != 0
            ? default
            : new(true, (ushort)(p[1] | (p[2] & 0x0F) << 8), (ushort)(p[2] >> 4 | p[3] << 4));
}

/// <summary>Snapshot de la entrada del mando (sticks 0-255, gatillos 0-255, botones y touchpad).</summary>
public readonly record struct PadInput(
    byte LeftX, byte LeftY, byte RightX, byte RightY,
    byte L2, byte R2, PadButtons Buttons,
    TouchPoint Touch0 = default, TouchPoint Touch1 = default);
