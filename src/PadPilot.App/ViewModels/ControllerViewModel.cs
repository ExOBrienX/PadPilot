using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PadPilot.App.Services;
using PadPilot.Core.Devices;
using PadPilot.Core.Models;
using PadPilot.Core.Protocol;
using PadPilot.Core.Services;

namespace PadPilot.App.ViewModels;

/// <summary>Un botón físico en la pestaña de remapeo, con la acción elegida.</summary>
public sealed partial class ButtonMapItem : ObservableObject
{
    private readonly Action<ButtonMapItem> _changed;
    private bool _syncing;

    public ButtonMapItem(PadButtons source, string name, ButtonAction current, Action<ButtonMapItem> changed)
    {
        Source = source;
        Name = name;
        var xbox = ButtonActions.DefaultXbox(source);
        DefaultHint = xbox == XboxButton.None ? Loc.T("btn.defaultNone") : Loc.T("btn.default", "Xbox " + ButtonActions.XboxName(xbox));
        _changed = changed;
        _syncing = true;
        SelectedAction = current;
        _syncing = false;
    }

    public PadButtons Source { get; }

    public string Name { get; }

    public string DefaultHint { get; }

    public IReadOnlyList<ButtonAction> Actions => ButtonActions.Catalog;

    [ObservableProperty] public partial ButtonAction SelectedAction { get; set; }

    partial void OnSelectedActionChanged(ButtonAction value)
    {
        if (!_syncing && value is not null) _changed(this);
    }

    public void Reset() => Resync(ButtonActions.Default);

    /// <summary>Vuelve a seleccionar la acción (tras cambiar el catálogo de macros) sin guardar nada.</summary>
    public void Resync(ButtonAction action)
    {
        _syncing = true;
        SelectedAction = action;
        _syncing = false;
    }
}

/// <summary>Un mando: batería, slot, luz, gatillos, remapeo y probador. Cada cambio se aplica y se guarda al instante.</summary>
public sealed partial class ControllerViewModel : ObservableObject
{
    public const int TabGeneral = 0, TabTriggers = 1, TabButtons = 2, TabTester = 3, TabSetup = 4, TabAudio = 5, TabMacros = 6;

    private static readonly IBrush OkBrush = new ImmutableSolidColorBrush(Color.Parse("#4ADE80"));
    private static readonly IBrush LowBrush = new ImmutableSolidColorBrush(Color.Parse("#F87171"));
    private static readonly IBrush ChargingBrush = new ImmutableSolidColorBrush(Color.Parse("#60A5FA"));
    private static readonly IBrush UnknownBrush = new ImmutableSolidColorBrush(Color.Parse("#6B7280"));

    private static readonly string[] SlotNameList = [.. Enumerable.Range(1, PlayerSlots.Count).Select(n => Loc.T("slot.n", n))];
    private static readonly string[] TriggerModeList =
        [Loc.T("trig.mode.off"), Loc.T("trig.mode.feedback"), Loc.T("trig.mode.weapon"), Loc.T("trig.mode.vibration")];

    private readonly ControllerManager _manager;
    private readonly InputRouter _router;
    private readonly ControllerProfile _profile;
    private bool _syncing;

    public ControllerViewModel(ControllerManager manager, InputRouter router, MacroEditorViewModel macros, GamepadController controller)
    {
        _manager = manager;
        _router = router;
        Macros = macros;
        Controller = controller;
        _profile = manager.Profiles.GetOrCreate(controller);
        Tester = new InputTesterViewModel(controller, _profile, () =>
        {
            _router.RefreshMap(controller);
            _manager.Profiles.SaveDeferred();
        });

        _syncing = true;
        BatteryText = Loc.T("bat.reading");
        BatteryBrush = UnknownBrush;
        LightbarBrush = UnknownBrush;
        XboxStatus = "";
        SlotIndex = controller.Slot - 1;
        UseSlotColor = _profile.UseSlotColor;
        LightbarIntensity = _profile.LightbarIntensity;
        LoadTriggers();
        _syncing = false;

        if (controller is GenericHidController generic)
        {
            Setup = new GenericSetupViewModel(generic, manager.Profiles, () => SummaryChanged?.Invoke());
            SelectedTab = TabSetup;
        }

        ButtonMappings = ButtonActions.Remappable
            .Where(b => b != PadButtons.Mute || controller.Kind is ControllerKind.DualSense or ControllerKind.DualSenseEdge)
            .Where(b => b != PadButtons.Touchpad || controller.SupportsTouchpad)
            .Select(b =>
            {
                _profile.ButtonMap.TryGetValue(b.ToString(), out var id);
                return new ButtonMapItem(b, SourceName(b), ButtonActions.Find(id), OnMappingChanged);
            })
            .ToList();

        if (controller.Kind != ControllerKind.Generic)
            Audio = new AudioViewModel(controller, _profile, () => _manager.Profiles.SaveDeferred());

        RefreshColor();
        UpdateBattery();
        RefreshXboxStatus();
        controller.BatteryChanged += OnBatteryChanged;
        controller.ButtonsPressed += OnButtonsPressed;
        manager.SlotColorsChanged += RefreshColor;
    }

    public GamepadController Controller { get; }

    public InputTesterViewModel Tester { get; }

    /// <summary>Asistente de asignación (solo mandos genéricos).</summary>
    public GenericSetupViewModel? Setup { get; }

    public bool IsGeneric => Setup is not null;

    /// <summary>Micrófono y audífonos del mando (DualSense y DS4).</summary>
    public AudioViewModel? Audio { get; }

    public bool HasAudio => Audio is not null;

    /// <summary>Editor de macros, compartido por todos los mandos.</summary>
    public MacroEditorViewModel Macros { get; }

    public bool HasLightbar => Controller.SupportsLightbar;

    /// <summary>Se dispara cuando cambia algo que se ve en la lista de mandos o en la bandeja.</summary>
    public event Action? SummaryChanged;

    /// <summary>Se dispara al cambiar de pestaña (para encender o apagar el probador).</summary>
    public event Action? TabChanged;

    public string Title => Controller.Kind == ControllerKind.Generic ? Loc.T("generic.name") : Controller.DisplayName;

    public string Subtitle =>
        $"{(Controller.Connection == ConnectionKind.Usb ? "USB" : "Bluetooth")} · "
        + (Controller is GenericHidController g ? $"{g.Model} · " : "")
        + "ID …" + (Controller.Id.Length > 4 ? Controller.Id[^4..] : Controller.Id);

    public string ChipTitle => Controller.Slot > 0 ? $"{Loc.T("slot.short", Controller.Slot)} · {Title}" : Title;

    public bool SupportsTriggers => Controller.SupportsAdaptiveTriggers;

    public IReadOnlyList<string> SlotNames => SlotNameList;

    public IReadOnlyList<string> TriggerModes => TriggerModeList;

    public IReadOnlyList<ColorPreset> Presets => ColorPreset.All;

    public IReadOnlyList<TriggerPreset> TriggerPresetList => TriggerPresets.All;

    [ObservableProperty] public partial int SelectedTab { get; set; }

    partial void OnSelectedTabChanged(int value)
    {
        if (value != TabSetup) Setup?.StopIfRunning();
        TabChanged?.Invoke();
    }

    // ---- Batería ----

    [ObservableProperty] public partial int BatteryPercent { get; set; }

    [ObservableProperty] public partial string BatteryText { get; set; }

    [ObservableProperty] public partial IBrush BatteryBrush { get; set; }

    private void OnBatteryChanged(GamepadController _) => Dispatcher.UIThread.Post(UpdateBattery);

    private void UpdateBattery()
    {
        var battery = Controller.Battery;
        BatteryPercent = battery.Percent;
        BatteryText = battery.Status switch
        {
            _ when !Controller.ReportsBattery => Loc.T("bat.none"),
            ChargeStatus.Unknown => Loc.T("bat.reading"),
            ChargeStatus.Error => Loc.T("bat.error"),
            ChargeStatus.Full => Loc.T("bat.full"),
            ChargeStatus.Charging => Loc.T("bat.charging", battery.Percent),
            _ => Loc.T("bat.pct", battery.Percent),
        };
        BatteryBrush = battery.Status switch
        {
            ChargeStatus.Charging or ChargeStatus.Full => ChargingBrush,
            ChargeStatus.Discharging when battery.Percent <= 20 => LowBrush,
            ChargeStatus.Discharging => OkBrush,
            _ => UnknownBrush,
        };
        SummaryChanged?.Invoke();
    }

    // ---- Slot de jugador ----

    [ObservableProperty] public partial int SlotIndex { get; set; }

    partial void OnSlotIndexChanged(int value)
    {
        if (_syncing || value < 0) return;
        _manager.AssignSlot(Controller, value + 1);
    }

    /// <summary>Lo llama la ventana principal cuando el manager reordena slots (p. ej. tras un intercambio).</summary>
    public void RefreshSlot()
    {
        Sync(() => SlotIndex = Controller.Slot - 1);
        OnPropertyChanged(nameof(ChipTitle));
        RefreshColor();
    }

    // ---- Barra de luz ----

    /// <summary>true: usa el color del slot (compartido). false: color propio de este mando.</summary>
    [ObservableProperty] public partial bool UseSlotColor { get; set; }

    [ObservableProperty] public partial ColorPreset? SelectedPreset { get; set; }

    /// <summary>El color actual en el selector "Personalizado".</summary>
    [ObservableProperty] public partial Color CustomColor { get; set; }

    [ObservableProperty] public partial IBrush LightbarBrush { get; set; }

    [ObservableProperty] public partial double LightbarIntensity { get; set; }

    public bool UseOwnColor
    {
        get => !UseSlotColor;
        set => UseSlotColor = !value;
    }

    public string ColorTargetText => UseSlotColor
        ? Controller.Slot > 0
            ? Loc.T("light.slotHint", Controller.Slot)
            : Loc.T("light.noSlot")
        : Loc.T("light.ownHint");

    partial void OnUseSlotColorChanged(bool value)
    {
        OnPropertyChanged(nameof(UseOwnColor));
        OnPropertyChanged(nameof(ColorTargetText));
        if (_syncing) return;
        _profile.UseSlotColor = value;
        Commit();
        RefreshColor();
    }

    partial void OnSelectedPresetChanged(ColorPreset? value)
    {
        if (_syncing || value is null) return;
        SetColor(value.Color);
    }

    partial void OnCustomColorChanged(Color value)
    {
        if (_syncing) return;
        SetColor(new Rgb(value.R, value.G, value.B));
    }

    partial void OnLightbarIntensityChanged(double value)
    {
        if (_syncing) return;
        _profile.LightbarIntensity = (int)Math.Round(value);
        Commit();
    }

    private void SetColor(Rgb color)
    {
        if (UseSlotColor)
        {
            // Cambia el color del slot: lo aplica a todos los mandos de ese slot y avisa a las demás tarjetas.
            _manager.SetSlotColor(Controller.Slot, color);
        }
        else
        {
            _profile.Color = color;
            Commit();
            RefreshColor();
        }
    }

    private void RefreshColor()
    {
        var color = _manager.EffectiveColor(Controller);
        Sync(() =>
        {
            SelectedPreset = ColorPreset.Find(color);
            CustomColor = Color.FromRgb(color.R, color.G, color.B);
        });
        LightbarBrush = new ImmutableSolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        OnPropertyChanged(nameof(ColorTargetText));
    }

    // ---- Gatillos adaptativos (DualSense) ----

    [ObservableProperty] public partial TriggerPreset? SelectedTriggerPreset { get; set; }

    [ObservableProperty] public partial int LeftTriggerMode { get; set; }
    [ObservableProperty] public partial double LeftTriggerStrength { get; set; }
    [ObservableProperty] public partial double LeftTriggerStart { get; set; }
    [ObservableProperty] public partial double LeftTriggerFrequency { get; set; }
    [ObservableProperty] public partial int RightTriggerMode { get; set; }
    [ObservableProperty] public partial double RightTriggerStrength { get; set; }
    [ObservableProperty] public partial double RightTriggerStart { get; set; }
    [ObservableProperty] public partial double RightTriggerFrequency { get; set; }

    public bool LeftIsVibration => LeftTriggerMode == (int)TriggerMode.Vibration;
    public bool RightIsVibration => RightTriggerMode == (int)TriggerMode.Vibration;

    partial void OnLeftTriggerModeChanged(int value)
    {
        OnPropertyChanged(nameof(LeftIsVibration));
        UpdateTriggers();
    }

    partial void OnRightTriggerModeChanged(int value)
    {
        OnPropertyChanged(nameof(RightIsVibration));
        UpdateTriggers();
    }

    partial void OnLeftTriggerStrengthChanged(double value) => UpdateTriggers();
    partial void OnLeftTriggerStartChanged(double value) => UpdateTriggers();
    partial void OnLeftTriggerFrequencyChanged(double value) => UpdateTriggers();
    partial void OnRightTriggerStrengthChanged(double value) => UpdateTriggers();
    partial void OnRightTriggerStartChanged(double value) => UpdateTriggers();
    partial void OnRightTriggerFrequencyChanged(double value) => UpdateTriggers();

    partial void OnSelectedTriggerPresetChanged(TriggerPreset? value)
    {
        if (_syncing || value is null) return;
        _profile.LeftTrigger = value.Left;
        _profile.RightTrigger = value.Right;
        Sync(LoadTriggers);
        Commit();
    }

    private void UpdateTriggers()
    {
        if (_syncing) return;
        _profile.LeftTrigger = BuildEffect(LeftTriggerMode, LeftTriggerStrength, LeftTriggerStart, LeftTriggerFrequency, _profile.LeftTrigger);
        _profile.RightTrigger = BuildEffect(RightTriggerMode, RightTriggerStrength, RightTriggerStart, RightTriggerFrequency, _profile.RightTrigger);
        Sync(() => SelectedTriggerPreset = TriggerPresets.Find(_profile.LeftTrigger, _profile.RightTrigger));
        Commit();
    }

    /// <summary>Lleva los efectos del perfil a los controles (sin volver a escribirlos).</summary>
    private void LoadTriggers()
    {
        LoadTrigger(_profile.LeftTrigger, out int lm, out double ls, out double lp, out double lf);
        LeftTriggerMode = lm; LeftTriggerStrength = ls; LeftTriggerStart = lp; LeftTriggerFrequency = lf;
        LoadTrigger(_profile.RightTrigger, out int rm, out double rs, out double rp, out double rf);
        RightTriggerMode = rm; RightTriggerStrength = rs; RightTriggerStart = rp; RightTriggerFrequency = rf;
        SelectedTriggerPreset = TriggerPresets.Find(_profile.LeftTrigger, _profile.RightTrigger);
    }

    private static TriggerEffect BuildEffect(int mode, double strength, double start, double frequency, TriggerEffect previous)
    {
        byte s = (byte)Math.Clamp(Math.Round(strength), 1, 8);
        byte p = (byte)Math.Clamp(Math.Round(start), 0, 9);

        switch ((TriggerMode)mode)
        {
            case TriggerMode.Feedback:
                return new TriggerEffect(TriggerMode.Feedback, Position: p, Strength: s);

            case TriggerMode.Weapon:
            {
                // Se conserva el largo del "clic" del efecto anterior (p. ej. el de un preset).
                int gap = previous.Mode == TriggerMode.Weapon ? Math.Max(previous.End - previous.Position, 1) : 2;
                int begin = Math.Clamp((int)p, 2, 7);
                return new TriggerEffect(TriggerMode.Weapon, Position: (byte)begin,
                    End: (byte)Math.Clamp(begin + gap, begin + 1, 8), Strength: s);
            }

            case TriggerMode.Vibration:
                return new TriggerEffect(TriggerMode.Vibration, Position: p, Strength: s,
                    Frequency: (byte)Math.Clamp(Math.Round(frequency), 1, 255));

            default:
                return TriggerEffect.Off;
        }
    }

    private static void LoadTrigger(TriggerEffect effect, out int mode, out double strength, out double start, out double frequency)
    {
        mode = (int)effect.Mode;
        strength = effect.Strength == 0 ? 4 : effect.Strength;
        start = effect.Position;
        frequency = effect.Frequency == 0 ? 30 : effect.Frequency;
    }

    // ---- Remapeo de botones ----

    public List<ButtonMapItem> ButtonMappings { get; }

    [ObservableProperty] public partial string XboxStatus { get; set; }

    private void OnMappingChanged(ButtonMapItem item)
    {
        if (item.SelectedAction.Kind == ActionKind.Default)
            _profile.ButtonMap.Remove(item.Source.ToString());
        else
            _profile.ButtonMap[item.Source.ToString()] = item.SelectedAction.Id;

        _router.RefreshMap(Controller);
        _manager.Profiles.SaveDeferred();
    }

    /// <summary>Tras crear/renombrar/borrar macros: volver a seleccionar en cada combo lo que dice el perfil.</summary>
    public void ResyncMappings()
    {
        foreach (var item in ButtonMappings)
        {
            _profile.ButtonMap.TryGetValue(item.Source.ToString(), out var id);
            var action = ButtonActions.Find(id);
            if (action.Kind == ActionKind.Default) _profile.ButtonMap.Remove(item.Source.ToString()); // macro borrada
            item.Resync(action);
        }
        _router.RefreshMap(Controller);
    }

    private void OnButtonsPressed(GamepadController _, PadButtons pressed)
    {
        if ((pressed & PadButtons.Mute) != 0 && Audio is { } audio)
            Dispatcher.UIThread.Post(audio.OnMutePressed);
    }

    [RelayCommand]
    private void ResetMappings()
    {
        foreach (var item in ButtonMappings) item.Reset();
        _profile.ButtonMap.Clear();
        _router.RefreshMap(Controller);
        _manager.Profiles.SaveDeferred();
    }

    public void RefreshXboxStatus()
    {
        int player = _router.XboxPlayer(Controller);
        XboxStatus = !_router.IsEmulating
            ? Loc.T("btn.xboxOff")
            : player > 0
                ? Loc.T("btn.xboxOn", player)
                : Loc.T("btn.xboxOnNoPlayer");
    }

    private string SourceName(PadButtons button) => Controller.Kind == ControllerKind.Generic
        ? GenericName(button)
        : button switch
    {
        PadButtons.Cross => Loc.T("src.cross"),
        PadButtons.Circle => Loc.T("src.circle"),
        PadButtons.Square => Loc.T("src.square"),
        PadButtons.Triangle => Loc.T("src.triangle"),
        PadButtons.Share => Controller.Kind == ControllerKind.DualShock4 ? "Share" : "Create",
        PadButtons.Touchpad => Loc.T("src.touchpad"),
        PadButtons.Mute => Loc.T("src.mute"),
        PadButtons.DPadUp => Loc.T("dpad.up"),
        PadButtons.DPadDown => Loc.T("dpad.down"),
        PadButtons.DPadLeft => Loc.T("dpad.left"),
        PadButtons.DPadRight => Loc.T("dpad.right"),
        _ => button.ToString(),
    };

    /// <summary>Los genéricos traen iconos de Xbox: se nombran así.</summary>
    public static string GenericName(PadButtons button) => button switch
    {
        PadButtons.Cross => "A",
        PadButtons.Circle => "B",
        PadButtons.Square => "X",
        PadButtons.Triangle => "Y",
        PadButtons.L1 => "LB",
        PadButtons.R1 => "RB",
        PadButtons.L2 => "LT",
        PadButtons.R2 => "RT",
        PadButtons.L3 => Loc.T("src.ls"),
        PadButtons.R3 => Loc.T("src.rs"),
        PadButtons.Share => "Back / Select",
        PadButtons.Options => "Start",
        PadButtons.PS => "Home",
        PadButtons.DPadUp => Loc.T("dpad.up"),
        PadButtons.DPadDown => Loc.T("dpad.down"),
        PadButtons.DPadLeft => Loc.T("dpad.left"),
        PadButtons.DPadRight => Loc.T("dpad.right"),
        _ => button.ToString(),
    };

    // ---- Acciones ----

    [RelayCommand]
    private async Task TestRumbleAsync()
    {
        Controller.Output.RumbleStrong = 180;
        Controller.Output.RumbleWeak = 180;
        Controller.ApplyOutput();

        await Task.Delay(350);

        Controller.Output.RumbleStrong = 0;
        Controller.Output.RumbleWeak = 0;
        Controller.ApplyOutput();
    }

    /// <summary>Suelta los eventos al desconectarse el mando.</summary>
    public void Detach()
    {
        Tester.IsLive = false;
        Setup?.StopIfRunning();
        Audio?.Dispose();
        Controller.BatteryChanged -= OnBatteryChanged;
        Controller.ButtonsPressed -= OnButtonsPressed;
        _manager.SlotColorsChanged -= RefreshColor;
    }

    private void Commit()
    {
        _manager.ApplyProfile(Controller);
        _manager.Profiles.SaveDeferred();
        SummaryChanged?.Invoke();
    }

    private void Sync(Action change)
    {
        bool was = _syncing;
        _syncing = true;
        try { change(); }
        finally { _syncing = was; }
    }
}
