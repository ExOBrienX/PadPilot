using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PadPilot.App.Services;
using PadPilot.Core.Devices;
using PadPilot.Core.Services;

namespace PadPilot.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ControllerManager _manager;
    private readonly InputRouter _router;
    private readonly HidHideService _hidHide;
    private bool _windowOpen;
    private bool _syncing;

    public MainViewModel(ControllerManager manager, InputRouter router, HidHideService hidHide)
    {
        _manager = manager;
        _router = router;
        _hidHide = hidHide;
        HidHideStatus = "";
        MacroEditor = new MacroEditorViewModel(manager.Profiles);
        BatteryLights = new BatteryLightsViewModel(manager.Profiles);
        MacroEditor.CatalogChanged += () =>
        {
            foreach (var vm in Controllers) vm.ResyncMappings();
        };
        var settings = manager.Profiles.Settings;
        _syncing = true;
        HideControllers = manager.Profiles.HideControllers;
        LanguageIndex = settings.Language switch { "es" => 1, "en" => 2, _ => 0 };
        ThemeIndex = settings.Theme switch { "system" => 0, "light" => 1, _ => 2 };
        LayoutIndex = settings.Layout == "horizontal" ? 1 : 0;
        StartWithWindows = Autostart.IsEnabled;
        _syncing = false;
        ApplyTheme(settings.Theme);
        CheckRequirements();
        TrayText = Loc.T("tray.none");
        EmulationStatus = "";

        // Los eventos del manager y del router llegan desde hilos de fondo: se pasan al hilo de UI.
        manager.ControllerConnected += c => Dispatcher.UIThread.Post(() => Add(c));
        manager.ControllerDisconnected += c => Dispatcher.UIThread.Post(() => Remove(c));
        manager.SlotsChanged += () => Dispatcher.UIThread.Post(Resort);
        router.StatusChanged += () => Dispatcher.UIThread.Post(RefreshEmulation);

        Controllers.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasControllers));
            UpdateTrayText();
        };

        foreach (var controller in manager.Controllers)
            Add(controller);
        RefreshEmulation();
    }

    public ObservableCollection<ControllerViewModel> Controllers { get; } = [];

    public bool IsEmpty => Controllers.Count == 0;

    public bool HasControllers => Controllers.Count > 0;

    [ObservableProperty] public partial ControllerViewModel? SelectedController { get; set; }

    [ObservableProperty] public partial string TrayText { get; set; }

    // ---- Emulación Xbox ----

    [ObservableProperty] public partial bool EmulateXbox { get; set; }

    [ObservableProperty] public partial string EmulationStatus { get; set; }

    public MacroEditorViewModel MacroEditor { get; }

    // ---- Páginas: 0 = mandos, 1 = ajustes, 2 = acerca de ----

    [ObservableProperty] public partial int PageIndex { get; set; }

    public bool IsControllersPage => PageIndex == 0;
    public bool IsSettingsPage => PageIndex == 1;
    public bool IsAboutPage => PageIndex == 2;

    partial void OnPageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsControllersPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsAboutPage));
        if (value == 1) CheckRequirements();
        UpdateLive();
    }

    [RelayCommand]
    private void Navigate(string page) => PageIndex = int.Parse(page);

    // ---- Ajustes generales ----

    public IReadOnlyList<string> LanguageOptions { get; } = [Loc.T("settings.langAuto"), "Español", "English"];
    public IReadOnlyList<string> ThemeOptions { get; } = [Loc.T("theme.system"), Loc.T("theme.light"), Loc.T("theme.dark")];
    public IReadOnlyList<string> LayoutOptions { get; } = [Loc.T("layout.vertical"), Loc.T("layout.horizontal")];

    [ObservableProperty] public partial int LanguageIndex { get; set; }
    [ObservableProperty] public partial bool LanguageRestartNeeded { get; set; }
    [ObservableProperty] public partial int ThemeIndex { get; set; }
    [ObservableProperty] public partial int LayoutIndex { get; set; }
    [ObservableProperty] public partial bool StartWithWindows { get; set; }

    public bool AutostartSupported => Autostart.IsSupported;

    partial void OnLanguageIndexChanged(int value)
    {
        if (_syncing) return;
        string setting = value switch { 1 => "es", 2 => "en", _ => "" };
        _manager.Profiles.Settings.Language = setting;
        _manager.Profiles.SaveDeferred();

        var current = Loc.Language;
        Loc.Init(setting);           // averiguar qué idioma resultaría…
        LanguageRestartNeeded = Loc.Language != current;
        Loc.Init(current);           // …pero la interfaz sigue en el actual hasta reiniciar
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (_syncing) return;
        string theme = value switch { 0 => "system", 1 => "light", _ => "dark" };
        _manager.Profiles.Settings.Theme = theme;
        _manager.Profiles.SaveDeferred();
        ApplyTheme(theme);
    }

    public static void ApplyTheme(string theme)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = theme switch { "light" => ThemeVariant.Light, "system" => ThemeVariant.Default, _ => ThemeVariant.Dark };
    }

    public bool IsHorizontal => LayoutIndex == 1;
    public Dock ChipsDock => IsHorizontal ? Dock.Left : Dock.Top;
    public Orientation ChipsOrientation => IsHorizontal ? Orientation.Vertical : Orientation.Horizontal;

    partial void OnLayoutIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsHorizontal));
        OnPropertyChanged(nameof(ChipsDock));
        OnPropertyChanged(nameof(ChipsOrientation));
        if (_syncing) return;
        _manager.Profiles.Settings.Layout = value == 1 ? "horizontal" : "vertical";
        _manager.Profiles.SaveDeferred();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_syncing) return;
        Autostart.Set(value);
    }

    /// <summary>Lo llama la bandeja al cambiar el inicio automático desde su menú.</summary>
    public void RefreshAutostart()
    {
        _syncing = true;
        StartWithWindows = Autostart.IsEnabled;
        _syncing = false;
    }

    [RelayCommand]
    private void RestartApp()
    {
        if (Environment.ProcessPath is not { } exe) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--restart") { UseShellExecute = true });
            RestartRequested?.Invoke();
        }
        catch (Exception) { }
    }

    // ---- Requisitos ----

    [ObservableProperty] public partial bool VigemInstalled { get; set; }
    [ObservableProperty] public partial bool HidHideInstalled { get; set; }

    public string VigemStatus => Loc.T(VigemInstalled ? "req.installed" : "req.missing");
    public string HidHideInstallStatus => Loc.T(HidHideInstalled ? "req.installed" : "req.missing");

    partial void OnVigemInstalledChanged(bool value) => OnPropertyChanged(nameof(VigemStatus));
    partial void OnHidHideInstalledChanged(bool value) => OnPropertyChanged(nameof(HidHideInstallStatus));

    [RelayCommand]
    private void CheckRequirements()
    {
        VigemInstalled = AppInfo.IsDriverInstalled("ViGEmBus");
        HidHideInstalled = AppInfo.IsDriverInstalled("HidHide");
    }

    [RelayCommand]
    private void OpenVigemDownload() => AppInfo.OpenUrl(AppInfo.ViGEmBusUrl);

    // ---- Acerca de ----

    public string AppName => AppInfo.Name;
    public string VersionText => Loc.T("about.version", AppInfo.Version);
    public string AuthorText => Loc.T("about.author", AppInfo.Author);
    public string CopyrightText => $"© 2026 {AppInfo.Author} · GPL-3.0";
    public bool HasDonate => AppInfo.DonateUrl.Length > 0;
    public bool HasIssues => AppInfo.IssuesUrl.Length > 0;
    public bool HasWebsite => AppInfo.WebsiteUrl.Length > 0;
    public bool HasNoLinks => !HasDonate && !HasIssues && !HasWebsite;
    public string ThirdPartyText => string.Join(Environment.NewLine, AppInfo.ThirdParty.Select(t => $"{t.Name} — {t.License}"));

    [RelayCommand] private void Donate() => AppInfo.OpenUrl(AppInfo.DonateUrl);
    [RelayCommand] private void ReportIssue() => AppInfo.OpenUrl(AppInfo.IssuesUrl);
    [RelayCommand] private void OpenWebsite() => AppInfo.OpenUrl(AppInfo.WebsiteUrl);

    public BatteryLightsViewModel BatteryLights { get; }

    // ---- HidHide: ocultar los mandos originales ----

    [ObservableProperty] public partial bool HideControllers { get; set; }

    [ObservableProperty] public partial string HidHideStatus { get; set; }

    [ObservableProperty] public partial bool HidHideNeedsInstall { get; set; }

    [ObservableProperty] public partial bool HidHideNeedsAdmin { get; set; }

    partial void OnHideControllersChanged(bool value)
    {
        if (_syncing) return;
        _manager.Profiles.HideControllers = value;
        ApplyHidHide();
    }

    /// <summary>Oculta los mandos si la emulación y la opción están activas; si no, los vuelve a mostrar.</summary>
    private void ApplyHidHide()
    {
        HidHideNeedsInstall = HidHideNeedsAdmin = false;

        if (!HideControllers)
        {
            _hidHide.RestoreAll();
            HidHideStatus = Loc.T("hide.off");
            return;
        }

        switch (_hidHide.Check())
        {
            case HidHideService.State.NotInstalled:
                HidHideNeedsInstall = true;
                HidHideStatus = Loc.T("hide.notInstalled");
                return;
            case HidHideService.State.NeedsAdmin:
                HidHideNeedsAdmin = true;
                HidHideStatus = Loc.T("hide.needsAdmin");
                return;
        }

        if (!_router.IsEmulating)
        {
            _hidHide.RestoreAll();
            HidHideStatus = Loc.T("hide.waiting");
            return;
        }

        HidHideStatus = _hidHide.Apply(_manager.Controllers)
            ? Loc.T("hide.on")
            : Loc.T("hide.error", _hidHide.LastError);
    }

    [RelayCommand]
    private void OpenHidHideDownload() => AppInfo.OpenUrl(AppInfo.HidHideUrl);

    /// <summary>Se reinicia PadPilot con permisos de administrador (Windows pide confirmación).</summary>
    [RelayCommand]
    private void RestartAsAdmin()
    {
        if (Environment.ProcessPath is not { } exe) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--restart")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            RestartRequested?.Invoke();
        }
        catch (Exception) { /* el usuario dijo que no */ }
    }

    /// <summary>La app debe cerrarse para que arranque la copia con permisos.</summary>
    public event Action? RestartRequested;

    partial void OnEmulateXboxChanged(bool value)
    {
        if (_syncing) return;
        _router.SetEmulation(value);
        RefreshEmulation();
    }

    private void RefreshEmulation()
    {
        _syncing = true;
        EmulateXbox = _router.IsEmulating;
        _syncing = false;

        EmulationStatus = _router.EmulationError
            ?? (_router.IsEmulating
                ? Loc.T("emu.on")
                : Loc.T("emu.off"));

        foreach (var vm in Controllers) vm.RefreshXboxStatus();
        ApplyHidHide();
    }

    // ---- Ventana y probador ----

    partial void OnSelectedControllerChanged(ControllerViewModel? value) => UpdateLive();

    /// <summary>La ventana avisa al abrirse y cerrarse: el probador solo lee el mando con la ventana abierta.</summary>
    public void SetWindowOpen(bool open)
    {
        _windowOpen = open;
        if (!open)
            foreach (var vm in Controllers) vm.Setup?.StopIfRunning();
        UpdateLive();
    }

    private void UpdateLive()
    {
        foreach (var vm in Controllers)
        {
            bool visible = _windowOpen && PageIndex == 0 && vm == SelectedController;
            vm.Tester.IsLive = visible && vm.SelectedTab == ControllerViewModel.TabTester;
            vm.Audio?.SetLive(visible && vm.SelectedTab == ControllerViewModel.TabAudio);
        }
    }

    // ---- Lista de mandos ----

    private void Add(GamepadController controller)
    {
        if (Controllers.Any(vm => ReferenceEquals(vm.Controller, controller))) return;

        var vm = new ControllerViewModel(_manager, _router, MacroEditor, controller);
        vm.SummaryChanged += UpdateTrayText;
        vm.TabChanged += UpdateLive;
        Controllers.Add(vm);
        Resort();
        SelectedController ??= vm;
        if (HideControllers) ApplyHidHide();
    }

    private void Remove(GamepadController controller)
    {
        var vm = Controllers.FirstOrDefault(v => ReferenceEquals(v.Controller, controller));
        if (vm is null) return;
        vm.SummaryChanged -= UpdateTrayText;
        vm.TabChanged -= UpdateLive;
        vm.Detach();
        Controllers.Remove(vm);
        if (SelectedController == vm) SelectedController = Controllers.FirstOrDefault();
        if (HideControllers) ApplyHidHide();
    }

    /// <summary>Ordena los mandos por slot (Jugador 1 primero).</summary>
    private void Resort()
    {
        var selected = SelectedController;
        foreach (var vm in Controllers)
            vm.RefreshSlot();

        var ordered = Controllers.OrderBy(v => v.Controller.Slot == 0 ? int.MaxValue : v.Controller.Slot).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            int current = Controllers.IndexOf(ordered[i]);
            if (current != i) Controllers.Move(current, i);
        }

        // Mover elementos puede hacer que la lista pierda la selección.
        if (selected is not null && Controllers.Contains(selected)) SelectedController = selected;
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        TrayText = Controllers.Count == 0
            ? Loc.T("tray.none")
            : string.Join("\n", Controllers.Select(v => $"{Loc.T("slot.short", v.Controller.Slot)} {v.Title}: {v.BatteryText}"));
    }
}
