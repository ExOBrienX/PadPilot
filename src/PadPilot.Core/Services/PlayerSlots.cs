using PadPilot.Core.Models;

namespace PadPilot.Core.Services;

public static class PlayerSlots
{
    public const int Count = 4;

    /// <summary>Patrón de los 5 LEDs bajo el touchpad del DualSense, igual que en la PS5.</summary>
    public static byte DualSenseLeds(int slot) => slot switch
    {
        1 => 0b00100,
        2 => 0b01010,
        3 => 0b10101,
        4 => 0b11011,
        _ => 0,
    };

    /// <summary>Colores de jugador de la PS4: azul, rojo, verde, rosa. Se pueden cambiar desde la app.</summary>
    public static Rgb[] DefaultColors() =>
    [
        new(0, 64, 255),
        new(255, 0, 0),
        new(0, 255, 60),
        new(255, 0, 170),
    ];

    /// <summary>Color para un mando sin slot (más de 4 conectados).</summary>
    public static Rgb NoSlotColor => new(255, 255, 255);
}
