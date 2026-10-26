using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using PadPilot.Core.Devices;
using PadPilot.Core.Models;

namespace PadPilot.App.Services;

/// <summary>
/// Audio del mando por USB: el micrófono y la salida de audífonos (jack de 3,5 mm) que Windows expone
/// como dispositivos de sonido. Por Bluetooth Windows no tiene acceso al audio del mando.
/// </summary>
public sealed class ControllerAudio : IDisposable
{
    private readonly GamepadController _controller;
    private WasapiCapture? _capture;
    private WasapiOut? _monitorOut;
    private BufferedWaveProvider? _monitorBuffer;
    private MemoryStream? _recording;
    private WaveFormat? _recordingFormat;
    private volatile float _peak;

    public ControllerAudio(GamepadController controller)
    {
        _controller = controller;
        Refresh();
    }

    public MMDevice? Microphone { get; private set; }

    public MMDevice? Headphones { get; private set; }

    public bool IsAvailable => Microphone is not null || Headphones is not null;

    /// <summary>Pico del micrófono (0-1) desde la última lectura. Se reinicia al leerlo.</summary>
    public float TakePeak()
    {
        float p = _peak;
        _peak = 0;
        return p;
    }

    public bool IsListening => _capture is not null;

    public bool IsMonitoring => _monitorOut is not null;

    public bool IsRecording => _recording is not null;

    /// <summary>Busca los dispositivos de sonido del mando (aparecen unos segundos después de enchufarlo).</summary>
    public void Refresh()
    {
        StopListening();
        Microphone = Find(DataFlow.Capture);
        Headphones = Find(DataFlow.Render);
    }

    private MMDevice? Find(DataFlow flow)
    {
        if (_controller.Connection != ConnectionKind.Usb) return null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                string name = device.DeviceFriendlyName ?? "";
                bool match = _controller.Kind == ControllerKind.DualShock4
                    ? name.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase)
                      && !name.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
                    : name.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
                      || name.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase);
                if (match) return device;
                device.Dispose();
            }
        }
        catch (Exception) { }
        return null;
    }

    // ---- Volumen y silencio (los de Windows, que afectan a todas las apps) ----

    public float MicVolume
    {
        get => Safe(() => Microphone?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0);
        set => Safe(() => { if (Microphone is { } m) m.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0, 1); });
    }

    public bool MicMuted
    {
        get => Safe(() => Microphone?.AudioEndpointVolume.Mute ?? false);
        set => Safe(() => { if (Microphone is { } m) m.AudioEndpointVolume.Mute = value; });
    }

    public float HeadphoneVolume
    {
        get => Safe(() => Headphones?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0);
        set => Safe(() => { if (Headphones is { } h) h.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0, 1); });
    }

    // ---- Escuchar el micrófono: medidor, escucha en vivo y grabación ----

    /// <summary>Abre el micrófono para el medidor de nivel. No se oye nada hasta activar la escucha.</summary>
    public void StartListening()
    {
        if (_capture is not null || Microphone is null) return;
        try
        {
            var capture = new WasapiCapture(Microphone, true, 20);
            capture.DataAvailable += OnData;
            capture.StartRecording();
            _capture = capture;
        }
        catch (Exception) { _capture = null; }
    }

    public void StopListening()
    {
        StopMonitor();
        _recording = null;
        if (_capture is null) return;
        try
        {
            _capture.DataAvailable -= OnData;
            _capture.StopRecording();
            _capture.Dispose();
        }
        catch (Exception) { }
        _capture = null;
    }

    /// <summary>Reproduce el micrófono en vivo por <paramref name="output"/> (null = salida predeterminada de Windows).</summary>
    public void StartMonitor(MMDevice? output)
    {
        StartListening();
        if (_capture is null || _monitorOut is not null) return;
        try
        {
            _monitorBuffer = new BufferedWaveProvider(_capture.WaveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMilliseconds(400),
            };
            var player = output is null ? new WasapiOut(AudioClientShareMode.Shared, 60) : new WasapiOut(output, AudioClientShareMode.Shared, true, 60);
            player.Init(_monitorBuffer);
            player.Play();
            _monitorOut = player;
        }
        catch (Exception)
        {
            _monitorOut = null;
            _monitorBuffer = null;
        }
    }

    public void StopMonitor()
    {
        try { _monitorOut?.Stop(); _monitorOut?.Dispose(); } catch (Exception) { }
        _monitorOut = null;
        _monitorBuffer = null;
    }

    /// <summary>Graba <paramref name="seconds"/> segundos y los reproduce por <paramref name="output"/>.</summary>
    public async Task RecordAndPlayAsync(int seconds, MMDevice? output)
    {
        StartListening();
        if (_capture is null) return;

        _recordingFormat = _capture.WaveFormat;
        _recording = new MemoryStream();
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        var data = _recording?.ToArray();
        _recording = null;
        if (data is null || data.Length == 0) return;

        await PlayAsync(new RawSourceWaveStream(new MemoryStream(data), _recordingFormat), output);
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var format = _capture?.WaveFormat;
        if (format is null) return;

        float peak = 0;
        if (format.Encoding == WaveFormatEncoding.IeeeFloat || (format is WaveFormatExtensible && format.BitsPerSample == 32))
        {
            for (int i = 0; i + 4 <= e.BytesRecorded; i += 4)
                peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(e.Buffer, i)));
        }
        else if (format.BitsPerSample == 16)
        {
            for (int i = 0; i + 2 <= e.BytesRecorded; i += 2)
                peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f));
        }
        if (peak > _peak) _peak = Math.Min(peak, 1);

        _monitorBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        _recording?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    // ---- Audífonos ----

    /// <summary>Un tono corto por la izquierda y luego por la derecha, en los audífonos del mando.</summary>
    public Task TestHeadphonesAsync()
    {
        if (Headphones is null) return Task.CompletedTask;
        ISampleProvider Tone(float pan, double seconds) => new PanningSampleProvider(
                new SignalGenerator(48000, 1) { Type = SignalGeneratorType.Sin, Frequency = 660, Gain = 0.25 }
                    .Take(TimeSpan.FromSeconds(seconds)))
            { Pan = pan };

        var sequence = new ConcatenatingSampleProvider([Tone(-1, 0.5), Tone(1, 0.5)]);
        return PlayAsync(sequence.ToWaveProvider(), Headphones);
    }

    private static async Task PlayAsync(IWaveProvider source, MMDevice? output)
    {
        try
        {
            using var player = output is null ? new WasapiOut(AudioClientShareMode.Shared, 100) : new WasapiOut(output, AudioClientShareMode.Shared, true, 100);
            var done = new TaskCompletionSource();
            player.PlaybackStopped += (_, _) => done.TrySetResult();
            player.Init(source);
            player.Play();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (Exception) { }
    }

    private static T Safe<T>(Func<T> get)
    {
        try { return get(); } catch (Exception) { return default!; }
    }

    private static void Safe(Action set)
    {
        try { set(); } catch (Exception) { }
    }

    public void Dispose()
    {
        StopListening();
        Microphone?.Dispose();
        Headphones?.Dispose();
        Microphone = null;
        Headphones = null;
    }
}
