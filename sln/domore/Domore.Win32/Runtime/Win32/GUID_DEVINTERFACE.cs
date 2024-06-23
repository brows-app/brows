using System;

namespace Domore.Runtime.Win32 {
    public static class GUID_DEVINTERFACE {
        public static readonly Guid USB_DEVICE = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        public static readonly Guid USB_HOST_CONTROLLER = new("3ABF6F2D-71C4-462A-8A92-1E6861E6AF27");
        public static readonly Guid USB_HUB = new("F18A0E88-C30C-11D0-8815-00A0C906BED8");
    }
}
