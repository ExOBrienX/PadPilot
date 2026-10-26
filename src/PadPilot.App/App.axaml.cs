using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using PadPilot.App.Services;
using PadPilot.App.ViewModels;
using PadPilot.App.Views;
using PadPilot.Core.Protocol;
using PadPilot.Core.Services;

namespace PadPilot.App;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private TrayIcon? _tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var profiles = new ProfileStore(ProfileStore.DefaultPath);
            // El idioma se fija antes de crear cualquier texto de la interfaz.
            Loc.Init(profiles.Settings.Language);
            TriggerPresets.Localizer = Loc.T;
            Autostart.RefreshPath();

            var manager = new ControllerManager(profiles);
            var router = new InputRouter(manager);
            var hidHide = new HidHideService();
            _viewModel = new MainViewModel(manager, router, hidHide);
            _viewModel.RestartRequested += () => desktop.Shutdown();
            manager.Start();
            var lighting = new LightingService(manager);

            CreateTray(desktop);

            // Con --tray (así arranca con Windows) parte oculta en la bandeja.
            bool startHidden = desktop.Args?.Contains("--tray") == true;
            if (!startHidden) ShowMainWindow();

            desktop.Exit += (_, _) =>
            {
                _tray?.Dispose();
                hidHide.RestoreAll(); // nunca dejar mandos ocultos con PadPilot cerrado
                lighting.Dispose();
                router.Dispose();
                manager.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem(Loc.T("tray.open"));
        open.Click += (_, _) => ShowMainWindow();

        var autostart = new NativeMenuItem(Loc.T("settings.autostart"))
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = Autostart.IsEnabled,
            IsEnabled = Autostart.IsSupported,
        };
        autostart.Click += (_, _) =>
        {
            Autostart.Set(!Autostart.IsEnabled);
            autostart.IsChecked = Autostart.IsEnabled;
            _viewModel?.RefreshAutostart();
        };

        var exit = new NativeMenuItem(Loc.T("tray.exit"));
        exit.Click += (_, _) => desktop.Shutdown();

        var vm = _viewModel!;
        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(autostart);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://PadPilot/Assets/padpilot.ico"))),
            ToolTipText = vm.TrayText,
            Menu = menu,
            IsVisible = true,
        };
        _tray = tray;
        tray.Clicked += (_, _) => ShowMainWindow();

        // El tooltip del ícono muestra la batería de cada mando sin abrir la ventana.
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.TrayText))
                tray.ToolTipText = vm.TrayText;
        };

        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            // La ventana se crea al abrirla y se destruye al cerrarla: en la bandeja no ocupa memoria de UI.
            _window = new MainWindow { DataContext = _viewModel };
            _window.Closed += (_, _) =>
            {
                _window = null;
                _viewModel?.SetWindowOpen(false);
            };
            _window.Show();
            _viewModel?.SetWindowOpen(true);
        }
        else
        {
            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;
            _window.Activate();
        }
    }
}
