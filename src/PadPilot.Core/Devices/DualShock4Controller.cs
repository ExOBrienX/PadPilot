using HidSharp;
using PadPilot.Core.Models;
using PadPilot.Core.Protocol;

namespace PadPilot.Core.Devices;

/// <summary>DualShock 4 (v1, v2 y adaptador inalámbrico). No tiene gatillos adaptativos ni LEDs de jugador.</summary>
public sealed class DualShock4Controller(HidDevice device, ControllerKind kind)
    : GamepadController(device, kind)
{
    private const byte UsbInputReport = 0x01;
    private const byte BtInputReport = 0x11;
    private const byte UsbOutputReport = 0x05;
    private const byte BtOutputReport = 0x11;
    private const byte FeatureCalibrationBt = 0x05;
    private const byte FeaturePairingInfoUsb = 0x12;

    private const int UsbOutputLength = 32;
    private const int BtOutputLength = 78;

    private const int InButtons0 = 4;
    private const int InStatus = 29;
    private const int InTouch = 34; // tras num_touch_reports (32) y el timestamp del primer reporte (33)

    public override string DisplayName => "DualShock 4";

    public override (int Width, int Height) TouchpadSize => (1920, 942);

    protected override void Initialize(HidStream stream)
    {
        // Igual que el DualSense: por BT hay que pedir la calibración para recibir el reporte completo 0x11.
        if (Connection == ConnectionKind.Bluetooth)
            stream.GetFeature(NewFeatureBuffer(FeatureCalibrationBt));
    }

    protected override string? TryReadMacAddress(HidStream stream)
    {
        if (Connection != ConnectionKind.Usb) return null; // por BT se usa el número de serie
        var buffer = NewFeatureBuffer(FeaturePairingInfoUsb);
        stream.GetFeature(buffer);
        return FormatMac(buffer.AsSpan(1, 6), reversed: true);
    }

    protected override bool TryParseInput(ReadOnlySpan<byte> r, out PadInput input, out BatteryInfo battery)
    {
        int o;
        if (Connection == ConnectionKind.Usb && r[0] == UsbInputReport && r.Length >= 64) o = 1;
        else if (r[0] == BtInputReport && r.Length >= 78) o = 3;
        else
        {
            input = default;
            battery = default;
            return false;
        }

        input = new PadInput(
            r[o], r[o + 1], r[o + 2], r[o + 3],
            r[o + 7], r[o + 8], // L2, R2 analógicos
            ButtonParser.Parse(r[o + InButtons0], r[o + InButtons0 + 1], r[o + InButtons0 + 2], hasMute: false),
            TouchPoint.Parse(r[(o + InTouch)..]),
            TouchPoint.Parse(r[(o + InTouch + 4)..]));
        battery = ParseBattery(r[o + InStatus]);
        return true;
    }

    private static BatteryInfo ParseBattery(byte status)
    {
        int level = status & 0x0F;
        bool cable = (status & 0x10) != 0;

        if (!cable) return new BatteryInfo(Math.Min(level * 10 + 5, 100), ChargeStatus.Discharging);
        if (level < 10) return new BatteryInfo(level * 10 + 5, ChargeStatus.Charging);
        if (level <= 11) return new BatteryInfo(100, ChargeStatus.Full);
        return new BatteryInfo(0, ChargeStatus.Error);
    }

    protected override int BuildOutputReport(Span<byte> buffer, OutputState s, bool lightsOnly)
    {
        Span<byte> c;
        int length;

        if (Connection == ConnectionKind.Usb)
        {
            buffer[0] = UsbOutputReport;
            c = buffer[1..];
            length = UsbOutputLength;
        }
        else
        {
            buffer[0] = BtOutputReport;
            buffer[1] = 0xC0; // HID + CRC32
            buffer[2] = 0x00; // control de audio
            c = buffer[3..];
            length = BtOutputLength;
        }

        // motores | barra de luz | parpadeo. Solo luces: sin el bit de motores, la vibración no se toca.
        c[0] = lightsOnly ? (byte)(0x02 | 0x04) : (byte)(0x01 | 0x02 | 0x04);
        c[3] = s.RumbleWeak;
        c[4] = s.RumbleStrong;
        var light = s.EffectiveLightbar;
        c[5] = light.R;
        c[6] = light.G;
        c[7] = light.B;
        c[8] = 0; // parpadeo encendido (0 = fijo)
        c[9] = 0; // parpadeo apagado

        if (Connection == ConnectionKind.Bluetooth)
            Crc32.Append(buffer[..BtOutputLength]);

        return length;
    }
}
