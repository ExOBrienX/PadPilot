using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;
using PadPilot.Core.Models;
using PadPilot.Core.Protocol;

namespace PadPilot.Core.Devices;

/// <summary>
/// Cualquier gamepad HID (DirectInput / tipo Android). Lee el descriptor HID del propio mando para saber
/// dónde están sus ejes, botones y cruceta, y los traduce con una <see cref="GenericLayout"/> al mismo
/// <see cref="PadInput"/> que usan los DualSense: así el probador, el remapeo y la emulación Xbox funcionan igual.
/// </summary>
public sealed class GenericHidController : GamepadController
{
    private enum SlotKind : byte { None, Axis, Button, Hat }

    private readonly ReportDescriptor _descriptor;
    private readonly DeviceItemInputParser _parser;
    private readonly uint[] _axisUsages;
    private readonly int _buttonCount;
    private readonly bool _hasHat;
    private readonly byte[] _reportBuffer;
    private (SlotKind Kind, int Index, int Min, int Max)[]? _slots; // índice de valor del parser → dónde guardarlo

    // Estado crudo (solo lo escribe el hilo lector)
    private readonly object _rawLock = new();
    private readonly byte[] _axisValues;
    private readonly bool[] _buttons;
    private int _hat = -1;

    private volatile Compiled _compiled;

    private GenericHidController(HidDevice device, ReportDescriptor descriptor, DeviceItem item)
        : base(device, ControllerKind.Generic)
    {
        _descriptor = descriptor;
        _parser = item.CreateDeviceItemInputParser();
        _reportBuffer = new byte[Math.Max(device.GetMaxInputReportLength(), 1)];

        var axes = new List<uint>();
        int maxButton = 0;
        foreach (var report in item.Reports.Where(r => r.ReportType == ReportType.Input))
            foreach (var data in report.DataItems)
                foreach (uint usage in data.Usages.GetAllValues())
                {
                    if (HidUsages.IsAxis(usage) && !axes.Contains(usage)) axes.Add(usage);
                    else if (usage == HidUsages.HatSwitch) _hasHat = true;
                    else if (usage >> 16 == HidUsages.ButtonPage) maxButton = Math.Max(maxButton, (int)(usage & 0xFFFF));
                }

        _axisUsages = axes.ToArray();
        _buttonCount = maxButton;
        _axisValues = new byte[_axisUsages.Length];
        Array.Fill(_axisValues, (byte)128);
        _buttons = new bool[_buttonCount];

        string product = "", maker = "";
        try { product = device.GetProductName()?.Trim() ?? ""; } catch (Exception) { }
        try { maker = device.GetManufacturer()?.Trim() ?? ""; } catch (Exception) { }
        Model = string.Join(" ", new[] { maker, product }.Where(s => s.Length > 0).Distinct());
        if (Model.Length == 0) Model = ModelKey;

        _compiled = Compile(StandardLayout());
    }

    /// <summary>Devuelve un controlador si el dispositivo es un gamepad o joystick HID; null si no.</summary>
    public static GenericHidController? TryCreate(HidDevice device)
    {
        var descriptor = device.GetReportDescriptor();
        var item = descriptor.DeviceItems.FirstOrDefault(i =>
            i.Usages.GetAllValues().Any(u => u is HidUsages.Gamepad or HidUsages.Joystick));
        return item is null ? null : new GenericHidController(device, descriptor, item);
    }

    public override string DisplayName => "Mando genérico";

    /// <summary>Fabricante y producto según el propio mando (p. ej. "G801 NGDS").</summary>
    public string Model { get; }

    /// <summary>VID:PID. La asignación de botones se guarda por modelo, no por mando.</summary>
    public string ModelKey => $"{Device.VendorID:X4}:{Device.ProductID:X4}";

    public IReadOnlyList<uint> AxisUsages => _axisUsages;

    public int ButtonCount => _buttonCount;

    public bool HasHat => _hasHat;

    public override bool SupportsLightbar => false;
    public override bool SupportsRumble => false;
    public override bool SupportsTouchpad => false;
    public override bool ReportsBattery => false;
    public override (int Width, int Height) TouchpadSize => (1, 1);

    public GenericLayout StandardLayout() => GenericLayout.Standard(_axisUsages, _buttonCount, _hasHat);

    /// <summary>Cambia la asignación en caliente (desde cualquier hilo).</summary>
    public void SetLayout(GenericLayout layout) => _compiled = Compile(layout);

    public GenericRawState RawSnapshot()
    {
        lock (_rawLock)
            return new GenericRawState(_axisUsages, (byte[])_axisValues.Clone(), (bool[])_buttons.Clone(), _hat);
    }

    // ---- Lectura ----

    protected override bool TryParseInput(ReadOnlySpan<byte> report, out PadInput input, out BatteryInfo battery)
    {
        input = default;
        battery = BatteryInfo.Unknown;
        if (report.Length == 0) return false;

        report.CopyTo(_reportBuffer);
        if (!_descriptor.TryGetReport(ReportType.Input, _reportBuffer[0], out var hidReport)) return false;
        if (!_parser.TryParseReport(_reportBuffer, 0, hidReport)) return false;

        _slots ??= BuildSlots();

        lock (_rawLock)
        {
            for (int i = 0; i < _slots.Length && i < _parser.ValueCount; i++)
            {
                var (kind, index, min, max) = _slots[i];
                if (kind == SlotKind.None) continue;
                int value = _parser.GetValue(i).GetLogicalValue();

                switch (kind)
                {
                    case SlotKind.Axis:
                        _axisValues[index] = max > min
                            ? (byte)Math.Clamp((value - min) * 255L / (max - min), 0, 255)
                            : (byte)128;
                        break;
                    case SlotKind.Button:
                        _buttons[index] = value != 0;
                        break;
                    case SlotKind.Hat:
                        // Fuera de rango = cruceta suelta. Con 4 posiciones se pasa a 8 (solo diagonales pares).
                        int positions = max - min + 1;
                        _hat = value < min || value > max ? -1 : (value - min) * (8 / Math.Max(positions, 1));
                        break;
                }
            }

            input = _compiled.Map(_axisValues, _buttons, _hat);
        }
        return true;
    }

    private (SlotKind, int, int, int)[] BuildSlots()
    {
        var slots = new (SlotKind, int, int, int)[_parser.ValueCount];
        for (int i = 0; i < slots.Length; i++)
        {
            var value = _parser.GetValue(i);
            uint usage = value.Usages.FirstOrDefault();
            int min = value.DataItem.LogicalMinimum, max = value.DataItem.LogicalMaximum;

            if (usage == HidUsages.HatSwitch) slots[i] = (SlotKind.Hat, 0, min, max);
            else if (usage >> 16 == HidUsages.ButtonPage && (usage & 0xFFFF) is > 0 and var n && n <= _buttonCount)
                slots[i] = (SlotKind.Button, (int)n - 1, min, max);
            else if (Array.IndexOf(_axisUsages, usage) is >= 0 and var a) slots[i] = (SlotKind.Axis, a, min, max);
        }
        return slots;
    }

    protected override int BuildOutputReport(Span<byte> buffer, OutputState state, bool lightsOnly) => 0; // sin salida

    // ---- Asignación compilada: arrays en vez de diccionarios, para el hilo lector ----

    private Compiled Compile(GenericLayout layout) => new(layout, _axisUsages);

    private sealed class Compiled
    {
        private readonly record struct Source(SlotKind Kind, int Index, bool Inverted);

        private static readonly PadButtons[] HatDirections =
        [
            PadButtons.DPadUp, PadButtons.DPadUp | PadButtons.DPadRight, PadButtons.DPadRight,
            PadButtons.DPadRight | PadButtons.DPadDown, PadButtons.DPadDown,
            PadButtons.DPadDown | PadButtons.DPadLeft, PadButtons.DPadLeft, PadButtons.DPadLeft | PadButtons.DPadUp,
        ];

        private readonly (PadButtons Target, Source Source)[] _buttons;
        private readonly Source _lx, _ly, _rx, _ry, _l2, _r2;
        private readonly bool _dpadFromHat;

        public Compiled(GenericLayout layout, uint[] axes)
        {
            Source Parse(string? text)
            {
                if (text is null) return default;
                if (text == GenericLayout.Hat) return new(SlotKind.Hat, 0, false);
                var parts = text.Split(':');
                if (parts[0] == "button" && int.TryParse(parts[1], out int n)) return new(SlotKind.Button, n - 1, false);
                if (parts[0] == "axis" && uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out uint u)
                    && Array.IndexOf(axes, u) is >= 0 and var idx)
                    return new(SlotKind.Axis, idx, parts.Length > 2 && parts[2] == "inv");
                return default;
            }

            var map = layout.Map;
            Source Get(string key) => Parse(map.GetValueOrDefault(key));

            _lx = Get(GenericLayout.LeftX); _ly = Get(GenericLayout.LeftY);
            _rx = Get(GenericLayout.RightX); _ry = Get(GenericLayout.RightY);
            _l2 = Get(nameof(PadButtons.L2)); _r2 = Get(nameof(PadButtons.R2));
            _dpadFromHat = map.GetValueOrDefault(GenericLayout.DPad) == GenericLayout.Hat;

            _buttons = Enum.GetValues<PadButtons>()
                .Where(b => b is not (PadButtons.None or PadButtons.L2 or PadButtons.R2))
                .Select(b => (b, Get(b.ToString())))
                .Where(x => x.Item2.Kind != SlotKind.None)
                .ToArray();
        }

        public PadInput Map(byte[] axes, bool[] buttons, int hat)
        {
            var result = PadButtons.None;
            foreach (var (target, source) in _buttons)
                if (Pressed(source, axes, buttons)) result |= target;

            if (_dpadFromHat && hat is >= 0 and < 8) result |= HatDirections[hat];

            byte l2 = Trigger(_l2, axes, buttons), r2 = Trigger(_r2, axes, buttons);
            if (l2 > 30) result |= PadButtons.L2;
            if (r2 > 30) result |= PadButtons.R2;

            return new PadInput(
                AxisValue(_lx, axes), AxisValue(_ly, axes), AxisValue(_rx, axes), AxisValue(_ry, axes),
                l2, r2, result);
        }

        private static bool Pressed(Source s, byte[] axes, bool[] buttons) => s.Kind switch
        {
            SlotKind.Button => s.Index < buttons.Length && buttons[s.Index],
            SlotKind.Axis => (s.Inverted ? 255 - axes[s.Index] : axes[s.Index]) > 192,
            _ => false,
        };

        private static byte AxisValue(Source s, byte[] axes) =>
            s.Kind == SlotKind.Axis ? (byte)(s.Inverted ? 255 - axes[s.Index] : axes[s.Index]) : (byte)128;

        private static byte Trigger(Source s, byte[] axes, bool[] buttons) => s.Kind switch
        {
            SlotKind.Axis => (byte)(s.Inverted ? 255 - axes[s.Index] : axes[s.Index]),
            SlotKind.Button => s.Index < buttons.Length && buttons[s.Index] ? (byte)255 : (byte)0,
            _ => 0,
        };
    }
}
