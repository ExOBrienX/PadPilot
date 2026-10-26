using PadPilot.Core.Models;

namespace PadPilot.Core.Protocol;

/// <summary>DualSense y DS4 comparten la misma distribución de los 3 bytes de botones.</summary>
internal static class ButtonParser
{
    private static readonly PadButtons[] Hat =
    [
        PadButtons.DPadUp,
        PadButtons.DPadUp | PadButtons.DPadRight,
        PadButtons.DPadRight,
        PadButtons.DPadRight | PadButtons.DPadDown,
        PadButtons.DPadDown,
        PadButtons.DPadDown | PadButtons.DPadLeft,
        PadButtons.DPadLeft,
        PadButtons.DPadLeft | PadButtons.DPadUp,
    ];

    private static readonly PadButtons[] Byte1 =
    [
        PadButtons.L1, PadButtons.R1, PadButtons.L2, PadButtons.R2,
        PadButtons.Share, PadButtons.Options, PadButtons.L3, PadButtons.R3,
    ];

    public static PadButtons Parse(byte b0, byte b1, byte b2, bool hasMute)
    {
        var result = PadButtons.None;

        int hat = b0 & 0x0F;          // 8 = cruceta suelta
        if (hat < 8) result |= Hat[hat];
        if ((b0 & 0x10) != 0) result |= PadButtons.Square;
        if ((b0 & 0x20) != 0) result |= PadButtons.Cross;
        if ((b0 & 0x40) != 0) result |= PadButtons.Circle;
        if ((b0 & 0x80) != 0) result |= PadButtons.Triangle;

        for (int i = 0; i < 8; i++)
            if ((b1 & (1 << i)) != 0) result |= Byte1[i];

        if ((b2 & 0x01) != 0) result |= PadButtons.PS;
        if ((b2 & 0x02) != 0) result |= PadButtons.Touchpad;
        // En el DS4 los bits altos de b2 son un contador, no botones.
        if (hasMute && (b2 & 0x04) != 0) result |= PadButtons.Mute;

        return result;
    }
}
