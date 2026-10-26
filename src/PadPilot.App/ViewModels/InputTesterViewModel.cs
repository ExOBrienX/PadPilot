using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PadPilot.App.Services;
using PadPilot.Core.Devices;
using PadPilot.Core.Models;
using PadPilot.Core.Services;

namespace PadPilot.App.ViewModels;

/// <summary>Un botón del probador: se ilumina mientras está presionado.</summary>
public sealed partial class ButtonLight(string name, PadButtons button) : ObservableObject
{
    public string Name { get; } = name;

    public PadButtons Button { get; } = button;

    [ObservableProperty] public partial bool IsPressed { get; set; }
}

/// <summary>
/// Probador de entradas: sticks, gatillos, botones y touchpad en vivo. Solo lee el mando (a ~60 fps)
/// mientras la pestaña está a la vista; el resto del tiempo no consume nada.
/// </summary>
public sealed partial class InputTesterViewModel : ObservableObject
{
    public const double StickArea = 110;
    public const double StickDot = 16;
    public const double TouchWidth = 300;
    private const double TouchDot = 22;

    private readonly GamepadController _controller;
    private readonly ControllerProfile _profile;
    private readonly Action _sticksChanged;
    private readonly DispatcherTimer _timer;
    private bool _syncing;

    public InputTesterViewModel(GamepadController controller, ControllerProfile profile, Action sticksChanged)
    {
        _controller = controller;
        _profile = profile;
        _sticksChanged = sticksChanged;
        _syncing = true;
        LeftDeadzone = profile.LeftStick.Deadzone;
        RightDeadzone = profile.RightStick.Deadzone;
        _syncing = false;
        var (w, h) = controller.TouchpadSize;
        TouchHeight = Math.Round(TouchWidth * h / w);

        if (controller.Kind == ControllerKind.Generic)
        {
            Buttons =
            [
                new("A", PadButtons.Cross), new("B", PadButtons.Circle), new("X", PadButtons.Square), new("Y", PadButtons.Triangle),
                new("↑", PadButtons.DPadUp), new("↓", PadButtons.DPadDown), new("←", PadButtons.DPadLeft), new("→", PadButtons.DPadRight),
                new("LB", PadButtons.L1), new("RB", PadButtons.R1), new("LT", PadButtons.L2), new("RT", PadButtons.R2),
                new("LS", PadButtons.L3), new("RS", PadButtons.R3),
                new("Back", PadButtons.Share), new("Start", PadButtons.Options), new("Home", PadButtons.PS),
            ];
        }
        else
        {
            bool ds = controller.Kind != ControllerKind.DualShock4;
            Buttons =
            [
                new("✕", PadButtons.Cross), new("○", PadButtons.Circle), new("□", PadButtons.Square), new("△", PadButtons.Triangle),
                new("↑", PadButtons.DPadUp), new("↓", PadButtons.DPadDown), new("←", PadButtons.DPadLeft), new("→", PadButtons.DPadRight),
                new("L1", PadButtons.L1), new("R1", PadButtons.R1), new("L2", PadButtons.L2), new("R2", PadButtons.R2),
                new("L3", PadButtons.L3), new("R3", PadButtons.R3),
                new(ds ? "Create" : "Share", PadButtons.Share), new("Options", PadButtons.Options),
                new("PS", PadButtons.PS), new("Touchpad", PadButtons.Touchpad),
            ];
            if (ds) Buttons.Add(new("Mute", PadButtons.Mute));
        }
        HasTouchpad = controller.SupportsTouchpad;

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Refresh());
        Refresh();
    }

    public List<ButtonLight> Buttons { get; }

    public bool HasTouchpad { get; }

    public double TouchHeight { get; }

    [ObservableProperty] public partial double LeftDotX { get; set; }
    [ObservableProperty] public partial double LeftDotY { get; set; }
    [ObservableProperty] public partial double RightDotX { get; set; }
    [ObservableProperty] public partial double RightDotY { get; set; }
    [ObservableProperty] public partial string LeftStickText { get; set; } = "";
    [ObservableProperty] public partial string RightStickText { get; set; } = "";
    [ObservableProperty] public partial int L2Value { get; set; }
    [ObservableProperty] public partial int R2Value { get; set; }

    // Salida corregida (centro + zona muerta): es lo que recibe el Xbox virtual.
    [ObservableProperty] public partial double LeftOutX { get; set; }
    [ObservableProperty] public partial double LeftOutY { get; set; }
    [ObservableProperty] public partial double RightOutX { get; set; }
    [ObservableProperty] public partial double RightOutY { get; set; }
    [ObservableProperty] public partial string LeftOutText { get; set; } = "";
    [ObservableProperty] public partial string RightOutText { get; set; } = "";

    [ObservableProperty] public partial double LeftDeadzone { get; set; }
    [ObservableProperty] public partial double RightDeadzone { get; set; }
    [ObservableProperty] public partial string CalibrationText { get; set; } =
        Loc.T("test.calHint");

    public double LeftRing => RingSize(LeftDeadzone);
    public double RightRing => RingSize(RightDeadzone);
    public double LeftRingOffset => (StickArea - LeftRing) / 2;
    public double RightRingOffset => (StickArea - RightRing) / 2;

    partial void OnLeftDeadzoneChanged(double value)
    {
        OnPropertyChanged(nameof(LeftRing));
        OnPropertyChanged(nameof(LeftRingOffset));
        if (_syncing) return;
        _profile.LeftStick.Deadzone = Math.Round(value);
        _sticksChanged();
    }

    partial void OnRightDeadzoneChanged(double value)
    {
        OnPropertyChanged(nameof(RightRing));
        OnPropertyChanged(nameof(RightRingOffset));
        if (_syncing) return;
        _profile.RightStick.Deadzone = Math.Round(value);
        _sticksChanged();
    }

    private static double RingSize(double deadzone) => Math.Max(deadzone / 100 * (StickArea - StickDot) + StickDot / 2, 0);

    /// <summary>
    /// Mide 1 segundo con los sticks sueltos: toma el promedio como centro y sube la zona muerta
    /// lo justo para tapar el temblor que quede.
    /// </summary>
    [RelayCommand]
    private async Task CalibrateAsync()
    {
        CalibrationText = Loc.T("test.measuring");
        var samples = new List<PadInput>();
        for (int i = 0; i < 50; i++)
        {
            samples.Add(_controller.Input);
            await Task.Delay(20);
        }

        string left = Calibrate(_profile.LeftStick, samples.Select(s => (s.LeftX, s.LeftY)).ToList());
        string right = Calibrate(_profile.RightStick, samples.Select(s => (s.RightX, s.RightY)).ToList());

        _syncing = true;
        LeftDeadzone = _profile.LeftStick.Deadzone;
        RightDeadzone = _profile.RightStick.Deadzone;
        _syncing = false;
        _sticksChanged();
        CalibrationText = Loc.T("test.done", left, right);
    }

    private static string Calibrate(StickSettings stick, List<(byte X, byte Y)> samples)
    {
        double cx = samples.Average(s => s.X), cy = samples.Average(s => s.Y);
        stick.CenterX = Math.Round(cx, 1);
        stick.CenterY = Math.Round(cy, 1);

        // Temblor: lo más lejos que se fue del nuevo centro, en % del recorrido.
        double jitter = samples.Max(s => Math.Max(Math.Abs(s.X - cx), Math.Abs(s.Y - cy))) / 127.0 * 100;
        double needed = Math.Ceiling(jitter) + 3;
        if (stick.Deadzone < needed) stick.Deadzone = Math.Min(needed, 60);

        double offset = Math.Max(Math.Abs(cx - 128), Math.Abs(cy - 128)) / 128.0 * 100;
        return offset >= 3
            ? Loc.T("test.corrected", offset, stick.Deadzone)
            : Loc.T("test.centered", stick.Deadzone);
    }

    [ObservableProperty] public partial bool Touch0Visible { get; set; }
    [ObservableProperty] public partial double Touch0X { get; set; }
    [ObservableProperty] public partial double Touch0Y { get; set; }
    [ObservableProperty] public partial bool Touch1Visible { get; set; }
    [ObservableProperty] public partial double Touch1X { get; set; }
    [ObservableProperty] public partial double Touch1Y { get; set; }
    [ObservableProperty] public partial string TouchText { get; set; } = "";

    public bool IsLive
    {
        get => _timer.IsEnabled;
        set
        {
            if (value == _timer.IsEnabled) return;
            if (value) { Refresh(); _timer.Start(); }
            else _timer.Stop();
        }
    }

    private void Refresh()
    {
        var input = _controller.Input;

        LeftDotX = StickPos(input.LeftX);
        LeftDotY = StickPos(input.LeftY);
        RightDotX = StickPos(input.RightX);
        RightDotY = StickPos(input.RightY);
        LeftStickText = $"X {input.LeftX,3}  Y {input.LeftY,3}";
        RightStickText = $"X {input.RightX,3}  Y {input.RightY,3}";
        L2Value = input.L2;

        var (lx, ly) = _profile.LeftStick.Apply(input.LeftX, input.LeftY);
        var (rx, ry) = _profile.RightStick.Apply(input.RightX, input.RightY);
        LeftOutX = OutPos(lx); LeftOutY = OutPos(ly);
        RightOutX = OutPos(rx); RightOutY = OutPos(ry);
        LeftOutText = Loc.T("test.output", lx, -ly);
        RightOutText = Loc.T("test.output", rx, -ry);
        R2Value = input.R2;

        foreach (var light in Buttons)
            light.IsPressed = (input.Buttons & light.Button) != 0;

        var (w, h) = _controller.TouchpadSize;
        Touch0Visible = input.Touch0.Active;
        Touch0X = input.Touch0.X * TouchWidth / w - TouchDot / 2;
        Touch0Y = input.Touch0.Y * TouchHeight / h - TouchDot / 2;
        Touch1Visible = input.Touch1.Active;
        Touch1X = input.Touch1.X * TouchWidth / w - TouchDot / 2;
        Touch1Y = input.Touch1.Y * TouchHeight / h - TouchDot / 2;
        TouchText = (input.Touch0.Active, input.Touch1.Active) switch
        {
            (false, false) => Loc.T("test.touchIdle"),
            (true, false) => Loc.T("test.touch1", input.Touch0.X, input.Touch0.Y),
            (false, true) => Loc.T("test.touch1", input.Touch1.X, input.Touch1.Y),
            _ => Loc.T("test.touch2", input.Touch0.X, input.Touch0.Y, input.Touch1.X, input.Touch1.Y),
        };
    }

    private static double StickPos(byte value) => value / 255.0 * (StickArea - StickDot);

    private static double OutPos(double value) => (value + 1) / 2 * (StickArea - StickDot);
}
