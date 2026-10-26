using PadPilot.Core.Models;

namespace PadPilot.Core.Devices;

public static class DeviceIds
{
    public const int SonyVendorId = 0x054C;

    public static ControllerKind? Identify(int productId) => productId switch
    {
        0x0CE6 => ControllerKind.DualSense,
        0x0DF2 => ControllerKind.DualSenseEdge,
        0x05C4 or 0x09CC => ControllerKind.DualShock4, // v1 y v2
        0x0BA0 => ControllerKind.DualShock4,           // adaptador inalámbrico USB de Sony
        _ => null,
    };
}
