using System;

namespace Brows.Triggers;

[Flags]
public enum PressModifiers {
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}
