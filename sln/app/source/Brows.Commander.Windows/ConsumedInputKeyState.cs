using System.Collections.Generic;
using System.Windows.Input;

namespace Brows;

internal sealed class ConsumedInputKeyState {
    private readonly HashSet<Key> _ConsumedKeys = [];
    private Key? _TextInputPendingKey;

    private static bool MayProduceText(Key key) {
        var keyIsAlphanumeric = key is >= Key.D0 and <= Key.Z;
        var keyIsNumericPad = key is >= Key.NumPad0 and <= Key.Divide;
        var keyIsOem = key is >= Key.Oem1 and <= Key.Oem102;
        var keyIsControlText = key is Key.Back or Key.Tab or Key.Enter or Key.Space;
        var keyIsCompositionKey = key is Key.ImeProcessed or Key.DeadCharProcessed or Key.OemClear;
        var keyMayProduceText =
            keyIsAlphanumeric || keyIsNumericPad || keyIsOem || keyIsControlText || keyIsCompositionKey;
        return keyMayProduceText;
    }

    internal long Generation { get; private set; }

    internal bool TrySuppressKeyDown(Key key) {
        if (_ConsumedKeys.Contains(key)) {
            _TextInputPendingKey = MayProduceText(key) ? key : null;
            return true;
        }
        _TextInputPendingKey = null;
        return false;
    }

    internal void RecordConsumedKeyDown(Key key) {
        _ConsumedKeys.Add(key);
        _TextInputPendingKey = MayProduceText(key) ? key : null;
    }

    internal bool TryReleaseKeyUp(Key key) {
        var consumed = _ConsumedKeys.Remove(key);
        var releasedKeyOwnsPendingText = _TextInputPendingKey == key;
        if (releasedKeyOwnsPendingText) {
            _TextInputPendingKey = null;
        }
        return consumed;
    }

    /*
     * WPF text events have no physical-key identity. Reserve only the next automatic
     * text event in the consumed key sequence; leave multi-step compositions alone.
     */
    internal bool TrySuppressTextInput(bool isComposingText) {
        if (isComposingText) {
            return false;
        }
        var shouldSuppress = _TextInputPendingKey is not null;
        _TextInputPendingKey = null;
        return shouldSuppress;
    }

    internal void Clear() {
        _ConsumedKeys.Clear();
        _TextInputPendingKey = null;
        Generation++;
    }
}