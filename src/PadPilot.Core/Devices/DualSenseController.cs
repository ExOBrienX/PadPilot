using HidSharp;
using PadPilot.Core.Models;
using PadPilot.Core.Protocol;

namespace PadPilot.Core.Devices;

/// <summary>
/// DualSense y DualSense Edge. Distribución de reportes basada en el driver hid-playstation
/// del kernel de Linux y en la documentación de la comunidad.
/// </summary>
public sealed class DualSenseController(HidDevice device, ControllerKind kind)
    : GamepadController(device, kind)
{
    private const byte UsbInputReport = 0x01;
    private const byte BtInputReport = 0x31;
    private const byte UsbOutputReport = 0x02;
    private const byte BtOutputReport = 0x31;
    private const byte FeatureCalibration = 0x05;
    private const byte FeaturePairingInfo = 0x09;

    private const int UsbOutputLength = 48;  // id + 47 bytes comunes
    private const int BtOutputLength = 78;   // id + seq + tag + 47 comunes + relleno + CRC32

    // Offsets dentro del bloque común de entrada (sin contar el report id)
    private const int InButtons0 = 7;
    private const int InTouch = 32;
    private const int InStatus = 52;

    private int _btSequence;
    private bool _ledsReleased;

    public override string DisplayName =>
        Kind == ControllerKind.DualSenseEdge ? "DualSense Edge" : "DualSense";

    public override bool SupportsAdaptiveTriggers => true;

    public override bool SupportsPlayerLeds => true;

    public override (int Width, int Height) TouchpadSize => (1920, 1080);

    protected override void Initialize(HidStream stream)
    {
        // Por Bluetooth el DualSense parte enviando un reporte reducido (0x01, sin batería).
        // Leer el feature report de calibración lo cambia al reporte completo 0x31.
        if (Connection == ConnectionKind.Bluetooth)
            stream.GetFeature(NewFeatureBuffer(FeatureCalibration));
    }

    protected override string? TryReadMacAddress(HidStream stream)
    {
        var buffer = NewFeatureBuffer(FeaturePairingInfo);
        stream.GetFeature(buffer);
        return FormatMac(buffer.AsSpan(1, 6), reversed: true);
    }

    protected override bool TryParseInput(ReadOnlySpan<byte> r, out PadInput input, out BatteryInfo battery)
    {
        int o;
        if (Connection == ConnectionKind.Usb && r[0] == UsbInputReport && r.Length >= 64) o = 1;
        else if (r[0] == BtInputReport && r.Length >= 78) o = 2;
        else
        {
            input = default;
            battery = default;
            return false;
        }

        input = new PadInput(
            r[o], r[o + 1], r[o + 2], r[o + 3], r[o + 4], r[o + 5],
            ButtonParser.Parse(r[o + InButtons0], r[o + InButtons0 + 1], r[o + InButtons0 + 2], hasMute: true),
            TouchPoint.Parse(r[(o + InTouch)..]),
            TouchPoint.Parse(r[(o + InTouch + 4)..]));
        battery = ParseBattery(r[o + InStatus]);
        return true;
    }

    private static BatteryInfo ParseBattery(byte status)
    {
        int level = status & 0x0F;
        int percent = Math.Min(level * 10 + 5, 100);
        return (status >> 4) switch
        {
            0x0 => new BatteryInfo(percent, ChargeStatus.Discharging),
            0x1 => new BatteryInfo(percent, ChargeStatus.Charging),
            0x2 => new BatteryInfo(100, ChargeStatus.Full),
            _ => new BatteryInfo(0, ChargeStatus.Error), // error de voltaje/temperatura
        };
    }

    protected override int BuildOutputReport(Span<byte> buffer, OutputState s, bool lightsOnly)
    {
        Span<byte> c;
        int length;

        if (Connection == ConnectionKind.Usb)
        {
            buffer[0] = UsbOutputReport;
            c = buffer.Slice(1, 47);
            length = UsbOutputLength;
        }
        else
        {
            buffer[0] = BtOutputReport;
            buffer[1] = (byte)(_btSequence << 4);
            _btSequence = (_btSequence + 1) & 0x0F;
            buffer[2] = 0x10; // tag
            c = buffer.Slice(3, 47);
            length = BtOutputLength;
        }

        // valid_flag0: vibración compatible | usar motores (no hápticos por audio) | gatillo der. | gatillo izq.
        // Solo luces: nada de esto, así no se corta la vibración ni los gatillos que esté usando un juego.
        c[0] = lightsOnly ? (byte)0 : (byte)(0x01 | 0x02 | 0x04 | 0x08);
        // valid_flag1: LED de mute | barra de luz | LEDs de jugador
        c[1] = 0x01 | 0x04 | 0x10;

        c[2] = s.RumbleWeak;   // motor derecho
        c[3] = s.RumbleStrong; // motor izquierdo
        c[8] = (byte)(s.MuteLed ? 1 : 0);

        s.RightTrigger.WriteTo(c.Slice(10, TriggerEffect.EncodedLength));
        s.LeftTrigger.WriteTo(c.Slice(21, TriggerEffect.EncodedLength));

        // valid_flag2: brillo de LEDs | vibración mejorada (firmware nuevo)
        c[38] = 0x01 | 0x04;
        if (!_ledsReleased)
        {
            // La primera vez apagamos la animación azul de arranque para poder controlar la barra.
            c[38] |= 0x02;
            c[41] = 0x02;
            _ledsReleased = true;
        }

        c[42] = (byte)s.PlayerLedBrightness;
        c[43] = (byte)(s.PlayerLeds & 0x1F);
        var light = s.EffectiveLightbar;
        c[44] = light.R;
        c[45] = light.G;
        c[46] = light.B;

        if (Connection == ConnectionKind.Bluetooth)
            Crc32.Append(buffer[..BtOutputLength]);

        return length;
    }

    protected override void PrepareForClose(OutputState state)
    {
        base.PrepareForClose(state);
        state.LeftTrigger = TriggerEffect.Off;
        state.RightTrigger = TriggerEffect.Off;
    }
}
