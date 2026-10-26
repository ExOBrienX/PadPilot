using System.Collections.Concurrent;
using System.Numerics;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using PadPilot.Core.Devices;
using PadPilot.Core.Models;
using PadPilot.Core.Services;

namespace PadPilot.App.Services;

/// <summary>
/// Lleva la entrada de cada mando a donde corresponda: un Xbox 360 virtual (ViGEmBus) y/o teclas de Windows,
/// según el remapeo de su perfil. La vibración que el juego pide al Xbox virtual vuelve al mando real.
/// </summary>
public sealed class InputRouter : IDisposable
{
    private sealed class Route(GamepadController controller)
    {
        public readonly GamepadController Controller = controller;
        public readonly object Sync = new();
        public readonly ButtonAction[] Map = new ButtonAction[32]; // índice = bit de PadButtons
        public bool HasKeys;
        public StickSettings LeftStick = new(), RightStick = new();
        public IXbox360Controller? Pad;
        public int XboxIndex = -1;
        public PadButtons Previous;
        public PadInput LastInput;
        public readonly CancellationTokenSource?[] Macros = new CancellationTokenSource?[32]; // una por botón
        public ushort MacroButtons;
        public byte MacroLt, MacroRt;
        public bool HasLast;
        public ushort LastButtons;
        public byte LastLt, LastRt;
        public short LastLx, LastLy, LastRx, LastRy;
    }

    private readonly ControllerManager _manager;
    private readonly ConcurrentDictionary<GamepadController, Route> _routes = new();
    private readonly List<Route> _padOrder = []; // orden en que se conectaron los Xbox virtuales
    private ViGEmClient? _client;

    public InputRouter(ControllerManager manager)
    {
        _manager = manager;
        manager.ControllerConnected += c => OnUi(() => Attach(c));
        manager.ControllerDisconnected += c => OnUi(() => Detach(c));
        manager.SlotsChanged += () => OnUi(ReorderPads);

        foreach (var controller in manager.Controllers)
            Attach(controller);

        if (manager.Profiles.EmulateXbox)
            SetEmulation(true);
    }

    /// <summary>Cambió el estado de la emulación o el índice Xbox de algún mando. Puede llegar desde otro hilo.</summary>
    public event Action? StatusChanged;

    public bool IsEmulating => _client is not null;

    /// <summary>Por qué no se pudo activar la emulación (p. ej. ViGEmBus no instalado).</summary>
    public string? EmulationError { get; private set; }

    /// <summary>Jugador Xbox (1-4) que Windows le dio al mando virtual, o 0 si no se sabe.</summary>
    public int XboxPlayer(GamepadController controller) =>
        _routes.TryGetValue(controller, out var r) && r.XboxIndex >= 0 ? r.XboxIndex + 1 : 0;

    public bool SetEmulation(bool enabled)
    {
        _manager.Profiles.EmulateXbox = enabled;

        if (!enabled)
        {
            foreach (var route in _padOrder.ToList()) DisconnectPad(route);
            _padOrder.Clear();
            _client?.Dispose();
            _client = null;
            EmulationError = null;
            StatusChanged?.Invoke();
            return true;
        }

        if (_client is not null) return true;

        try
        {
            _client = new ViGEmClient();
            EmulationError = null;
        }
        catch (Exception)
        {
            _manager.Profiles.EmulateXbox = false;
            EmulationError = Loc.T("emu.noVigem");
            StatusChanged?.Invoke();
            return false;
        }

        foreach (var route in _routes.Values.OrderBy(SlotOrder))
            ConnectPad(route);
        StatusChanged?.Invoke();
        return true;
    }

    /// <summary>Vuelve a leer el remapeo y los sticks del perfil (llamar tras cambiarlos).</summary>
    public void RefreshMap(GamepadController controller)
    {
        if (!_routes.TryGetValue(controller, out var route)) return;
        var profile = _manager.Profiles.GetOrCreate(controller);

        lock (route.Sync)
        {
            ReleaseKeys(route); // por si había una tecla apretada con la asignación anterior
            Array.Fill(route.Map, ButtonActions.Default);
            foreach (var source in ButtonActions.Remappable)
            {
                profile.ButtonMap.TryGetValue(source.ToString(), out var id);
                route.Map[Bit(source)] = ButtonActions.Find(id);
            }
            route.HasKeys = route.Map.Any(a => a.Kind is ActionKind.Keys or ActionKind.Macro);
            route.LeftStick = profile.LeftStick;
            route.RightStick = profile.RightStick;
            route.HasLast = false;
        }
    }

    // ---- Ciclo de vida ----

    private void Attach(GamepadController controller)
    {
        var route = new Route(controller);
        if (!_routes.TryAdd(controller, route)) return;
        RefreshMap(controller);
        controller.InputReceived += OnInput;
        if (_client is not null) ConnectPad(route);
    }

    private void Detach(GamepadController controller)
    {
        if (!_routes.TryRemove(controller, out var route)) return;
        controller.InputReceived -= OnInput;
        lock (route.Sync) ReleaseKeys(route);
        DisconnectPad(route);
        _padOrder.Remove(route);
        StatusChanged?.Invoke();
    }

    private void ConnectPad(Route route)
    {
        if (_client is null || route.Pad is not null) return;
        try
        {
            var pad = _client.CreateXbox360Controller();
            pad.AutoSubmitReport = false;
            pad.FeedbackReceived += (_, e) => OnFeedback(route, e);
            pad.Connect();
            route.Pad = pad;
            route.HasLast = false;
            _padOrder.Add(route);
            try { route.XboxIndex = pad.UserIndex; } catch (Exception) { route.XboxIndex = -1; }
        }
        catch (Exception)
        {
            route.Pad = null;
        }
    }

    private void DisconnectPad(Route route)
    {
        var pad = route.Pad;
        if (pad is null) return;
        lock (route.Sync) route.Pad = null;
        route.XboxIndex = -1;
        try { pad.Disconnect(); } catch (Exception) { }

        // Que no quede vibrando lo último que pidió el juego.
        route.Controller.Output.RumbleStrong = 0;
        route.Controller.Output.RumbleWeak = 0;
        route.Controller.ApplyOutput();
    }

    /// <summary>
    /// Windows numera los Xbox virtuales en el orden en que se conectan. Para que J1 sea el jugador 1
    /// en el juego, si el orden no coincide con los slots se reconectan todos en orden.
    /// </summary>
    private void ReorderPads()
    {
        if (_client is null) return;
        var desired = _routes.Values.OrderBy(SlotOrder).ToList();
        if (desired.SequenceEqual(_padOrder)) return;

        foreach (var route in _padOrder.ToList()) DisconnectPad(route);
        _padOrder.Clear();
        foreach (var route in desired) ConnectPad(route);
        StatusChanged?.Invoke();
    }

    private static int SlotOrder(Route r) => r.Controller.Slot == 0 ? int.MaxValue : r.Controller.Slot;

    private void OnFeedback(Route route, Xbox360FeedbackReceivedEventArgs e)
    {
        var output = route.Controller.Output;
        if (output.RumbleStrong != e.LargeMotor || output.RumbleWeak != e.SmallMotor)
        {
            output.RumbleStrong = e.LargeMotor;
            output.RumbleWeak = e.SmallMotor;
            route.Controller.ApplyOutput();
        }

        if (route.XboxIndex != e.LedNumber)
        {
            route.XboxIndex = e.LedNumber;
            StatusChanged?.Invoke();
        }
    }

    // ---- Hilo lector: cientos de veces por segundo, sin asignaciones ----

    private void OnInput(GamepadController controller, PadInput input)
    {
        if (!_routes.TryGetValue(controller, out var route)) return;

        lock (route.Sync)
        {
            var pad = route.Pad;
            if (pad is null && !route.HasKeys) return;

            route.LastInput = input;
            var buttons = input.Buttons;
            var changed = buttons ^ route.Previous;
            route.Previous = buttons;

            if (route.HasKeys && changed != PadButtons.None)
                SendKeys(route, buttons, changed);

            if (pad is not null)
                SubmitXbox(route, pad, input);
        }
    }

    private void SendKeys(Route route, PadButtons buttons, PadButtons changed)
    {
        uint bits = (uint)changed;
        while (bits != 0)
        {
            int bit = BitOperations.TrailingZeroCount(bits);
            bits &= bits - 1;
            bool down = ((uint)buttons & (1u << bit)) != 0;
            var action = route.Map[bit];

            if (action is { Kind: ActionKind.Macro, MacroId: { } macroId })
            {
                if (down) StartMacro(route, bit, macroId);
                else if (_manager.Profiles.FindMacro(macroId) is { RepeatWhileHeld: true }) StopMacro(route, bit);
                continue;
            }

            if (action is not { Kind: ActionKind.Keys, Keys: { } keys }) continue;

            if (down)
                foreach (var key in keys) KeyboardOutput.Down(key);
            else
                for (int i = keys.Length - 1; i >= 0; i--) KeyboardOutput.Up(keys[i]);
        }
    }

    // ---- Macros: corren en su propia tarea, sin bloquear el hilo lector ----

    private void StartMacro(Route route, int bit, string macroId)
    {
        var macro = _manager.Profiles.FindMacro(macroId);
        if (macro is null || macro.Steps.Count == 0) return;

        StopMacro(route, bit);
        var cts = new CancellationTokenSource();
        route.Macros[bit] = cts;
        var steps = macro.Steps.Select(s => (Action: ButtonActions.Find(s.Action), s.HoldMs, s.DelayMs)).ToArray();
        _ = Task.Run(() => RunMacroAsync(route, steps, macro.RepeatWhileHeld, cts.Token));
    }

    private static void StopMacro(Route route, int bit)
    {
        route.Macros[bit]?.Cancel();
        route.Macros[bit] = null;
    }

    private static async Task RunMacroAsync(Route route, (ButtonAction Action, int HoldMs, int DelayMs)[] steps, bool repeat, CancellationToken token)
    {
        try
        {
            do
            {
                foreach (var (action, hold, delay) in steps)
                {
                    PressMacroAction(route, action, true);
                    try { await Task.Delay(Math.Max(hold, 1), token); }
                    finally { PressMacroAction(route, action, false); }
                    await Task.Delay(Math.Max(delay, 0), token);
                }
            }
            while (repeat && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException) { }
    }

    private static void PressMacroAction(Route route, ButtonAction action, bool down)
    {
        switch (action)
        {
            case { Kind: ActionKind.Keys, Keys: { } keys }:
                if (down) foreach (var key in keys) KeyboardOutput.Down(key);
                else for (int i = keys.Length - 1; i >= 0; i--) KeyboardOutput.Up(keys[i]);
                break;

            case { Kind: ActionKind.Xbox, Xbox: var xbox }:
                lock (route.Sync)
                {
                    if (xbox == XboxButton.LeftTrigger) route.MacroLt = down ? (byte)255 : (byte)0;
                    else if (xbox == XboxButton.RightTrigger) route.MacroRt = down ? (byte)255 : (byte)0;
                    else if (down) route.MacroButtons |= (ushort)xbox;
                    else route.MacroButtons &= (ushort)~(ushort)xbox;

                    route.HasLast = false; // forzar el envío aunque el mando no haya cambiado
                    if (route.Pad is { } pad) SubmitXbox(route, pad, route.LastInput);
                }
                break;
        }
    }

    private static void ReleaseKeys(Route route)
    {
        for (int i = 0; i < route.Macros.Length; i++) StopMacro(route, i);
        route.MacroButtons = 0;
        route.MacroLt = route.MacroRt = 0;

        uint bits = (uint)route.Previous;
        while (bits != 0)
        {
            int bit = BitOperations.TrailingZeroCount(bits);
            bits &= bits - 1;
            if (route.Map[bit] is { Kind: ActionKind.Keys, Keys: { } keys })
                for (int i = keys.Length - 1; i >= 0; i--) KeyboardOutput.Up(keys[i]);
        }
        route.Previous = PadButtons.None;
    }

    private static void SubmitXbox(Route route, IXbox360Controller pad, PadInput input)
    {
        ushort xb = 0;
        byte lt = 0, rt = 0;

        foreach (var source in ButtonActions.Remappable)
        {
            var action = route.Map[Bit(source)];
            var target = action.Kind switch
            {
                ActionKind.Default => ButtonActions.DefaultXbox(source),
                ActionKind.Xbox => action.Xbox,
                _ => XboxButton.None,
            };
            if (target == XboxButton.None) continue;

            // L2/R2 son analógicos: si van a un gatillo Xbox se pasa el recorrido completo.
            bool pressed = (input.Buttons & source) != 0;
            byte value = source switch
            {
                PadButtons.L2 => input.L2,
                PadButtons.R2 => input.R2,
                _ => pressed ? (byte)255 : (byte)0,
            };

            if (target == XboxButton.LeftTrigger) lt = Math.Max(lt, value);
            else if (target == XboxButton.RightTrigger) rt = Math.Max(rt, value);
            else if (pressed) xb |= (ushort)target;
        }

        var (lnx, lny) = route.LeftStick.Apply(input.LeftX, input.LeftY);
        var (rnx, rny) = route.RightStick.Apply(input.RightX, input.RightY);
        xb |= route.MacroButtons;
        lt = Math.Max(lt, route.MacroLt);
        rt = Math.Max(rt, route.MacroRt);

        short lx = Axis(lnx), ly = Axis(-lny);
        short rx = Axis(rnx), ry = Axis(-rny);

        if (route.HasLast && xb == route.LastButtons && lt == route.LastLt && rt == route.LastRt
            && lx == route.LastLx && ly == route.LastLy && rx == route.LastRx && ry == route.LastRy)
            return;

        route.HasLast = true;
        route.LastButtons = xb; route.LastLt = lt; route.LastRt = rt;
        route.LastLx = lx; route.LastLy = ly; route.LastRx = rx; route.LastRy = ry;

        try
        {
            pad.SetButtonsFull(xb);
            pad.SetSliderValue(Xbox360Slider.LeftTrigger, lt);
            pad.SetSliderValue(Xbox360Slider.RightTrigger, rt);
            pad.SetAxisValue(Xbox360Axis.LeftThumbX, lx);
            pad.SetAxisValue(Xbox360Axis.LeftThumbY, ly);
            pad.SetAxisValue(Xbox360Axis.RightThumbX, rx);
            pad.SetAxisValue(Xbox360Axis.RightThumbY, ry);
            pad.SubmitReport();
        }
        catch (Exception) { /* el virtual se está desconectando */ }
    }

    // Stick corregido en -1..1 → eje Xbox (-32767..32767, Y hacia arriba: se invierte antes de llamar).
    private static short Axis(double v) => (short)Math.Round(Math.Clamp(v, -1, 1) * short.MaxValue);

    private static int Bit(PadButtons button) => BitOperations.TrailingZeroCount((uint)button);

    private static void OnUi(Action action) => Avalonia.Threading.Dispatcher.UIThread.Post(action);

    public void Dispose()
    {
        foreach (var route in _routes.Values)
        {
            route.Controller.InputReceived -= OnInput;
            lock (route.Sync) ReleaseKeys(route);
            DisconnectPad(route);
        }
        _routes.Clear();
        _padOrder.Clear();
        _client?.Dispose();
        _client = null;
    }
}
