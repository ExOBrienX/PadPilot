using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PadPilot.App.Services;
using PadPilot.Core.Devices;
using PadPilot.Core.Services;

namespace PadPilot.App.ViewModels;

/// <summary>Una fila del resumen: control → lo que tiene asignado.</summary>
public sealed record LayoutRow(string Control, string Source);

/// <summary>
/// Pestaña "Configurar" de los mandos genéricos: asignación estándar automática o asistente "1 por 1"
/// que pide pulsar cada botón y detecta cuál fue. La asignación se guarda por modelo.
/// </summary>
public sealed partial class GenericSetupViewModel : ObservableObject
{
    private enum StepKind { Button, Trigger, DPad, StickX, StickY }

    private sealed record Step(string Target, string Control, string Prompt, StepKind Kind);

    private static readonly Step[] Steps =
    [
        new("Cross", "A", Loc.T("setup.p.a"), StepKind.Button),
        new("Circle", "B", Loc.T("setup.p.b"), StepKind.Button),
        new("Square", "X", Loc.T("setup.p.x"), StepKind.Button),
        new("Triangle", "Y", Loc.T("setup.p.y"), StepKind.Button),
        new("L1", "LB", Loc.T("setup.p.lb"), StepKind.Button),
        new("R1", "RB", Loc.T("setup.p.rb"), StepKind.Button),
        new("L2", "LT", Loc.T("setup.p.lt"), StepKind.Trigger),
        new("R2", "RT", Loc.T("setup.p.rt"), StepKind.Trigger),
        new("Share", "Back / Select", Loc.T("setup.p.back"), StepKind.Button),
        new("Options", "Start", Loc.T("setup.p.start"), StepKind.Button),
        new("PS", "Home", Loc.T("setup.p.home"), StepKind.Button),
        new("L3", Loc.T("src.ls"), Loc.T("setup.p.ls"), StepKind.Button),
        new("R3", Loc.T("src.rs"), Loc.T("setup.p.rs"), StepKind.Button),
        new("DPadUp", Loc.T("dpad.up"), Loc.T("setup.p.up"), StepKind.DPad),
        new("DPadDown", Loc.T("dpad.down"), Loc.T("setup.p.down"), StepKind.DPad),
        new("DPadLeft", Loc.T("dpad.left"), Loc.T("setup.p.left"), StepKind.DPad),
        new("DPadRight", Loc.T("dpad.right"), Loc.T("setup.p.right"), StepKind.DPad),
        new(GenericLayout.LeftX, Loc.T("setup.lx"), Loc.T("setup.p.lx"), StepKind.StickX),
        new(GenericLayout.LeftY, Loc.T("setup.ly"), Loc.T("setup.p.ly"), StepKind.StickY),
        new(GenericLayout.RightX, Loc.T("setup.rx"), Loc.T("setup.p.rx"), StepKind.StickX),
        new(GenericLayout.RightY, Loc.T("setup.ry"), Loc.T("setup.p.ry"), StepKind.StickY),
    ];

    private const int AxisThreshold = 70;    // de 255: cuánto tiene que moverse un eje para contar
    private const int ReleaseTolerance = 40; // cuánto puede quedar desviado al soltar

    private readonly GenericHidController _controller;
    private readonly ProfileStore _profiles;
    private readonly Action _layoutChanged;
    private readonly DispatcherTimer _timer;

    private Dictionary<string, string> _draft = new();
    private int _step;
    private bool _waitingRelease;
    private GenericRawState? _baseline;

    public GenericSetupViewModel(GenericHidController controller, ProfileStore profiles, Action layoutChanged)
    {
        _controller = controller;
        _profiles = profiles;
        _layoutChanged = layoutChanged;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Input, (_, _) => Poll());
        Prompt = "";
        Progress = "";
        RefreshSummary();
    }

    public string ModelText =>
        Loc.T("setup.model", _controller.Model, _controller.ButtonCount, string.Join(", ", _controller.AxisUsages.Select(HidUsages.AxisName)))
        + (_controller.HasHat ? Loc.T("setup.hat") : "");

    public ObservableCollection<LayoutRow> Summary { get; } = [];

    [ObservableProperty] public partial string LayoutName { get; set; } = "";

    [ObservableProperty] public partial bool IsRunning { get; set; }

    [ObservableProperty] public partial string Prompt { get; set; }

    [ObservableProperty] public partial string Progress { get; set; }

    public bool IsIdle => !IsRunning;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    private GenericLayout CurrentLayout =>
        _profiles.GetGenericLayout(_controller.ModelKey) ?? _controller.StandardLayout();

    // ---- Acciones ----

    [RelayCommand]
    private void UseStandard()
    {
        Stop();
        _profiles.SetGenericLayout(_controller.ModelKey, null);
        _controller.SetLayout(_controller.StandardLayout());
        RefreshSummary();
        _layoutChanged();
    }

    [RelayCommand]
    private void StartWizard()
    {
        // Se parte de la asignación actual: lo que se salte se queda como estaba.
        _draft = new Dictionary<string, string>(CurrentLayout.Map);
        _step = -1;
        IsRunning = true;
        _timer.Start();
        NextStep();
    }

    [RelayCommand]
    private void Skip()
    {
        if (!IsRunning) return;
        NextStep();
    }

    [RelayCommand]
    private void Cancel()
    {
        Stop();
        Prompt = Loc.T("setup.cancelled");
    }

    // ---- Asistente ----

    private void NextStep()
    {
        _step++;

        // Si la cruceta resultó ser un "hat", las otras direcciones ya están cubiertas.
        while (_step < Steps.Length && Steps[_step].Kind == StepKind.DPad
               && _draft.GetValueOrDefault(GenericLayout.DPad) == GenericLayout.Hat
               && Steps[_step].Target != "DPadUp")
            _step++;

        if (_step >= Steps.Length)
        {
            Finish();
            return;
        }

        var step = Steps[_step];
        Prompt = step.Prompt;
        Progress = Loc.T("setup.step", _step + 1, Steps.Length);
        _baseline = null;
        _waitingRelease = true; // primero, que no haya nada apretado
    }

    private void Poll()
    {
        var now = _controller.RawSnapshot();

        if (_waitingRelease)
        {
            if (_baseline is null)
            {
                if (now.FirstPressedButton() == 0 && now.Hat < 0)
                {
                    _baseline = now;
                    _waitingRelease = false;
                }
                return;
            }
            if (IsReleased(now, _baseline)) _waitingRelease = false;
            return;
        }

        _baseline ??= now;
        var step = Steps[_step];
        var source = Detect(step.Kind, _baseline, now);
        if (source is null) return;

        if (step.Kind == StepKind.DPad && source == GenericLayout.Hat)
        {
            _draft[GenericLayout.DPad] = GenericLayout.Hat;
            foreach (var d in new[] { "DPadUp", "DPadDown", "DPadLeft", "DPadRight" }) _draft.Remove(d);
        }
        else
        {
            if (step.Kind == StepKind.DPad) _draft.Remove(GenericLayout.DPad);
            // Un mismo origen no puede quedar en dos controles a la vez.
            foreach (var key in _draft.Where(kv => kv.Value == source).Select(kv => kv.Key).ToList())
                _draft.Remove(key);
            _draft[step.Target] = source;
        }

        // Esperar a que suelte antes de pasar al siguiente, para no detectar lo mismo dos veces.
        var baseline = _baseline;
        NextStep();
        if (IsRunning)
        {
            _baseline = baseline;
            _waitingRelease = true;
        }
    }

    private static string? Detect(StepKind kind, GenericRawState baseline, GenericRawState now)
    {
        int button = now.FirstPressedButton(baseline);

        int axis = -1, delta = 0;
        for (int i = 0; i < now.AxisValues.Length; i++)
        {
            int d = now.AxisValues[i] - baseline.AxisValues[i];
            if (Math.Abs(d) > Math.Abs(delta)) { delta = d; axis = i; }
        }
        bool axisMoved = axis >= 0 && Math.Abs(delta) >= AxisThreshold;
        string AxisSource() => GenericLayout.Axis(now.AxisUsages[axis], inverted: delta < 0);

        return kind switch
        {
            StepKind.Button => button > 0 ? GenericLayout.Button(button) : null,
            StepKind.Trigger => axisMoved ? AxisSource() : button > 0 ? GenericLayout.Button(button) : null,
            StepKind.DPad => now.Hat >= 0 && baseline.Hat < 0 ? GenericLayout.Hat
                : button > 0 ? GenericLayout.Button(button) : null,
            // Derecha y abajo deben dar valores altos (como en el DualSense); si bajan, el eje va invertido.
            StepKind.StickX or StepKind.StickY => axisMoved ? AxisSource() : null,
            _ => null,
        };
    }

    private static bool IsReleased(GenericRawState now, GenericRawState baseline)
    {
        if (now.FirstPressedButton() != 0 || now.Hat >= 0) return false;
        for (int i = 0; i < now.AxisValues.Length; i++)
            if (Math.Abs(now.AxisValues[i] - baseline.AxisValues[i]) > ReleaseTolerance) return false;
        return true;
    }

    private void Finish()
    {
        var layout = new GenericLayout { IsCustom = true, Map = _draft };
        _profiles.SetGenericLayout(_controller.ModelKey, layout);
        _controller.SetLayout(layout);
        Stop();
        Prompt = Loc.T("setup.done");
        RefreshSummary();
        _layoutChanged();
    }

    private void Stop()
    {
        _timer.Stop();
        IsRunning = false;
        Progress = "";
    }

    /// <summary>Al cambiar de pestaña o cerrar la ventana el asistente se cancela.</summary>
    public void StopIfRunning()
    {
        if (IsRunning) Cancel();
    }

    private void RefreshSummary()
    {
        var layout = CurrentLayout;
        LayoutName = layout.IsCustom ? Loc.T("setup.custom") : Loc.T("setup.standard");

        Summary.Clear();
        foreach (var step in Steps)
        {
            if (step.Kind == StepKind.DPad && layout.Map.GetValueOrDefault(GenericLayout.DPad) == GenericLayout.Hat)
            {
                if (step.Target == "DPadUp") Summary.Add(new LayoutRow(Loc.T("setup.dpad"), Loc.T("setup.hatName")));
                continue;
            }
            Summary.Add(new LayoutRow(step.Control, Describe(layout.Map.GetValueOrDefault(step.Target))));
        }
    }

    private static string Describe(string? source)
    {
        if (source is null) return "—";
        if (source == GenericLayout.Hat) return Loc.T("setup.hatName");
        var parts = source.Split(':');
        if (parts[0] == "button") return Loc.T("setup.button", parts[1]);
        if (parts[0] == "axis" && uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out uint usage))
            return Loc.T("setup.axis", HidUsages.AxisName(usage)) + (parts.Length > 2 ? Loc.T("setup.inverted") : "");
        return source;
    }
}
