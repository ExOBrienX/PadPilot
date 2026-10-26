namespace PadPilot.Core.Protocol;

public enum TriggerMode
{
    Off,
    /// <summary>Resistencia constante desde una posición.</summary>
    Feedback,
    /// <summary>"Gatillo de arma": resistencia que se rompe en un punto (clic).</summary>
    Weapon,
    /// <summary>Vibración en el gatillo desde una posición.</summary>
    Vibration,
}

/// <summary>
/// Efecto de gatillo adaptativo del DualSense. Se codifica en 11 bytes (modo + 10 parámetros)
/// usando los efectos "oficiales" documentados por la comunidad (Nielk1).
/// </summary>
/// <param name="Position">Zona de inicio 0-9 (0 = arriba del recorrido).</param>
/// <param name="End">Zona final (solo Weapon).</param>
/// <param name="Strength">Fuerza/amplitud 1-8. 0 desactiva el efecto.</param>
/// <param name="Frequency">Frecuencia en Hz (solo Vibration).</param>
public sealed record TriggerEffect(
    TriggerMode Mode = TriggerMode.Off,
    byte Position = 0,
    byte End = 0,
    byte Strength = 0,
    byte Frequency = 0)
{
    public static TriggerEffect Off { get; } = new();

    public const int EncodedLength = 11;

    public void WriteTo(Span<byte> d)
    {
        d[..EncodedLength].Clear();

        switch (Mode)
        {
            case TriggerMode.Feedback when Strength > 0:
            {
                int start = Math.Min((int)Position, 9);
                uint force = (uint)(Math.Clamp((int)Strength, 1, 8) - 1) & 0x07;
                uint activeZones = 0, forceZones = 0;
                for (int i = start; i < 10; i++)
                {
                    activeZones |= 1u << i;
                    forceZones |= force << (3 * i);
                }
                d[0] = 0x21;
                WriteZones(d, activeZones, forceZones);
                break;
            }

            case TriggerMode.Weapon when Strength > 0:
            {
                int start = Math.Clamp((int)Position, 2, 7);
                int end = Math.Clamp((int)End, start + 1, 8);
                uint zones = (1u << start) | (1u << end);
                d[0] = 0x25;
                d[1] = (byte)zones;
                d[2] = (byte)(zones >> 8);
                d[3] = (byte)(Math.Clamp((int)Strength, 1, 8) - 1);
                break;
            }

            case TriggerMode.Vibration when Strength > 0 && Frequency > 0:
            {
                int start = Math.Min((int)Position, 9);
                uint amplitude = (uint)(Math.Clamp((int)Strength, 1, 8) - 1) & 0x07;
                uint activeZones = 0, amplitudeZones = 0;
                for (int i = start; i < 10; i++)
                {
                    activeZones |= 1u << i;
                    amplitudeZones |= amplitude << (3 * i);
                }
                d[0] = 0x26;
                WriteZones(d, activeZones, amplitudeZones);
                d[9] = Frequency;
                break;
            }

            default:
                d[0] = 0x05; // sin resistencia
                break;
        }
    }

    private static void WriteZones(Span<byte> d, uint active, uint values)
    {
        d[1] = (byte)active;
        d[2] = (byte)(active >> 8);
        d[3] = (byte)values;
        d[4] = (byte)(values >> 8);
        d[5] = (byte)(values >> 16);
        d[6] = (byte)(values >> 24);
    }
}
