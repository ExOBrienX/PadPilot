using CommunityToolkit.Mvvm.ComponentModel;
using PadPilot.Core.Services;

namespace PadPilot.App.ViewModels;

/// <summary>Ajustes de los avisos de batería en la barra de luz (valen para todos los mandos).</summary>
public sealed partial class BatteryLightsViewModel : ObservableObject
{
    private readonly ProfileStore _profiles;
    private readonly LightingSettings _settings;

    public BatteryLightsViewModel(ProfileStore profiles)
    {
        _profiles = profiles;
        _settings = profiles.Lighting;
        LowBatteryAlert = _settings.LowBatteryAlert;
        LowBatteryPercent = _settings.LowBatteryPercent;
        ChargingColors = _settings.ChargingColors;
        ChargedBlueAt = _settings.ChargedBlueAt;
        EnforceLights = _settings.EnforceLights;
    }

    [ObservableProperty] public partial bool LowBatteryAlert { get; set; }
    [ObservableProperty] public partial double LowBatteryPercent { get; set; }
    [ObservableProperty] public partial bool ChargingColors { get; set; }
    [ObservableProperty] public partial double ChargedBlueAt { get; set; }
    [ObservableProperty] public partial bool EnforceLights { get; set; }

    partial void OnLowBatteryAlertChanged(bool value) => Save(() => _settings.LowBatteryAlert = value);
    partial void OnLowBatteryPercentChanged(double value) => Save(() => _settings.LowBatteryPercent = (int)Math.Round(value));
    partial void OnChargingColorsChanged(bool value) => Save(() => _settings.ChargingColors = value);
    partial void OnChargedBlueAtChanged(double value) => Save(() => _settings.ChargedBlueAt = (int)Math.Round(value));
    partial void OnEnforceLightsChanged(bool value) => Save(() => _settings.EnforceLights = value);

    // El servicio de luces lee estos ajustes en su próximo tick (≤ 250 ms): no hay que avisarle.
    private void Save(Action change)
    {
        change();
        _profiles.SaveDeferred();
    }
}
