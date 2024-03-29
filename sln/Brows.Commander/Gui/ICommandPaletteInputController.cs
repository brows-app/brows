﻿using System;

namespace Brows.Gui {
    public interface ICommandPaletteInputController {
        event EventHandler Loaded;
        event EventHandler Unloaded;
        event GestureEventHandler Gesture;
        void MoveCaret(int index);
        void SelectText(int start, int length);
        void Focus();
    }
}
