// Herramienta de consola para probar el núcleo sin interfaz.
// Muestra mandos conectados, batería y botones, y permite probar salida con el teclado.
using PadPilot.Core.Devices;
using PadPilot.Core.Models;
using PadPilot.Core.Protocol;
using PadPilot.Core.Services;

var profilePath = Path.Combine(Path.GetTempPath(), "PadPilot.Probe", "profiles.json");
using var manager = new ControllerManager(new ProfileStore(profilePath));

manager.ControllerConnected += c =>
{
    Console.WriteLine($"[+] {c.DisplayName} por {c.Connection} · id {c.Id} · jugador {c.Slot}");
    c.BatteryChanged += x => Console.WriteLine($"    {x.DisplayName} J{x.Slot}: batería {x.Battery.Percent} % ({x.Battery.Status})");
    // El hilo lector arranca antes de este evento: la primera lectura de batería puede haber pasado ya.
    if (c.Battery.Status != ChargeStatus.Unknown)
        Console.WriteLine($"    {c.DisplayName} J{c.Slot}: batería {c.Battery.Percent} % ({c.Battery.Status})");
    c.ButtonsPressed += (x, pressed) => Console.WriteLine($"    {x.DisplayName} J{x.Slot}: {pressed}");
};
manager.ControllerDisconnected += c => Console.WriteLine($"[-] {c.DisplayName} desconectado");

manager.Start();

Console.WriteLine("PadPilot Probe");
Console.WriteLine("  r = vibrar   c = cambiar color   t = alternar resistencia en gatillos   s = intercambiar J1/J2   q = salir");
Console.WriteLine("  g = siguiente efecto de arma (pistola, escopeta, ametralladora…)");
Console.WriteLine();

Rgb[] colors = [new(255, 0, 0), new(0, 255, 0), new(0, 64, 255), new(255, 0, 170), new(255, 255, 255)];
int colorIndex = 0;
bool triggersOn = false;

var weapons = TriggerPresets.All;
int weaponIndex = -1;

while (true)
{
    var key = Console.ReadKey(intercept: true).KeyChar;
    var controllers = manager.Controllers;

    switch (char.ToLowerInvariant(key))
    {
        case 'q':
            return;

        case 'r':
            foreach (var c in controllers) { c.Output.RumbleStrong = 200; c.Output.RumbleWeak = 200; c.ApplyOutput(); }
            Thread.Sleep(400);
            foreach (var c in controllers) { c.Output.RumbleStrong = 0; c.Output.RumbleWeak = 0; c.ApplyOutput(); }
            break;

        case 'c':
            colorIndex = (colorIndex + 1) % colors.Length;
            foreach (var c in controllers) { c.Output.Lightbar = colors[colorIndex]; c.ApplyOutput(); }
            Console.WriteLine($"    color {colors[colorIndex]}");
            break;

        case 't':
            triggersOn = !triggersOn;
            var effect = triggersOn
                ? new TriggerEffect(TriggerMode.Feedback, Position: 2, Strength: 6)
                : TriggerEffect.Off;
            foreach (var c in controllers.Where(c => c.SupportsAdaptiveTriggers))
            {
                c.Output.LeftTrigger = effect;
                c.Output.RightTrigger = effect;
                c.ApplyOutput();
            }
            Console.WriteLine($"    gatillos: {(triggersOn ? "resistencia" : "libres")}");
            break;

        case 'g':
            weaponIndex = (weaponIndex + 1) % weapons.Count;
            var weapon = weapons[weaponIndex];
            foreach (var c in controllers.Where(c => c.SupportsAdaptiveTriggers))
            {
                c.Output.LeftTrigger = weapon.Left;
                c.Output.RightTrigger = weapon.Right;
                c.ApplyOutput();
            }
            Console.WriteLine($"    [{weaponIndex + 1}/{weapons.Count}] {weapon.Name}: {weapon.Description}");
            break;

        case 's':
            var first = controllers.FirstOrDefault(c => c.Slot == 1);
            if (first is not null) manager.AssignSlot(first, 2);
            foreach (var c in manager.Controllers) Console.WriteLine($"    {c.DisplayName} → jugador {c.Slot}");
            break;
    }
}
