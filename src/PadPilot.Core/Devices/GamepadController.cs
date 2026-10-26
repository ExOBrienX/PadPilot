using HidSharp;
using PadPilot.Core.Models;
using PadPilot.Core.Protocol;

namespace PadPilot.Core.Devices;

/// <summary>
/// Base común para todos los mandos (DualSense, DualShock 4 y genéricos HID): abre el dispositivo, lee los reportes de entrada
/// en un hilo dedicado (bloqueante, sin polling, ~0 % CPU en reposo) y escribe los reportes de salida.
/// </summary>
public abstract class GamepadController : IDisposable
{
    private readonly HidDevice _device;
    private readonly object _writeLock = new();
    private readonly object _inputLock = new();
    private readonly byte[] _outputBuffer = new byte[78];
    private HidStream? _stream;
    private Thread? _reader;
    private PadInput _input;
    private volatile bool _disposed;
    private bool _hasWritten;

    protected GamepadController(HidDevice device, ControllerKind kind)
    {
        _device = device;
        Kind = kind;
        DevicePath = device.DevicePath;
        Connection = DetectConnection(device);
    }

    private static ConnectionKind DetectConnection(HidDevice device)
    {
        // Windows pone el servicio HID de Bluetooth (0x1124) o "bthledevice" (BLE) en la ruta.
        var path = device.DevicePath;
        if (path.Contains("{00001124-", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bthledevice", StringComparison.OrdinalIgnoreCase))
            return ConnectionKind.Bluetooth;

        // Sony por USB (y su adaptador inalámbrico) usa reportes de 64 bytes; por Bluetooth son más largos.
        return device.VendorID == DeviceIds.SonyVendorId && device.GetMaxInputReportLength() > 64
            ? ConnectionKind.Bluetooth
            : ConnectionKind.Usb;
    }

    protected HidDevice Device => _device;

    public string DevicePath { get; }

    /// <summary>Identificador estable del mando (su dirección MAC). Sirve para recordar su perfil.</summary>
    public string Id { get; private set; } = "";

    public ControllerKind Kind { get; }

    public ConnectionKind Connection { get; }

    /// <summary>Slot de jugador 1-4 (0 = sin slot). Lo asigna <see cref="Services.ControllerManager"/>.</summary>
    public int Slot { get; internal set; }

    public BatteryInfo Battery { get; private set; } = BatteryInfo.Unknown;

    public PadInput Input
    {
        get { lock (_inputLock) return _input; }
    }

    public OutputState Output { get; } = new();

    public abstract string DisplayName { get; }

    public virtual bool SupportsAdaptiveTriggers => false;

    public virtual bool SupportsPlayerLeds => false;

    public virtual bool SupportsLightbar => true;

    public virtual bool SupportsRumble => true;

    public virtual bool SupportsTouchpad => true;

    /// <summary>false si el mando no informa su batería (muchos genéricos).</summary>
    public virtual bool ReportsBattery => true;

    /// <summary>Resolución del touchpad (las coordenadas de <see cref="TouchPoint"/> van de 0 a este tamaño).</summary>
    public abstract (int Width, int Height) TouchpadSize { get; }

    /// <summary>
    /// Se dispara (en el hilo lector) con cada reporte, cientos de veces por segundo.
    /// Para la emulación de Xbox y el remapeo; los manejadores deben ser muy rápidos.
    /// </summary>
    public event Action<GamepadController, PadInput>? InputReceived;

    /// <summary>Se dispara (en el hilo lector) solo cuando cambia la batería.</summary>
    public event Action<GamepadController>? BatteryChanged;

    /// <summary>Se dispara (en el hilo lector) con los botones recién presionados. Útil para atajos.</summary>
    public event Action<GamepadController, PadButtons>? ButtonsPressed;

    /// <summary>Se dispara (en el hilo lector) cuando el mando se desconecta.</summary>
    public event Action<GamepadController>? Disconnected;

    public bool TryOpen()
    {
        if (!_device.TryOpen(out HidStream stream))
            return false;

        _stream = stream;
        stream.ReadTimeout = Timeout.Infinite;
        stream.WriteTimeout = 500;

        try { Initialize(stream); }
        catch (Exception) { /* los feature reports son opcionales en algunos firmwares */ }

        Id = ResolveId(stream);

        _reader = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = $"PadPilot {DisplayName} reader",
            Priority = ThreadPriority.AboveNormal,
        };
        _reader.Start();
        return true;
    }

    /// <summary>Envía el estado de <see cref="Output"/> al mando.</summary>
    public void ApplyOutput()
    {
        if (_disposed) return;
        ApplyOutputCore(lightsOnly: false);
    }

    /// <summary>
    /// Envía solo las luces (barra, LEDs de jugador y de mute) sin tocar vibración ni gatillos:
    /// sirve para reafirmar los colores aunque un juego esté usando la vibración.
    /// </summary>
    public void ApplyLights()
    {
        if (_disposed) return;
        ApplyOutputCore(lightsOnly: true);
    }

    private void ApplyOutputCore(bool lightsOnly)
    {
        var stream = _stream;
        if (stream is null) return;

        lock (_writeLock)
        {
            Array.Clear(_outputBuffer);
            int length = BuildOutputReport(_outputBuffer, Output, lightsOnly);
            if (length <= 0) return; // el mando no acepta salida (p. ej. genéricos por Bluetooth)
            _hasWritten = true;
            try { stream.Write(_outputBuffer, 0, length); }
            catch (Exception) { /* si se desconectó, el hilo lector lo detecta */ }
        }
    }

    // ---- Puntos de extensión para cada modelo ----

    /// <summary>Se llama al abrir el dispositivo (p. ej. activar reportes completos por Bluetooth).</summary>
    protected virtual void Initialize(HidStream stream) { }

    /// <summary>Lee la MAC desde un feature report, si el modelo lo permite.</summary>
    protected virtual string? TryReadMacAddress(HidStream stream) => null;

    protected abstract bool TryParseInput(ReadOnlySpan<byte> report, out PadInput input, out BatteryInfo battery);

    /// <summary>
    /// Escribe el reporte de salida en <paramref name="buffer"/> y devuelve su largo.
    /// Con <paramref name="lightsOnly"/> solo se marcan como válidas las luces.
    /// </summary>
    protected abstract int BuildOutputReport(Span<byte> buffer, OutputState state, bool lightsOnly);

    /// <summary>Deja el mando en un estado neutro antes de cerrar (gatillos sueltos, sin vibración).</summary>
    protected virtual void PrepareForClose(OutputState state)
    {
        state.RumbleStrong = 0;
        state.RumbleWeak = 0;
    }

    protected byte[] NewFeatureBuffer(byte reportId)
    {
        var buffer = new byte[Math.Max(_device.GetMaxFeatureReportLength(), 64)];
        buffer[0] = reportId;
        return buffer;
    }

    protected static string FormatMac(ReadOnlySpan<byte> bytes, bool reversed)
    {
        Span<char> chars = stackalloc char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = reversed ? bytes[bytes.Length - 1 - i] : bytes[i];
            b.TryFormat(chars[(i * 2)..], out _, "X2");
        }
        return new string(chars);
    }

    // ---- Internos ----

    private void ReadLoop()
    {
        var stream = _stream!;
        var buffer = new byte[Math.Max(_device.GetMaxInputReportLength(), 64)];
        var previousButtons = PadButtons.None;

        try
        {
            while (!_disposed)
            {
                int read;
                try { read = stream.Read(buffer, 0, buffer.Length); }
                catch (TimeoutException) { continue; }

                if (read <= 0) continue;
                if (!TryParseInput(buffer.AsSpan(0, read), out var input, out var battery)) continue;

                lock (_inputLock) _input = input;
                InputReceived?.Invoke(this, input);

                var pressed = input.Buttons & ~previousButtons;
                previousButtons = input.Buttons;
                if (pressed != PadButtons.None)
                    ButtonsPressed?.Invoke(this, pressed);

                if (battery != Battery)
                {
                    Battery = battery;
                    BatteryChanged?.Invoke(this);
                }
            }
        }
        catch (Exception)
        {
            // IOException / ObjectDisposedException: el mando se desconectó o se cerró.
        }
        finally
        {
            if (!_disposed)
                Disconnected?.Invoke(this);
        }
    }

    private string ResolveId(HidStream stream)
    {
        try
        {
            var mac = TryReadMacAddress(stream);
            if (!string.IsNullOrEmpty(mac)) return mac;
        }
        catch (Exception) { }

        try
        {
            // Por Bluetooth, Windows expone la MAC como número de serie.
            var serial = new string(_device.GetSerialNumber().Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (serial.Length == 12) return serial;
        }
        catch (Exception) { }

        return "PATH-" + StableHash(DevicePath).ToString("X8");
    }

    private static uint StableHash(string text)
    {
        uint hash = 2166136261; // FNV-1a
        foreach (char c in text.ToUpperInvariant())
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_stream is not null)
        {
            try
            {
                // Solo si PadPilot ya le había mandado algo: una conexión abierta solo para leer su MAC
                // (p. ej. el mismo mando por USB y Bluetooth) no debe pisar las luces del mando.
                if (_hasWritten)
                {
                    PrepareForClose(Output);
                    ApplyOutputCore(lightsOnly: false);
                }
            }
            catch (Exception) { }
        }

        _disposed = true;
        try { _stream?.Dispose(); } catch (Exception) { }
        GC.SuppressFinalize(this);
    }
}
