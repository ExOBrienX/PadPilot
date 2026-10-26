using HidSharp;
using PadPilot.Core.Devices;
using PadPilot.Core.Models;

namespace PadPilot.Core.Services;

/// <summary>
/// Corazón del plug and play: detecta mandos al conectarse (USB o Bluetooth), les asigna un slot
/// de jugador y les aplica su perfil guardado. Sin polling: reacciona a los eventos del sistema.
/// </summary>
public sealed class ControllerManager : IDisposable
{
    private readonly ProfileStore _profiles;
    private readonly object _gate = new();
    private readonly Dictionary<string, GamepadController> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly GamepadController?[] _slots = new GamepadController?[PlayerSlots.Count];
    private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);
    // Mandos ya conectados por otra vía (p. ej. Bluetooth mientras están por USB): no reabrirlos en cada escaneo.
    private readonly HashSet<string> _duplicates = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _rescanTimer;
    private bool _started;
    private bool _disposed;

    public ControllerManager(ProfileStore profiles) => _profiles = profiles;

    public ProfileStore Profiles => _profiles;

    public event Action<GamepadController>? ControllerConnected;
    public event Action<GamepadController>? ControllerDisconnected;
    public event Action? SlotsChanged;

    public IReadOnlyList<GamepadController> Controllers
    {
        get { lock (_gate) return _byPath.Values.ToList(); }
    }

    public void Start()
    {
        if (_started) return;
        _started = true;

        _rescanTimer = new Timer(_ => Rescan(), null, Timeout.Infinite, Timeout.Infinite);
        DeviceList.Local.Changed += OnDeviceListChanged;
        Rescan();
    }

    // Windows avisa varias veces seguidas al conectar algo; esperamos 400 ms y escaneamos una vez.
    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e) =>
        _rescanTimer?.Change(400, Timeout.Infinite);

    private void Rescan()
    {
        var added = new List<GamepadController>();
        var replaced = new List<GamepadController>();

        lock (_gate)
        {
            if (_disposed) return;

            var devices = DeviceList.Local.GetHidDevices().ToList();
            _duplicates.IntersectWith(devices.Select(d => d.DevicePath)); // olvidar los que ya no están

            foreach (var device in devices)
            {
                if (_byPath.ContainsKey(device.DevicePath) || _ignored.Contains(device.DevicePath)
                    || _duplicates.Contains(device.DevicePath)) continue;

                var controller = Create(device);
                if (controller is null)
                {
                    _ignored.Add(device.DevicePath); // teclados, ratones, etc.: no volver a mirarlos
                    continue;
                }

                try
                {
                    if (!controller.TryOpen())
                    {
                        controller.Dispose();
                        continue;
                    }
                }
                catch (Exception)
                {
                    controller.Dispose();
                    continue;
                }

                // El mismo mando por USB y por Bluetooth a la vez: se prefiere el USB (tiene audio y menos latencia).
                if (_byPath.Values.FirstOrDefault(c => c.Id == controller.Id) is { } existing)
                {
                    if (controller.Connection == ConnectionKind.Usb && existing.Connection == ConnectionKind.Bluetooth)
                    {
                        existing.Disconnected -= OnControllerDisconnected;
                        _byPath.Remove(existing.DevicePath);
                        for (int i = 0; i < _slots.Length; i++)
                            if (ReferenceEquals(_slots[i], existing)) _slots[i] = null;
                        _duplicates.Add(existing.DevicePath);
                        replaced.Add(existing);
                    }
                    else
                    {
                        controller.Dispose(); // no escribe nada: nunca se le envió un reporte
                        _duplicates.Add(device.DevicePath);
                        continue;
                    }
                }

                if (controller is GenericHidController generic)
                    generic.SetLayout(_profiles.GetGenericLayout(generic.ModelKey) ?? generic.StandardLayout());

                _byPath[device.DevicePath] = controller;
                controller.Disconnected += OnControllerDisconnected;
                AssignInitialSlot(controller);
                ApplyProfile(controller);
                added.Add(controller);
            }
        }

        foreach (var controller in replaced)
        {
            controller.Dispose();
            ControllerDisconnected?.Invoke(controller);
        }
        foreach (var controller in added)
            ControllerConnected?.Invoke(controller);
        if (added.Count > 0 || replaced.Count > 0)
            SlotsChanged?.Invoke();
    }

    /// <summary>DualSense/DS4 por su VID/PID; cualquier otro gamepad HID como genérico.</summary>
    private static GamepadController? Create(HidDevice device)
    {
        if (device.VendorID == DeviceIds.SonyVendorId)
        {
            if (DeviceIds.Identify(device.ProductID) is not { } kind) return null;
            return kind == ControllerKind.DualShock4
                ? new DualShock4Controller(device, kind)
                : new DualSenseController(device, kind);
        }

        // Los mandos XInput (Xbox y los virtuales de ViGEm, incluidos los nuestros) llevan "IG_" en la ruta:
        // Windows y los juegos ya los manejan, y leerlos aquí crearía un bucle con la emulación.
        if (device.DevicePath.Contains("&ig_", StringComparison.OrdinalIgnoreCase)) return null;

        try { return GenericHidController.TryCreate(device); }
        catch (Exception) { return null; }
    }

    private void OnControllerDisconnected(GamepadController controller)
    {
        lock (_gate)
        {
            _byPath.Remove(controller.DevicePath);
            _ignored.Remove(controller.DevicePath);
            // Si estaba también por otra vía (USB desenchufado, sigue por Bluetooth), retomar esa conexión.
            _duplicates.Clear();
            _rescanTimer?.Change(400, Timeout.Infinite);
            for (int i = 0; i < _slots.Length; i++)
                if (ReferenceEquals(_slots[i], controller)) _slots[i] = null;
        }

        controller.Dispose();
        ControllerDisconnected?.Invoke(controller);
        SlotsChanged?.Invoke();
    }

    private void AssignInitialSlot(GamepadController controller)
    {
        int preferred = _profiles.GetOrCreate(controller).PreferredSlot;
        int index = preferred is >= 1 and <= PlayerSlots.Count && _slots[preferred - 1] is null
            ? preferred - 1
            : Array.IndexOf(_slots, null);

        if (index >= 0)
        {
            _slots[index] = controller;
            controller.Slot = index + 1;
        }
        else
        {
            controller.Slot = 0; // más de 4 mandos conectados
        }
    }

    /// <summary>Mueve el mando al slot indicado; si estaba ocupado, intercambia los dos mandos.</summary>
    public void AssignSlot(GamepadController controller, int slot)
    {
        if (slot is < 1 or > PlayerSlots.Count) return;

        lock (_gate)
        {
            if (controller.Slot == slot) return;

            var other = _slots[slot - 1];
            int oldSlot = controller.Slot;

            if (oldSlot >= 1) _slots[oldSlot - 1] = other;
            if (other is not null) other.Slot = oldSlot;

            _slots[slot - 1] = controller;
            controller.Slot = slot;

            foreach (var touched in new[] { controller, other })
            {
                if (touched is null) continue;
                _profiles.GetOrCreate(touched).PreferredSlot = touched.Slot;
                ApplyProfile(touched);
            }
        }

        _profiles.SaveDeferred();
        SlotsChanged?.Invoke();
    }

    /// <summary>Traduce el perfil + slot actual a colores, LEDs y gatillos, y lo envía al mando.</summary>
    public void ApplyProfile(GamepadController controller)
    {
        var profile = _profiles.GetOrCreate(controller);
        var output = controller.Output;

        var color = EffectiveColor(controller);
        output.Lightbar = color.Scale(profile.LightbarIntensity / 100.0);
        output.PlayerLeds = PlayerSlots.DualSenseLeds(controller.Slot);
        output.PlayerLedBrightness = profile.PlayerLedBrightness;

        if (controller.SupportsAdaptiveTriggers)
        {
            output.LeftTrigger = profile.LeftTrigger;
            output.RightTrigger = profile.RightTrigger;
        }

        controller.ApplyOutput();
    }

    /// <summary>Color de la barra de luz sin aplicar la intensidad: el del slot o el propio del mando.</summary>
    public Rgb EffectiveColor(GamepadController controller)
    {
        var profile = _profiles.GetOrCreate(controller);
        return profile.UseSlotColor ? _profiles.GetSlotColor(controller.Slot) : profile.Color;
    }

    /// <summary>Cambia el color de un slot de jugador y lo aplica a los mandos que usan "color del jugador".</summary>
    public void SetSlotColor(int slot, Rgb color)
    {
        _profiles.SetSlotColor(slot, color);
        foreach (var controller in Controllers)
            if (controller.Slot == slot && _profiles.GetOrCreate(controller).UseSlotColor)
                ApplyProfile(controller);
        SlotColorsChanged?.Invoke();
    }

    /// <summary>Se dispara cuando cambia el color de algún slot (para refrescar la interfaz).</summary>
    public event Action? SlotColorsChanged;

    public void Dispose()
    {
        List<GamepadController> all;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            all = _byPath.Values.ToList();
            _byPath.Clear();
        }

        DeviceList.Local.Changed -= OnDeviceListChanged;
        _rescanTimer?.Dispose();

        foreach (var controller in all)
        {
            controller.Disconnected -= OnControllerDisconnected;
            controller.Dispose();
        }

        _profiles.Dispose();
    }
}
