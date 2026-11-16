using System.Windows.Input;

namespace Brows;

[TestFixture]
internal sealed class ConsumedInputKeyStateTest {
    [Test]
    public void TrySuppressKeyDown_TracksPhysicalKeyUntilMatchingKeyUp() {
        var state = new ConsumedInputKeyState();
        state.RecordConsumedKeyDown(Key.F12);

        var repeatIsSuppressed = state.TrySuppressKeyDown(Key.F12);
        var unrelatedKeyIsNotSuppressed = state.TrySuppressKeyDown(Key.F11);
        var keyUpIsConsumed = state.TryReleaseKeyUp(Key.F12);
        var newPressIsNotSuppressed = state.TrySuppressKeyDown(Key.F12);

        Assert.Multiple(() => {
            Assert.That(repeatIsSuppressed, Is.True);
            Assert.That(unrelatedKeyIsNotSuppressed, Is.False);
            Assert.That(keyUpIsConsumed, Is.True);
            Assert.That(newPressIsNotSuppressed, Is.False);
        });
    }

    [Test]
    public void TrySuppressTextInput_PreservesImeAndTracksThePendingKey() {
        var state = new ConsumedInputKeyState();
        state.RecordConsumedKeyDown(Key.L);

        var imeIsPreserved = state.TrySuppressTextInput(isComposingText: true);
        var keyTextIsSuppressed = state.TrySuppressTextInput(isComposingText: false);

        Assert.Multiple(() => {
            Assert.That(imeIsPreserved, Is.False);
            Assert.That(keyTextIsSuppressed, Is.True);
        });
    }

    [Test]
    public void TrySuppressTextInput_OnlyMatchingKeyUpClearsPendingText() {
        var state = new ConsumedInputKeyState();
        state.RecordConsumedKeyDown(Key.A);
        state.RecordConsumedKeyDown(Key.B);

        var firstKeyUpIsConsumed = state.TryReleaseKeyUp(Key.A);
        var secondKeyTextIsSuppressed = state.TrySuppressTextInput(isComposingText: false);

        Assert.Multiple(() => {
            Assert.That(firstKeyUpIsConsumed, Is.True);
            Assert.That(secondKeyTextIsSuppressed, Is.True);
        });
    }

    [Test]
    public void TrySuppressTextInput_SkipsNonTextKeysAndClearsForUnrelatedKeyDown() {
        var state = new ConsumedInputKeyState();
        state.RecordConsumedKeyDown(Key.F12);
        var functionKeyTextIsNotPending = state.TrySuppressTextInput(isComposingText: false);
        state.RecordConsumedKeyDown(Key.L);
        var unrelatedKeyIsNotSuppressed = state.TrySuppressKeyDown(Key.M);
        var unrelatedTextIsNotSuppressed = state.TrySuppressTextInput(isComposingText: false);

        Assert.Multiple(() => {
            Assert.That(functionKeyTextIsNotPending, Is.False);
            Assert.That(unrelatedKeyIsNotSuppressed, Is.False);
            Assert.That(unrelatedTextIsNotSuppressed, Is.False);
        });
    }
}