using System;

namespace Brows.Triggers;

[Flags]
public enum ClickModifiers {
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}
