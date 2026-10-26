using System.Runtime.InteropServices;

namespace PadPilot.App.Services;

/// <summary>
/// Simula teclas con SendInput. Usa códigos de escaneo (lo que leen la mayoría de los juegos);
/// las teclas multimedia van por código virtual porque no tienen un escaneo fiable.
/// </summary>
internal static class KeyboardOutput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const uint MapVkToVscEx = 4;

    public static void Down(ushort vk) => Send(vk, up: false);

    public static void Up(ushort vk) => Send(vk, up: true);

    private static void Send(ushort vk, bool up)
    {
        var input = new INPUT { type = InputKeyboard };
        bool media = vk is >= 0xA6 and <= 0xB7;

        if (media)
        {
            input.u.ki.wVk = vk;
            input.u.ki.dwFlags = up ? KeyEventKeyUp : 0;
        }
        else
        {
            uint scan = MapVirtualKey(vk, MapVkToVscEx);
            input.u.ki.wScan = (ushort)(scan & 0xFF);
            input.u.ki.dwFlags = KeyEventScanCode
                | ((scan & 0xFF00) is 0xE000 or 0xE100 ? KeyEventExtendedKey : 0)
                | (up ? KeyEventKeyUp : 0);
        }

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi; // el más grande: fija el tamaño de la unión
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);
}
