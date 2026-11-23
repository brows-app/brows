using Brows.Commands;
using Brows.Gui;
using Brows.Providers;
using Brows.Triggers;
using Brows.Windows;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ICommand = Brows.Commands.ICommand;
using InputEventArgs = Brows.Triggers.InputEventArgs;

namespace Brows;

[TestFixture]
[NonParallelizable]
internal sealed class CommanderControlTest {
    private static TestWindow ShowWindow(UIElement content, CommanderControl commanderControl) {
        var root = new Grid();
        root.Children.Add(commanderControl);
        root.Children.Add(content);

        var window = new TestWindow {
            Content = root,
            Height = 160,
            Left = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 320
        };
        window.Show();
        window.UpdateLayout();
        DispatcherTools.Pump(window.Dispatcher);
        return window;
    }

    private static KeyEventArgs RaiseKey(
        UIElement source,
        RoutedEvent routedEvent,
        Key key,
        int timestamp,
        bool handled = false) {
        var args = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(source),
            timestamp,
            key) {
            RoutedEvent = routedEvent,
            Handled = handled,
            Source = source
        };
        source.RaiseEvent(args);
        return args;
    }

    private static TextCompositionEventArgs RaiseTextInput(
        TextBox source,
        string text,
        TextCompositionAutoComplete autoComplete = TextCompositionAutoComplete.On) {
        var composition = new TextComposition(InputManager.Current, source, text, autoComplete);
        var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
            Source = source
        };
        source.RaiseEvent(args);
        return args;
    }

    [Test]
    public void ResolveKeyboardSource_UsesContentAncestorsAndHonorsOwnerBoundaries() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var scopedTextBlock = new TextBlock();
            var scopedRun = new Run("scoped");
            scopedTextBlock.Inlines.Add(scopedRun);
            ownedRoot.Children.Add(scopedTextBlock);

            var clearedRoot = new Grid();
            InputOwner.SetOwner(clearedRoot, null);
            var clearedTextBlock = new TextBlock();
            var clearedRun = new Run("cleared");
            clearedTextBlock.Inlines.Add(clearedRun);
            clearedRoot.Children.Add(clearedTextBlock);
            ownedRoot.Children.Add(clearedRoot);

            var ordinaryDataContext = new object();
            var ordinarySource = new TextBox {
                DataContext = ordinaryDataContext
            };
            var detachedDataContext = new object();
            var detachedClear = new TextBox {
                DataContext = detachedDataContext
            };
            InputOwner.SetOwner(detachedClear, null);
            var content = new Grid();
            content.Children.Add(ownedRoot);
            content.Children.Add(ordinarySource);

            var commanderControl = new CommanderControl();
            var window = ShowWindow(content, commanderControl);
            try {
                var scopedSource = CommanderControl.ResolveKeyboardSource(scopedRun, null, window);
                var clearedSource = CommanderControl.ResolveKeyboardSource(clearedRun, null, window);
                var ordinaryResolvedSource = CommanderControl.ResolveKeyboardSource(
                    ordinarySource,
                    scopedTextBlock,
                    window);
                var detachedClearedSource = CommanderControl.ResolveKeyboardSource(
                    detachedClear,
                    scopedTextBlock,
                    window);

                Assert.Multiple(() => {
                    Assert.That(scopedSource, Is.SameAs(owner));
                    Assert.That(clearedSource, Is.Null);
                    Assert.That(ordinaryResolvedSource, Is.SameAs(ordinaryDataContext));
                    Assert.That(detachedClearedSource, Is.SameAs(detachedDataContext));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void ResolveKeyboardSource_UsesFocusedOwnerForMissingOrRootSourceWithinWindow() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var focused = new TextBox();
            ownedRoot.Children.Add(focused);
            var window = ShowWindow(ownedRoot, new CommanderControl());

            var otherOwner = new TestInputOwner();
            var otherRoot = new Grid();
            InputOwner.SetOwner(otherRoot, otherOwner);
            var otherFocused = new TextBox();
            otherRoot.Children.Add(otherFocused);
            var otherWindow = ShowWindow(otherRoot, new CommanderControl());

            try {
                var missingSource = CommanderControl.ResolveKeyboardSource(null, focused, window);
                var rootSource = CommanderControl.ResolveKeyboardSource(window, focused, window);
                var foreignFocus = CommanderControl.ResolveKeyboardSource(null, otherFocused, window);

                Assert.Multiple(() => {
                    Assert.That(missingSource, Is.SameAs(owner));
                    Assert.That(rootSource, Is.SameAs(owner));
                    Assert.That(foreignFocus, Is.Null);
                });
            }
            finally {
                otherWindow.Close();
                window.Close();
            }
        });
    }

    [Test]
    public void PreviewKeyDown_TracksRemappedConfiguredKeyAndConsumesRepeatsAndHandledKeyUp() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var scopedSource = new TextBox();
            ownedRoot.Children.Add(scopedSource);

            var ordinaryDataContext = new object();
            var ordinarySource = new TextBox {
                DataContext = ordinaryDataContext
            };
            var content = new Grid();
            content.Children.Add(ownedRoot);
            content.Children.Add(ordinarySource);

            var configuredGestureKey = PressKey.F12;
            var commanderControl = new CommanderControl();
            var received = new List<GestureEventArgs>();
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                received.Add(args);
                var gestureMatchesBinding = args.Gesture is PressGesture pressGesture &&
                    pressGesture.Key == configuredGestureKey;
                args.Triggered = args.Source == owner && gestureMatchesBinding;
            };
            var window = ShowWindow(content, commanderControl);
            try {
                var firstDown = RaiseKey(
                    scopedSource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 1);
                configuredGestureKey = PressKey.F6;
                Keyboard.Focus(ordinarySource);
                var repeatedOldKey = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 2);
                var handledOldKeyUp = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyUpEvent,
                    Key.F12,
                    timestamp: 3,
                    handled: true);
                var oldKeyAfterRemap = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 4);
                var remappedDown = RaiseKey(
                    scopedSource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F6,
                    timestamp: 5);
                var repeatedRemappedKey = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F6,
                    timestamp: 6);
                var handledRemappedKeyUp = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyUpEvent,
                    Key.F6,
                    timestamp: 7,
                    handled: true);
                var remappedKeyAfterKeyUp = RaiseKey(
                    scopedSource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F6,
                    timestamp: 8);

                Assert.Multiple(() => {
                    Assert.That(firstDown.Handled, Is.True);
                    Assert.That(repeatedOldKey.Handled, Is.True);
                    Assert.That(handledOldKeyUp.Handled, Is.True);
                    Assert.That(oldKeyAfterRemap.Handled, Is.False);
                    Assert.That(remappedDown.Handled, Is.True);
                    Assert.That(repeatedRemappedKey.Handled, Is.True);
                    Assert.That(handledRemappedKeyUp.Handled, Is.True);
                    Assert.That(remappedKeyAfterKeyUp.Handled, Is.True);
                    Assert.That(received.Count, Is.EqualTo(4));
                    Assert.That(received[0].Source, Is.SameAs(owner));
                    Assert.That(((PressGesture)received[0].Gesture).Key, Is.EqualTo(PressKey.F12));
                    Assert.That(received[1].Source, Is.SameAs(ordinaryDataContext));
                    Assert.That(((PressGesture)received[2].Gesture).Key, Is.EqualTo(PressKey.F6));
                    Assert.That(received[2].Source, Is.SameAs(owner));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void PreviewKeyDown_PreservesEventsAlreadyHandledByAnotherRoute() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var source = new TextBox();
            ownedRoot.Children.Add(source);
            var commanderControl = new CommanderControl();
            var gestureCount = 0;
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                gestureCount++;
                args.Triggered = true;
            };
            var window = ShowWindow(ownedRoot, commanderControl);
            try {
                var keyDown = RaiseKey(
                    source,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 1,
                    handled: true);

                Assert.Multiple(() => {
                    Assert.That(keyDown.Handled, Is.True);
                    Assert.That(gestureCount, Is.Zero);
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void UnloadedControl_WindowSwitchClearsConsumedPhysicalKeyStateAndOldHandler() {
        DispatcherTools.Run(dispatcher => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var scopedSource = new TextBox();
            ownedRoot.Children.Add(scopedSource);
            var ordinarySource = new TextBox();
            var firstContent = new Grid();
            firstContent.Children.Add(ownedRoot);
            firstContent.Children.Add(ordinarySource);

            var commanderControl = new CommanderControl();
            var received = new List<GestureEventArgs>();
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                received.Add(args);
                args.Triggered = args.Source == owner;
            };
            var firstWindow = ShowWindow(firstContent, commanderControl);
            TestWindow secondWindow = null;
            try {
                var firstDown = RaiseKey(
                    scopedSource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 1);
                var firstWindowRoot = (Grid)firstWindow.Content;
                firstWindowRoot.Children.Remove(commanderControl);
                firstWindow.UpdateLayout();
                DispatcherTools.Pump(dispatcher);

                var secondSource = new TextBox();
                var secondContent = new Grid();
                secondContent.Children.Add(secondSource);
                secondWindow = ShowWindow(secondContent, commanderControl);
                var eventOnOldWindow = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 2);
                var eventOnNewWindow = RaiseKey(
                    secondSource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 3);

                Assert.Multiple(() => {
                    Assert.That(firstDown.Handled, Is.True);
                    Assert.That(eventOnOldWindow.Handled, Is.False);
                    Assert.That(eventOnNewWindow.Handled, Is.False);
                    Assert.That(received.Count, Is.EqualTo(2));
                });
            }
            finally {
                secondWindow?.Close();
                firstWindow.Close();
            }
        });
    }

    [Test]
    public void WindowDeactivated_ClearsConsumedPhysicalKeyState() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var scopedSource = new TextBox();
            ownedRoot.Children.Add(scopedSource);
            var ordinarySource = new TextBox();
            var content = new Grid();
            content.Children.Add(ownedRoot);
            content.Children.Add(ordinarySource);

            var commanderControl = new CommanderControl();
            var received = new List<GestureEventArgs>();
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                received.Add(args);
                args.Triggered = args.Source == owner;
            };
            var window = ShowWindow(content, commanderControl);
            try {
                var firstDown = RaiseKey(
                    scopedSource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 1);
                window.RaiseDeactivated();
                var afterDeactivation = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 2);

                Assert.Multiple(() => {
                    Assert.That(firstDown.Handled, Is.True);
                    Assert.That(afterDeactivation.Handled, Is.False);
                    Assert.That(received.Count, Is.EqualTo(2));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void PreviewKeyDown_WhenScopeDeactivatesDuringDispatchDoesNotRestoreReservation() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var scopedSource = new TextBox();
            ownedRoot.Children.Add(scopedSource);
            var ordinarySource = new TextBox();
            var content = new Grid();
            content.Children.Add(ownedRoot);
            content.Children.Add(ordinarySource);

            TestWindow window = null;
            var commanderControl = new CommanderControl();
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                if (args.Source == owner) {
                    window.RaiseDeactivated();
                    args.Triggered = true;
                }
            };
            window = ShowWindow(content, commanderControl);
            try {
                var firstDown = RaiseKey(
                    scopedSource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 1);
                var secondDown = RaiseKey(
                    ordinarySource,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 2);

                Assert.Multiple(() => {
                    Assert.That(firstDown.Handled, Is.True);
                    Assert.That(secondDown.Handled, Is.False);
                });
            }
            finally {
                window.Close();
            }
        });
    }
    [Test]
    public void PreviewTextInputAndSpace_UseTheSameResolvedOwner() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var source = new TextBox();
            ownedRoot.Children.Add(source);

            var commanderControl = new CommanderControl();
            var received = new List<InputEventArgs>();
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                Assert.That(args.Source, Is.SameAs(owner));
            };
            ((ICommanderController)commanderControl).WindowInput += (_, args) => {
                received.Add(args);
                args.Triggered = true;
            };
            var window = ShowWindow(ownedRoot, commanderControl);
            try {
                var space = RaiseKey(
                    source,
                    Keyboard.PreviewKeyDownEvent,
                    Key.Space,
                    timestamp: 1);
                var text = RaiseTextInput(source, "かな");

                Assert.Multiple(() => {
                    Assert.That(space.Handled, Is.True);
                    Assert.That(text.Handled, Is.True);
                    Assert.That(received.Count, Is.EqualTo(2));
                    Assert.That(received[0].Text, Is.EqualTo(" "));
                    Assert.That(received[0].Source, Is.SameAs(owner));
                    Assert.That(received[1].Text, Is.EqualTo("かな"));
                    Assert.That(received[1].Source, Is.SameAs(owner));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void PreviewTextInput_AfterConsumedFunctionKeyPassesNormalText() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var source = new TextBox();
            ownedRoot.Children.Add(source);

            var commanderControl = new CommanderControl();
            var received = new List<InputEventArgs>();
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                args.Triggered = args.Source == owner;
            };
            ((ICommanderController)commanderControl).WindowInput += (_, args) => {
                received.Add(args);
                args.Triggered = true;
            };
            var window = ShowWindow(ownedRoot, commanderControl);
            try {
                var functionKey = RaiseKey(
                    source,
                    Keyboard.PreviewKeyDownEvent,
                    Key.F12,
                    timestamp: 1);
                var text = RaiseTextInput(source, "z");
                var functionKeyUp = RaiseKey(
                    source,
                    Keyboard.PreviewKeyUpEvent,
                    Key.F12,
                    timestamp: 2);

                Assert.Multiple(() => {
                    Assert.That(functionKey.Handled, Is.True);
                    Assert.That(text.Handled, Is.True);
                    Assert.That(functionKeyUp.Handled, Is.True);
                    Assert.That(received.Count, Is.EqualTo(1));
                    Assert.That(received[0].Text, Is.EqualTo("z"));
                    Assert.That(received[0].Source, Is.SameAs(owner));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void PreviewTextInput_SuppressesConsumedKeyTextAndPreservesImeAndUnrelatedText() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var ownedRoot = new Grid();
            InputOwner.SetOwner(ownedRoot, owner);
            var source = new TextBox();
            ownedRoot.Children.Add(source);

            var commanderControl = new CommanderControl();
            var received = new List<InputEventArgs>();
            ((ICommanderController)commanderControl).WindowGesture += (_, args) => {
                var gestureIsTextRelease = args.Gesture is PressGesture pressGesture && pressGesture.Key == PressKey.L;
                args.Triggered = args.Source == owner && gestureIsTextRelease;
            };
            ((ICommanderController)commanderControl).WindowInput += (_, args) => {
                received.Add(args);
                args.Triggered = true;
            };
            var window = ShowWindow(ownedRoot, commanderControl);
            try {
                var consumedDown = RaiseKey(
                    source,
                    Keyboard.PreviewKeyDownEvent,
                    Key.L,
                    timestamp: 1);
                var releaseText = RaiseTextInput(source, "l");
                var consumedUp = RaiseKey(
                    source,
                    Keyboard.PreviewKeyUpEvent,
                    Key.L,
                    timestamp: 2);

                var imeDown = RaiseKey(
                    source,
                    Keyboard.PreviewKeyDownEvent,
                    Key.L,
                    timestamp: 3);
                var imeText = RaiseTextInput(
                    source,
                    "かな",
                    TextCompositionAutoComplete.Off);
                var imeUp = RaiseKey(
                    source,
                    Keyboard.PreviewKeyUpEvent,
                    Key.L,
                    timestamp: 4);

                var unrelatedDown = RaiseKey(
                    source,
                    Keyboard.PreviewKeyDownEvent,
                    Key.L,
                    timestamp: 5);
                var unrelatedKey = RaiseKey(
                    source,
                    Keyboard.PreviewKeyDownEvent,
                    Key.M,
                    timestamp: 6);
                var unrelatedText = RaiseTextInput(source, "m");

                Assert.Multiple(() => {
                    Assert.That(consumedDown.Handled, Is.True);
                    Assert.That(releaseText.Handled, Is.True);
                    Assert.That(consumedUp.Handled, Is.True);
                    Assert.That(imeDown.Handled, Is.True);
                    Assert.That(imeText.Handled, Is.True);
                    Assert.That(imeUp.Handled, Is.True);
                    Assert.That(unrelatedDown.Handled, Is.True);
                    Assert.That(unrelatedKey.Handled, Is.False);
                    Assert.That(unrelatedText.Handled, Is.True);
                    Assert.That(received.Count, Is.EqualTo(2));
                    Assert.That(received[0].Text, Is.EqualTo("かな"));
                    Assert.That(received[0].Source, Is.SameAs(owner));
                    Assert.That(received[1].Text, Is.EqualTo("m"));
                    Assert.That(received[1].Source, Is.SameAs(owner));
                });
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void ResolveKeyboardSource_HonorsStyledNullOwnerBoundary() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var source = new TextBox {
                Style = new Style(typeof(TextBox)) {
                    Setters = {
                        new Setter(InputOwner.OwnerProperty, null)
                    }
                }
            };
            var root = new Grid();
            InputOwner.SetOwner(root, owner);
            root.Children.Add(source);
            var window = ShowWindow(root, new CommanderControl());
            try {
                Assert.That(InputOwner.GetOwner(source), Is.Null);
                Assert.That(CommanderControl.ResolveKeyboardSource(source, source, window), Is.Null);
            }
            finally {
                window.Close();
            }
        });
    }
    [Test]
    public void ResolveKeyboardSource_ConnectedElementsDoNotAllocatePerLookup() {
        DispatcherTools.Run(_ => {
            var owner = new TestInputOwner();
            var owned = new TextBox();
            var ownedRoot = new Grid();
            ownedRoot.Children.Add(owned);
            InputOwner.SetOwner(ownedRoot, owner);
            var ordinaryContext = new object();
            var ordinary = new TextBox { DataContext = ordinaryContext };
            var content = new Grid();
            content.Children.Add(ownedRoot);
            content.Children.Add(ordinary);
            var window = ShowWindow(content, new CommanderControl());
            try {
                for (var i = 0; i < 100; i++) {
                    CommanderControl.ResolveKeyboardSource(owned, owned, window);
                    CommanderControl.ResolveKeyboardSource(ordinary, owned, window);
                }
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 1000; i++) {
                    CommanderControl.ResolveKeyboardSource(owned, owned, window);
                    CommanderControl.ResolveKeyboardSource(ordinary, owned, window);
                }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(allocated, Is.Zero);
                Assert.That(CommanderControl.ResolveKeyboardSource(owned, owned, window), Is.SameAs(owner));
                Assert.That(CommanderControl.ResolveKeyboardSource(ordinary, owned, window),
                    Is.SameAs(ordinaryContext));
            }
            finally {
                window.Close();
            }
        });
    }

    [Test]
    public void ResolveKeyboardSource_ObservesOwnerChangesAndReparentingImmediately() {
        DispatcherTools.Run(_ => {
            var firstOwner = new TestInputOwner();
            var replacementOwner = new TestInputOwner();
            var secondOwner = new TestInputOwner();
            var source = new TextBox();
            var firstRoot = new Grid();
            var secondRoot = new Grid();
            InputOwner.SetOwner(firstRoot, firstOwner);
            InputOwner.SetOwner(secondRoot, secondOwner);
            firstRoot.Children.Add(source);
            var firstWindow = ShowWindow(firstRoot, new CommanderControl());
            var secondWindow = ShowWindow(secondRoot, new CommanderControl());
            try {
                Assert.That(CommanderControl.ResolveKeyboardSource(source, source, firstWindow),
                    Is.SameAs(firstOwner));
                InputOwner.SetOwner(firstRoot, replacementOwner);
                Assert.That(CommanderControl.ResolveKeyboardSource(source, source, firstWindow),
                    Is.SameAs(replacementOwner));
                InputOwner.SetOwner(source, null);
                Assert.That(CommanderControl.ResolveKeyboardSource(source, source, firstWindow), Is.Null);
                source.ClearValue(InputOwner.OwnerProperty);
                firstRoot.Children.Remove(source);
                secondRoot.Children.Add(source);
                secondWindow.UpdateLayout();
                Assert.That(CommanderControl.ResolveKeyboardSource(source, source, secondWindow),
                    Is.SameAs(secondOwner));
            }
            finally {
                secondWindow.Close();
                firstWindow.Close();
            }
        });
    }
    [Test]
    public void WindowClosing_WaitsForCleanupAndCoalescesRepeatedRequests() {
        DispatcherTools.Run(dispatcher => {
            var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var model = new Mock<ICommander>();
            var lifetime = model.As<IAsyncDisposable>();
            lifetime.Setup(item => item.DisposeAsync()).Returns(() => new ValueTask(cleanup.Task));
            var control = new CommanderControl { ViewModel = model.Object };
            var window = ShowWindow(new Grid(), control);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.Close();
            DispatcherTools.Pump(dispatcher);
            window.Close();
            DispatcherTools.Pump(dispatcher);
            Assert.That(closed, Is.False);
            lifetime.Verify(item => item.DisposeAsync(), Times.Once);
            cleanup.SetResult();
            DispatcherTools.Pump(dispatcher);
            Assert.That(closed, Is.True);
        });
    }

    [Test]
    public void WindowClosing_FailedCleanupKeepsWindowOpenAndAllowsRetry() {
        DispatcherTools.Run(dispatcher => {
            var model = new Mock<ICommander>();
            var lifetime = model.As<IAsyncDisposable>();
            var attempts = 0;
            lifetime.Setup(item => item.DisposeAsync()).Returns(() => {
                attempts++;
                return attempts == 1
                    ? new ValueTask(Task.FromException(new InvalidOperationException("cleanup")))
                    : ValueTask.CompletedTask;
            });
            var control = new CommanderControl { ViewModel = model.Object };
            var window = ShowWindow(new Grid(), control);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.Close();
            DispatcherTools.Pump(dispatcher);
            Assert.That(closed, Is.False);
            window.Close();
            DispatcherTools.Pump(dispatcher);
            Assert.That(closed, Is.True);
            Assert.That(attempts, Is.EqualTo(2));
        });
    }
    [Test]
    public void WindowClosing_DelayedCleanupCannotCloseReattachedWindow() {
        DispatcherTools.Run(dispatcher => {
            var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstModel = new Mock<ICommander>();
            firstModel.As<IAsyncDisposable>().Setup(item => item.DisposeAsync())
                .Returns(() => new ValueTask(cleanup.Task));
            var control = new CommanderControl { ViewModel = firstModel.Object };
            var first = ShowWindow(new Grid(), control);
            var second = new TestWindow {
                Height = 160,
                Width = 320,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            var firstClosed = false;
            var secondClosed = false;
            first.Closed += (_, _) => firstClosed = true;
            second.Closed += (_, _) => secondClosed = true;
            try {
                first.Close();
                DispatcherTools.Pump(dispatcher);
                var root = (Grid)first.Content;
                root.Children.Remove(control);
                DispatcherTools.Pump(dispatcher);
                control.ViewModel = Mock.Of<ICommander>();
                second.Content = control;
                second.Show();
                DispatcherTools.Pump(dispatcher);
                cleanup.SetResult();
                DispatcherTools.Pump(dispatcher);
                Assert.That(firstClosed, Is.False);
                Assert.That(secondClosed, Is.False);
            }
            finally {
                second.Close();
                first.Close();
            }
        });
    }
    private sealed class TestWindow : Window {
        internal void RaiseDeactivated() {
            OnDeactivated(EventArgs.Empty);
        }
    }

    private sealed class TestInputOwner : IControllingCommandGesture, IControllingText {
        public IProvider Provider => null;

        public bool Allows(ICommand command) {
            return true;
        }
    }
}
