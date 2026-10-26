using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PadPilot.App.Services;
using PadPilot.Core.Devices;
using PadPilot.Core.Models;
using PadPilot.Core.Services;

namespace PadPilot.App.ViewModels;

/// <summary>
/// Pestaña Audio: micrófono del mando (nivel, volumen, silencio, escucharse, grabar y reproducir)
/// y audífonos conectados al jack del mando. Solo funciona por USB.
/// </summary>
public sealed partial class AudioViewModel : ObservableObject, IDisposable
{
    private readonly GamepadController _controller;
    private readonly ControllerProfile _profile;
    private readonly Action _save;
    private readonly DispatcherTimer _meter;
    private ControllerAudio? _audio;
    private bool _syncing;
    private bool _live;

    public AudioViewModel(GamepadController controller, ControllerProfile profile, Action save)
    {
        _controller = controller;
        _profile = profile;
        _save = save;
        _meter = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => UpdateMeter());
        StatusText = "";
        MicName = "";
        HeadphoneName = "";
        MonitorTargets = [Loc.T("audio.toController"), Loc.T("audio.toPc")];
        _syncing = true;
        MuteButtonMic = profile.MuteButtonMic;
        _syncing = false;

        if (IsUsb)
        {
            _audio = new ControllerAudio(controller);
            Load();
        }
        else
        {
            StatusText = Loc.T("audio.btOnly");
        }
    }

    public bool IsUsb => _controller.Connection == ConnectionKind.Usb;

    public IReadOnlyList<string> MonitorTargets { get; }

    [ObservableProperty] public partial string StatusText { get; set; }
    [ObservableProperty] public partial bool HasMic { get; set; }
    [ObservableProperty] public partial bool HasHeadphones { get; set; }
    [ObservableProperty] public partial string MicName { get; set; }
    [ObservableProperty] public partial string HeadphoneName { get; set; }
    [ObservableProperty] public partial double MicVolume { get; set; }
    [ObservableProperty] public partial bool MicMuted { get; set; }
    [ObservableProperty] public partial double HeadphoneVolume { get; set; }
    [ObservableProperty] public partial double MicLevel { get; set; }
    [ObservableProperty] public partial bool IsMonitoring { get; set; }
    [ObservableProperty] public partial int MonitorTarget { get; set; }
    [ObservableProperty] public partial bool IsRecording { get; set; }
    [ObservableProperty] public partial bool MuteButtonMic { get; set; }

    private void Load()
    {
        if (_audio is null) return;
        _syncing = true;
        HasMic = _audio.Microphone is not null;
        HasHeadphones = _audio.Headphones is not null;
        MicName = _audio.Microphone?.FriendlyName ?? "";
        HeadphoneName = _audio.Headphones?.FriendlyName ?? "";
        MicVolume = Math.Round(_audio.MicVolume * 100);
        MicMuted = _audio.MicMuted;
        HeadphoneVolume = Math.Round(_audio.HeadphoneVolume * 100);
        IsMonitoring = false;
        _syncing = false;

        StatusText = _audio.IsAvailable
            ? ""
            : Loc.T("audio.notFound");
        SyncMuteLed();
        if (_live) _audio.StartListening();
    }

    /// <summary>La pestaña está a la vista: abrir el micrófono para el medidor.</summary>
    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        if (_audio is null) return;

        if (live)
        {
            _audio.StartListening();
            _meter.Start();
        }
        else
        {
            _meter.Stop();
            _audio.StopListening();
            _syncing = true;
            IsMonitoring = false;
            MicLevel = 0;
            _syncing = false;
        }
    }

    private void UpdateMeter()
    {
        if (_audio is null) return;
        // Escala logarítmica suave: la voz normal queda a media barra.
        float peak = _audio.TakePeak();
        double level = peak <= 0 ? 0 : Math.Clamp((20 * Math.Log10(peak) + 60) / 60 * 100, 0, 100);
        MicLevel = Math.Max(level, MicLevel * 0.85); // caída suave
    }

    partial void OnMicVolumeChanged(double value)
    {
        if (!_syncing && _audio is not null) _audio.MicVolume = (float)(value / 100);
    }

    partial void OnHeadphoneVolumeChanged(double value)
    {
        if (!_syncing && _audio is not null) _audio.HeadphoneVolume = (float)(value / 100);
    }

    partial void OnMicMutedChanged(bool value)
    {
        if (_syncing || _audio is null) return;
        _audio.MicMuted = value;
        SyncMuteLed();
    }

    partial void OnMuteButtonMicChanged(bool value)
    {
        if (_syncing) return;
        _profile.MuteButtonMic = value;
        _save();
        SyncMuteLed();
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        if (_syncing || _audio is null) return;
        if (value) _audio.StartMonitor(MonitorTarget == 0 ? _audio.Headphones : null);
        else _audio.StopMonitor();
    }

    partial void OnMonitorTargetChanged(int value)
    {
        if (_syncing || _audio is null || !IsMonitoring) return;
        _audio.StopMonitor();
        _audio.StartMonitor(value == 0 ? _audio.Headphones : null);
    }

    /// <summary>Lo llama el mando al pulsar Mute: alterna el silencio del micrófono.</summary>
    public void OnMutePressed()
    {
        if (!MuteButtonMic || _audio?.Microphone is null) return;
        MicMuted = !MicMuted;
    }

    /// <summary>El LED naranja del botón Mute refleja si el micrófono está silenciado.</summary>
    private void SyncMuteLed()
    {
        bool on = MuteButtonMic && _audio?.Microphone is not null && MicMuted;
        if (_controller.Output.MuteLed == on) return;
        _controller.Output.MuteLed = on;
        _controller.ApplyOutput();
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_audio is null) return;
        _audio.Refresh();
        Load();
    }

    [RelayCommand]
    private async Task RecordAsync()
    {
        if (_audio is null || IsRecording) return;
        IsRecording = true;
        bool wasMonitoring = IsMonitoring;
        IsMonitoring = false; // si no, se oiría dos veces
        try { await _audio.RecordAndPlayAsync(4, MonitorTarget == 0 ? _audio.Headphones : null); }
        finally
        {
            IsRecording = false;
            if (wasMonitoring && _live) IsMonitoring = true;
        }
    }

    [RelayCommand]
    private Task TestHeadphonesAsync() => _audio?.TestHeadphonesAsync() ?? Task.CompletedTask;

    public void Dispose()
    {
        _meter.Stop();
        _audio?.Dispose();
        _audio = null;
    }
}
