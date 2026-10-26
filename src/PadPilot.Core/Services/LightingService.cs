using PadPilot.Core.Devices;
using PadPilot.Core.Models;

namespace PadPilot.Core.Services;

/// <summary>Avisos de batería en la barra de luz. Se guardan en profiles.json y valen para todos los mandos.</summary>
public sealed class LightingSettings
{
    /// <summary>Barra roja parpadeando con poca batería (sin cargar).</summary>
    public bool LowBatteryAlert { get; set; } = true;

    /// <summary>Aviso desde este % o menos. El mando informa en pasos de 10 (5, 15, 25…).</summary>
    public int LowBatteryPercent { get; set; } = 15;

    /// <summary>Cargando: verde; azul desde <see cref="ChargedBlueAt"/>; azul parpadeando desde 90 % o lleno.</summary>
    public bool ChargingColors { get; set; } = true;

    public int ChargedBlueAt { get; set; } = 80;

    /// <summary>
    /// Volver a enviar las luces de PadPilot cada segundo, por si un juego, Steam u otra app las cambió.
    /// Solo se mandan las luces: la vibración y los gatillos del juego no se tocan.
    /// </summary>
    public bool EnforceLights { get; set; } = true;
}

/// <summary>
/// Decide el color "con prioridad" de cada mando (aviso de carga o batería baja) y mantiene las luces:
/// parpadeos por software y reenvío periódico para que nada más las pise. Un tick cada 250 ms.
/// </summary>
public sealed class LightingService : IDisposable
{
    private static readonly Rgb Red = new(255, 0, 0);
    private static readonly Rgb Green = new(0, 255, 40);
    private static readonly Rgb Blue = new(0, 64, 255);
    private static readonly Rgb Off = new(0, 0, 0);

    private readonly ControllerManager _manager;
    private readonly Timer _timer;
    private readonly Dictionary<GamepadController, (Rgb Light, byte Leds, bool Mute, long At)> _sent = new();
    private int _tick;
    private int _busy;

    public LightingService(ControllerManager manager)
    {
        _manager = manager;
        _timer = new Timer(_ => Tick(), null, 250, 250);
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return; // el tick anterior aún no termina
        try
        {
            _tick++;
            bool slowBlink = _tick / 2 % 2 == 0; // 500 ms encendido / 500 ms apagado
            bool fastBlink = _tick % 2 == 0;     // 250 / 250
            var settings = _manager.Profiles.Lighting;
            long now = Environment.TickCount64;
            var alive = new HashSet<GamepadController>();

            foreach (var controller in _manager.Controllers)
            {
                if (!controller.SupportsLightbar) continue;
                alive.Add(controller);

                var output = controller.Output;
                var profile = _manager.Profiles.GetOrCreate(controller);
                double intensity = Math.Max(profile.LightbarIntensity, 25) / 100.0; // el aviso siempre se ve
                output.LightbarOverride = Decide(controller.Battery, settings, slowBlink, fastBlink)?.Scale(intensity);

                var state = (output.EffectiveLightbar, output.PlayerLeds, output.MuteLed);
                bool changed = !_sent.TryGetValue(controller, out var last)
                    || last.Light != state.Item1 || last.Leds != state.Item2 || last.Mute != state.Item3;
                bool refresh = settings.EnforceLights && now - last.At >= 1000;

                if (changed || refresh)
                {
                    controller.ApplyLights();
                    _sent[controller] = (state.Item1, state.Item2, state.Item3, now);
                }
            }

            foreach (var gone in _sent.Keys.Where(c => !alive.Contains(c)).ToList())
                _sent.Remove(gone);
        }
        catch (Exception) { /* un mando que se desconecta a mitad de tick */ }
        finally { Volatile.Write(ref _busy, 0); }
    }

    /// <summary>null = sin aviso: se usa el color del perfil.</summary>
    private static Rgb? Decide(BatteryInfo battery, LightingSettings s, bool slowBlink, bool fastBlink)
    {
        bool charging = battery.Status is ChargeStatus.Charging or ChargeStatus.Full;

        if (s.ChargingColors && charging)
        {
            int percent = battery.Status == ChargeStatus.Full ? 100 : battery.Percent;
            if (percent >= 90) return slowBlink ? Blue : Off;
            return percent >= s.ChargedBlueAt ? Blue : Green;
        }

        if (s.LowBatteryAlert && battery.Status == ChargeStatus.Discharging && battery.Percent <= s.LowBatteryPercent)
        {
            // Al límite (5 % o menos) parpadea más rápido.
            bool on = battery.Percent <= 5 ? fastBlink : slowBlink;
            return on ? Red : Off;
        }

        return null;
    }

    public void Dispose() => _timer.Dispose();
}
