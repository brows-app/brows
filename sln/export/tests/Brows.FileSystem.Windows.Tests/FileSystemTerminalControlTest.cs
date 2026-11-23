using Brows.Commands;
using Brows.Composition;
using Brows.Conf;
using Brows.Detail;
using Brows.Entries;
using Brows.Panels;
using Brows.Providers;
using Brows.Terminal;
using Brows.Triggers;
using Brows.Windows;
using Brows.Windows.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TERMINAL = Brows.Commands.Terminal;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class FileSystemTerminalControlTest {
    private static void Complete(Task task, Dispatcher dispatcher) {
        if (task.IsCompleted == false) {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timeout.Tick += (_, _) => frame.Continue = false;
            timeout.Start();
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
            Dispatcher.PushFrame(frame);
            timeout.Stop();
        }
        Assert.That(task.IsCompleted, Is.True, "The terminal host did not complete cleanup.");
        task.GetAwaiter().GetResult();
    }

    private static string[] ProbeTerminal(TerminalEmulator terminal, Dispatcher dispatcher, int sequence) {
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var titleProperty = DependencyPropertyDescriptor.FromProperty(
            TerminalEmulator.TitleProperty, typeof(TerminalEmulator));
        var prefix = sequence.ToString(CultureInfo.InvariantCulture) + "|";
        EventHandler titleChanged = (_, _) => {
            if (terminal.Title.StartsWith(prefix, StringComparison.Ordinal)) {
                reported.TrySetResult();
            }
        };
        titleProperty.AddValueChanged(terminal, titleChanged);
        try {
            terminal.Paste($"Write-Output 'BROWS-PHASE3-OK'; [Console]::Write([char]27+']0;{sequence}|'+" +
                "(Get-Location).ProviderPath+'|'+$PID+[char]7)");
            terminal.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(terminal), Environment.TickCount, Key.Enter) {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            Complete(reported.Task, dispatcher);
            return terminal.Title.Split('|');
        }
        finally {
            titleProperty.RemoveValueChanged(terminal, titleChanged);
        }
    }

    private static IGestureTrigger PressGestureTrigger(Func<string> display, string defined) {
        var press = new Mock<IGestureTrigger>();
        press.Setup(item => item.Gesture).Returns(new PressGesture(PressKey.F12));
        press.Setup(item => item.Display).Returns(display);
        press.Setup(item => item.Defined).Returns(defined);
        return press.Object;
    }

    private static Mock<ICommandTrigger> PressTrigger(Command command, Func<string> display) {
        return PressTrigger(command, PressGestureTrigger(display, "terminal"));
    }

    private static Mock<ICommandTrigger> PressTrigger(Command command, params IGestureTrigger[] gestures) {
        var trigger = new Mock<ICommandTrigger>();
        trigger.Setup(item => item.PressGestures()).Returns(() => gestures);
        /*
         * A command's trigger is otherwise loaded from trigger.conf when the command is initialized.
         */
        typeof(Command).GetProperty(nameof(Command.Trigger)).SetValue(command, trigger.Object);
        return trigger;
    }

    private static void RaiseTriggerChanged(Command command) {
        var field = typeof(Command).GetField(
            nameof(Command.TriggerChanged), BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = (EventHandler)field.GetValue(command);
        handler?.Invoke(command, EventArgs.Empty);
    }

    [Test]
    public void Trigger_ShowsFocusDescriptionWhenTerminalDoesNotHaveFocus() {
        DispatcherTools.Run(dispatcher => {
            var command = new TERMINAL();
            PressTrigger(command, () => "CTRL+SHIFT+F12");
            using var host = new HostFixture(dispatcher, terminalCommand: command);
            Assert.That(host.Control.TerminalView.IsKeyboardFocusWithin, Is.False);
            Assert.That(host.Control.Trigger.Text, Is.EqualTo("CTRL+SHIFT+F12"));
            Assert.That(host.Control.TriggerDescription.Text,
                Is.EqualTo(FileSystemText.Value(host.Control.Translation, "FileSystemTerminal_Trigger_Focus")));
        });
    }

    [Test]
    public void Trigger_ShowsShortcutWithoutArgumentsWhenCloseShortcutComesFirst() {
        DispatcherTools.Run(dispatcher => {
            var command = new TERMINAL();
            PressTrigger(command,
                PressGestureTrigger(() => "CTRL+SHIFT+ALT+F12", "terminal close"),
                PressGestureTrigger(() => "CTRL+SHIFT+F11", "terminal"));
            using var host = new HostFixture(dispatcher, terminalCommand: command);
            Assert.That(host.Control.Trigger.Text, Is.EqualTo("CTRL+SHIFT+F11"));
            Assert.That(host.Control.TriggerDescription.Text,
                Is.EqualTo(FileSystemText.Value(host.Control.Translation, "FileSystemTerminal_Trigger_Focus")));
        });
    }

    [Test]
    public void TriggerDescription_IsEmptyWhenCommandHasOnlyShortcutsWithArguments() {
        DispatcherTools.Run(dispatcher => {
            var command = new TERMINAL();
            PressTrigger(command, PressGestureTrigger(() => "CTRL+SHIFT+ALT+F12", "terminal close"));
            using var host = new HostFixture(dispatcher, terminalCommand: command);
            Assert.That(host.Control.Trigger.Text, Is.Null.Or.Empty);
            Assert.That(host.Control.TriggerDescription.Text, Is.Empty);
        });
    }

    [Test]
    public void TriggerDescription_IsEmptyWhenCommandHasNoShortcut() {
        DispatcherTools.Run(dispatcher => {
            var command = new TERMINAL();
            using var host = new HostFixture(dispatcher, terminalCommand: command);
            host.Window.Activate();
            Assert.That(host.Control.TerminalView.Focus(), Is.True);
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Control.Trigger.Text, Is.Null.Or.Empty);
            Assert.That(host.Control.TriggerDescription.Text, Is.Empty);
        });
    }

    [Test]
    public void Trigger_ShowsReleaseDescriptionWhileTerminalHasFocus() {
        DispatcherTools.Run(dispatcher => {
            var command = new TERMINAL();
            PressTrigger(command, () => "CTRL+SHIFT+F12");
            using var host = new HostFixture(dispatcher, terminalCommand: command);
            host.Window.Activate();
            Assert.That(host.Control.TerminalView.Focus(), Is.True);
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Control.Trigger.Text, Is.EqualTo("CTRL+SHIFT+F12"));
            Assert.That(host.Control.TriggerDescription.Text,
                Is.EqualTo(FileSystemText.Value(host.Control.Translation, "FileSystemTerminal_Trigger_Release")));
            Assert.That(host.Anchor.Focus(), Is.True);
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Control.Trigger.Text, Is.EqualTo("CTRL+SHIFT+F12"));
            Assert.That(host.Control.TriggerDescription.Text,
                Is.EqualTo(FileSystemText.Value(host.Control.Translation, "FileSystemTerminal_Trigger_Focus")));
        });
    }

    [Test]
    public void Trigger_IsNotUpdatedRepeatedlyWhileTerminalKeepsFocus() {
        DispatcherTools.Run(dispatcher => {
            var command = new TERMINAL();
            var trigger = PressTrigger(command, () => "CTRL+SHIFT+F12");
            using var host = new HostFixture(dispatcher, terminalCommand: command);
            var unfocusedUpdates = trigger.Invocations.Count;
            host.Window.Activate();
            Assert.That(host.Control.TerminalView.Focus(), Is.True);
            DispatcherTools.Pump(dispatcher);
            var focusedUpdates = trigger.Invocations.Count;
            Assert.That(focusedUpdates, Is.EqualTo(unfocusedUpdates + 1));
            var settled = Task.Delay(TimeSpan.FromMilliseconds(500));
            Complete(settled, dispatcher);
            DispatcherTools.Pump(dispatcher);
            Assert.That(trigger.Invocations, Has.Count.EqualTo(focusedUpdates));
            Assert.That(host.Control.TriggerDescription.Text,
                Is.EqualTo(FileSystemText.Value(host.Control.Translation, "FileSystemTerminal_Trigger_Release")));
        });
    }

    [Test]
    public void Trigger_UpdatesWhenCommandTriggerChanges() {
        DispatcherTools.Run(dispatcher => {
            var display = "CTRL+SHIFT+F12";
            var command = new TERMINAL();
            PressTrigger(command, () => display);
            using var host = new HostFixture(dispatcher, terminalCommand: command);
            display = "ALT+F12";
            RaiseTriggerChanged(command);
            Assert.That(host.Control.Trigger.Text, Is.EqualTo("ALT+F12"));
        });
    }

    [Test]
    public void Constructor_DisablesAutomaticStartupAndScopesInputToTerminal() {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            Assert.That(host.Control.TerminalView.AutoStart, Is.False);
            Assert.That(host.Control.TerminalView.IsRunning, Is.False);
            Assert.That(InputOwner.GetOwner(host.Control.TerminalView), Is.SameAs(host.Detail));
            Assert.That(InputOwner.GetOwner(host.Control.Status), Is.Null);
            Assert.That(InputOwner.GetOwner(host.Anchor), Is.Null);
            Assert.That(host.Detail.CurrentController, Is.SameAs(host.Control));
        });
    }

    [Test]
    public void FeatureResources_ApplyImplicitTerminalTemplate() {
        DispatcherTools.Run(dispatcher => {
            var control = new FileSystemTerminalControl();
            control.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.FileSystem.Windows;component/Resource/Resource.xaml", UriKind.Relative)
            });
            control.ApplyTemplate();
            Assert.That(control.TerminalView, Is.InstanceOf<TerminalEmulator>());
            Assert.That(control.Status, Is.InstanceOf<TextBlock>());
            Assert.That(control.TerminalView.AutoStart, Is.False);
            Complete(control.CloseAsync(CancellationToken.None), dispatcher);
        });
    }

    [TestCase("Dark")]
    [TestCase("Light")]
    public void StatusBar_UsesThemeStatusBarColors(string mode) {
        DispatcherTools.Run(dispatcher => {
            var theme = new ResourceDictionary {
                Source = new Uri($"/Brows.Framework.Windows;component/Resources/Themes/{mode}.FileExplorer.xaml",
                    UriKind.Relative)
            };
            var control = new FileSystemTerminalControl();
            control.Resources.MergedDictionaries.Add(theme);
            control.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.FileSystem.Windows;component/Resource/Resource.xaml", UriKind.Relative)
            });
            control.ApplyTemplate();
            var statusBar = (Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(control.Status));
            var background = theme["Brows.Theme.FileSystemTerminal.StatusBar.Background"];
            var foreground = theme["Brows.Theme.FileSystemTerminal.StatusBar.Foreground"];
            Assert.That(statusBar.Background, Is.SameAs(background));
            Assert.That(control.Status.Foreground, Is.SameAs(foreground));
            Assert.That(control.TriggerDescription.Foreground, Is.SameAs(foreground));
            Assert.That(control.Trigger.Foreground, Is.SameAs(foreground));
            Complete(control.CloseAsync(CancellationToken.None), dispatcher);
        });
    }

    [Test]
    public void ProviderTemplate_RegistersTerminalAndPreviewInSharedGrid() {
        var application = Application.Current ?? new Application {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri("/Brows.Framework.Windows;component/Resources/Resources.xaml", UriKind.Relative)
        });
        var control = new FileSystemProviderControl();
        control.ApplyTemplate();
        var grid = (ProviderGrid)VisualTreeHelper.GetChild(control, 0);
        var terminal = (DataTemplate)grid.Resources[typeof(FileSystemTerminal)];
        var preview = (DataTemplate)grid.Resources[typeof(FileSystemEntryPreview)];
        Assert.That(terminal.LoadContent(), Is.TypeOf<FileSystemTerminalControl>());
        Assert.That(preview, Is.Not.Null);
    }
    [Test]
    public void Unload_DetachesControllerAndRetainsAwaitableCleanup() {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            host.Root.Children.Remove(host.Control);
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Detail.CurrentController, Is.Null);
            Complete(host.Detail.WaitForCleanup(CancellationToken.None), dispatcher);
            Assert.That(host.Control.TerminalView.IsRunning, Is.False);
        });
    }

    [Test]
    public void ViewModelChange_DetachesOldModelAndAttachesReplacement() {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            var replacement = new FileSystemTerminal(host.Provider, host.Panels, host.Panel);
            host.Control.ViewModel = replacement;
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Detail.CurrentController, Is.Null);
            Assert.That(replacement.CurrentController, Is.SameAs(host.Control));
            Complete(host.Detail.WaitForCleanup(CancellationToken.None), dispatcher);
            Assert.That(host.Control.TerminalView.IsRunning, Is.False);
            Complete(((IProviderDetailLifetime)replacement).CloseAsync(CancellationToken.None), dispatcher);
        });
    }

    [Test]
    public void FocusEntriesAsync_InvalidatedRetainedTerminalCanReturnToEntryGrid() {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            /*
             * Initialize the empty grid without reading or writing user configuration.
             */
            var import = new Mock<IImportAgent>();
            import.Setup(item => item.Find<IConfigurationData>()).Returns(new MemoryConfigData());
            typeof(EntryProvider).GetProperty("Config", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host.Provider, new EntryConfig());
            typeof(Provider).GetProperty("Import", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host.Provider, import.Object);
            var entries = new EntryObservationControl {
                Height = 60,
                ViewModel = ((IEntryProvider)host.Provider).Observation
            };
            entries.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.Framework.Windows;component/Resources/Resources.xaml", UriKind.Relative)
            });
            host.Root.Children.Add(entries);
            host.Window.UpdateLayout();
            DispatcherTools.Pump(dispatcher);
            host.Window.Activate();
            Assert.That(((IEntryProvider)host.Provider).Observation.Focus(), Is.True,
                "The real entry grid must be focusable before exercising release.");
            Assert.That(entries.IsKeyboardFocusWithin, Is.True);
            Assert.That(host.Control.TerminalView.Focus(), Is.True);
            ((IProviderDetailLifetime)host.Detail).Invalidate();
            var releaseIntent = host.Detail.AdvanceReleaseIntent();
            var released = host.Control.FocusEntriesAsync(
                host.Provider.Detail.Generation, releaseIntent, CancellationToken.None);
            Complete(released, dispatcher);
            Assert.That(released.GetAwaiter().GetResult(), Is.True);
            Assert.That(entries.IsKeyboardFocusWithin, Is.True);
            Assert.That(host.Control.TerminalView.IsKeyboardFocusWithin, Is.False);
        });
    }
    [Test]
    public void ViewModel_UnrelatedDataContextDoesNotChangeAttachmentOrInputOwner() {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            host.Control.DataContext = new object();
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Detail.CurrentController, Is.SameAs(host.Control));
            Assert.That(InputOwner.GetOwner(host.Control.TerminalView), Is.SameAs(host.Detail));
        });
    }

    [Test]
    public void TemplateReplacement_DetachesMissingPartsAndReattachesWithoutStaleFocus() {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            host.Window.Activate();
            Assert.That(host.Anchor.Focus(), Is.True);
            var original = host.Control.TerminalView;
            var request = host.Detail.BeginRequest();
            host.Control.RequestFocusLater(host.Provider.Detail.Generation, request, CancellationToken.None);
            host.Control.Template = new ControlTemplate(typeof(FileSystemTerminalControl)) {
                VisualTree = new FrameworkElementFactory(typeof(Border))
            };
            host.Control.ApplyTemplate();
            Assert.That(host.Control.TerminalView, Is.Null);
            Assert.That(host.Detail.CurrentController, Is.Null);
            Complete(host.Detail.WaitForCleanup(CancellationToken.None), dispatcher);
            host.Control.ClearValue(Control.TemplateProperty);
            host.Control.ApplyTemplate();
            host.Window.UpdateLayout();
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Control.TerminalView, Is.Not.SameAs(original));
            Assert.That(host.Control.TerminalView.AutoStart, Is.False);
            Assert.That(host.Detail.CurrentController, Is.SameAs(host.Control));
            Assert.That(host.Anchor.IsKeyboardFocusWithin, Is.True);
            Assert.That(InputOwner.GetOwner(host.Control.TerminalView), Is.SameAs(host.Detail));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RequestFocusLater_RejectsSupersededIntentOrInactivePanel(bool switchPanel) {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            host.Window.Activate();
            var request = host.Detail.BeginRequest();
            host.Control.RequestFocusLater(host.Provider.Detail.Generation, request, CancellationToken.None);
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Control.TerminalView.IsKeyboardFocusWithin, Is.True,
                "The positive focus request must work before testing its invalidation.");
            Assert.That(host.Anchor.Focus(), Is.True);
            host.Control.RequestFocusLater(host.Provider.Detail.Generation, request, CancellationToken.None);
            if (switchPanel) {
                host.Panels.Active = new TestPanel();
            }
            else {
                host.Detail.AdvanceReleaseIntent();
            }
            DispatcherTools.Pump(dispatcher);
            Assert.That(host.Anchor.IsKeyboardFocusWithin, Is.True);
            Assert.That(host.Control.TerminalView.IsKeyboardFocusWithin, Is.False);
        });
    }

    [Test]
    [Explicit("Launches the real published ConPTY package and PowerShell on a Windows desktop.")]
    public void RealPackage_StartsInLiteralProviderDirectoryAndStopsOwnedSession() {
        DispatcherTools.Run(dispatcher => {
            var temporary = Directory.CreateTempSubdirectory("Brows-Phase3-");
            try {
                var directory = Directory.CreateDirectory(Path.Combine(temporary.FullName, "start ' & ü"));
                using var host = new HostFixture(dispatcher, directory);
                var opened = host.Control.OpenAsync(CancellationToken.None);
                Complete(opened, dispatcher);
                Assert.That(opened.GetAwaiter().GetResult(), Is.True);
                var terminal = host.Control.TerminalView;
                Assert.That(terminal.IsRunning, Is.True);
                var first = ProbeTerminal(terminal, dispatcher, 1);
                Assert.That(first[1], Is.EqualTo(directory.FullName));
                var processId = int.Parse(first[2], CultureInfo.InvariantCulture);
                using var process = Process.GetProcessById(processId);
                var reopened = host.Control.OpenAsync(CancellationToken.None);
                Complete(reopened, dispatcher);
                Assert.That(reopened.GetAwaiter().GetResult(), Is.True);
                var second = ProbeTerminal(terminal, dispatcher, 2);
                Assert.That(second[1], Is.EqualTo(directory.FullName));
                Assert.That(int.Parse(second[2], CultureInfo.InvariantCulture), Is.EqualTo(processId));
                Assert.That(terminal.LastError, Is.Null);
                var image = new RenderTargetBitmap((int)terminal.ActualWidth, (int)terminal.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                image.Render(terminal);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                var artifact = Path.Combine(TestContext.CurrentContext.TestDirectory, "Phase3-NativeSmoke.png");
                using (var output = File.Create(artifact)) {
                    encoder.Save(output);
                }
                TestContext.AddTestAttachment(artifact, "Real terminal output and literal provider directory.");
                Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
                Assert.That(terminal.IsRunning, Is.False);
                Assert.That(process.HasExited, Is.True);
            }
            finally {
                var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
                var createdPath = Path.GetFullPath(temporary.FullName);
                Assert.That(createdPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(createdPath, recursive: true);
            }
        });
    }
    [Test]
    [Explicit("Launches real PowerShell sessions while replacing the terminal control template.")]
    public void RealPackage_TemplateReplacementStopsOldSessionBeforeRestart() {
        DispatcherTools.Run(dispatcher => {
            using var host = new HostFixture(dispatcher);
            var opened = host.Control.OpenAsync(CancellationToken.None);
            Complete(opened, dispatcher);
            Assert.That(opened.GetAwaiter().GetResult(), Is.True);
            var oldTerminal = host.Control.TerminalView;
            var first = ProbeTerminal(oldTerminal, dispatcher, 1);
            using var oldProcess = Process.GetProcessById(int.Parse(first[2], CultureInfo.InvariantCulture));
            host.Control.Template = new ControlTemplate(typeof(FileSystemTerminalControl)) {
                VisualTree = new FrameworkElementFactory(typeof(Border))
            };
            host.Control.ApplyTemplate();
            Complete(host.Detail.WaitForCleanup(CancellationToken.None), dispatcher);
            Assert.That(oldTerminal.IsRunning, Is.False);
            Assert.That(oldProcess.HasExited, Is.True);
            host.Control.ClearValue(Control.TemplateProperty);
            host.Control.ApplyTemplate();
            host.Window.UpdateLayout();
            DispatcherTools.Pump(dispatcher);
            var restarted = host.Control.OpenAsync(CancellationToken.None);
            Complete(restarted, dispatcher);
            Assert.That(restarted.GetAwaiter().GetResult(), Is.True);
            var second = ProbeTerminal(host.Control.TerminalView, dispatcher, 2);
            Assert.That(second[1], Is.EqualTo(host.Provider.Directory.FullName));
            Assert.That(second[2], Is.Not.EqualTo(first[2]));
            Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
        });
    }

    [TestCase("unload")]
    [TestCase("template")]
    [TestCase("model")]
    public void LateStartAfterOwnerInvalidation_IsStoppedAndCannotStartReplacement(string invalidation) {
        DispatcherTools.Run(dispatcher => {
            var adapters = new List<DelayedSessionAdapter>();
            using var host = new HostFixture(dispatcher, sessionAdapterFactory: terminal => {
                var adapter = new DelayedSessionAdapter(dispatcher);
                adapters.Add(adapter);
                return adapter;
            });
            var session = adapters[0];
            var opening = host.Control.OpenAsync(CancellationToken.None);
            Complete(session.Started.Task, dispatcher);
            FileSystemTerminal replacement = null;

            if (invalidation == "unload") {
                host.Root.Children.Remove(host.Control);
            }
            else if (invalidation == "template") {
                host.Control.Template = new ControlTemplate(typeof(FileSystemTerminalControl)) {
                    VisualTree = new FrameworkElementFactory(typeof(Border))
                };
                host.Control.ApplyTemplate();
            }
            else {
                replacement = new FileSystemTerminal(host.Provider, host.Panels, host.Panel);
                host.Control.ViewModel = replacement;
            }
            DispatcherTools.Pump(dispatcher);
            var cleanup = host.Detail.WaitForCleanup(CancellationToken.None);
            Assert.That(cleanup.IsCompleted, Is.False,
                "Cleanup must wait for the in-flight start to produce its result.");

            session.CompleteStart();
            Complete(opening, dispatcher);
            Assert.That(opening.GetAwaiter().GetResult(), Is.False,
                "An invalidated open must not report success or retain ownership of its late session.");
            Complete(cleanup, dispatcher);
            Assert.That(session.StartCalls, Is.EqualTo(1));
            Assert.That(session.StopCalls, Is.EqualTo(1));
            Assert.That(session.IsRunning, Is.False);

            if (invalidation == "unload") {
                host.Root.Children.Add(host.Control);
            }
            else if (invalidation == "template") {
                host.Control.ClearValue(Control.TemplateProperty);
                host.Control.ApplyTemplate();
                host.Window.UpdateLayout();
                Assert.That(adapters, Has.Count.EqualTo(2));
                Assert.That(adapters[1].StartCalls, Is.Zero,
                    "Replacing the template must not automatically start another session.");
            }
            else {
                Complete(((IProviderDetailLifetime)replacement).CloseAsync(CancellationToken.None), dispatcher);
            }
            DispatcherTools.Pump(dispatcher);
            if (invalidation != "template") {
                Assert.That(session.StartCalls, Is.EqualTo(1),
                    "Reload or model replacement must not automatically start another session.");
            }
        });
    }

    [Test]
    public void ReleaseAndPanelSwitchDuringStartup_PreservePendingSession() {
        DispatcherTools.Run(dispatcher => {
            var adapters = new List<DelayedSessionAdapter>();
            using var host = new HostFixture(dispatcher, sessionAdapterFactory: terminal => {
                var adapter = new DelayedSessionAdapter(dispatcher);
                adapters.Add(adapter);
                return adapter;
            });
            var session = adapters[0];
            var opening = host.Control.OpenAsync(CancellationToken.None);
            Complete(session.Started.Task, dispatcher);
            host.Detail.AdvanceReleaseIntent();
            host.Panels.Active = new TestPanel();
            session.CompleteStart();

            Complete(opening, dispatcher);
            Assert.That(opening.GetAwaiter().GetResult(), Is.True);
            Assert.That(session.IsRunning, Is.True);
            Assert.That(session.StopCalls, Is.Zero);
            Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
            Assert.That(session.StopCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void CloseAsync_RetainsFailedRetiredPartForRetry() {
        DispatcherTools.Run(dispatcher => {
            var adapters = new List<DelayedSessionAdapter>();
            using var host = new HostFixture(dispatcher, sessionAdapterFactory: terminal => {
                var adapter = new DelayedSessionAdapter(dispatcher);
                adapters.Add(adapter);
                return adapter;
            });
            var retired = adapters[0];
            var opening = host.Control.OpenAsync(CancellationToken.None);
            Complete(retired.Started.Task, dispatcher);
            retired.CompleteStart();
            Complete(opening, dispatcher);
            Assert.That(opening.GetAwaiter().GetResult(), Is.True);

            retired.StopFailuresRemaining = 2;
            host.Control.Template = new ControlTemplate(typeof(FileSystemTerminalControl)) {
                VisualTree = new FrameworkElementFactory(typeof(Border))
            };
            host.Control.ApplyTemplate();
            DispatcherTools.Pump(dispatcher);
            Assert.That(retired.StopCalls, Is.EqualTo(1));
            host.Control.ClearValue(Control.TemplateProperty);
            host.Control.ApplyTemplate();
            host.Window.UpdateLayout();
            Assert.That(adapters, Has.Count.EqualTo(2));

            var close = host.Control.CloseAsync(CancellationToken.None);
            Assert.That(() => Complete(close, dispatcher), Throws.TypeOf<InvalidOperationException>());
            Assert.That(retired.StopCalls, Is.EqualTo(2));

            Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
            Assert.That(retired.StopCalls, Is.EqualTo(3));
            Assert.That(retired.IsRunning, Is.False);
        });
    }

    private sealed class DelayedSessionAdapter : ITerminalSessionAdapter {
        private readonly Dispatcher Dispatcher;
        private readonly TaskCompletionSource<bool> StartResult = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private bool Running;

        internal TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal int StartCalls { get; private set; }
        internal int StopCalls { get; private set; }
        internal int StopFailuresRemaining { get; set; }

        internal DelayedSessionAdapter(Dispatcher dispatcher) {
            Dispatcher = dispatcher;
        }

        internal void CompleteStart() {
            Dispatcher.VerifyAccess();
            Running = true;
            StartResult.TrySetResult(true);
        }

        public bool IsRunning {
            get {
                Dispatcher.VerifyAccess();
                return Running;
            }
        }

        public Task StartAsync() {
            Dispatcher.VerifyAccess();
            StartCalls++;
            Started.TrySetResult(true);
            return StartResult.Task;
        }

        public Task StopAsync() {
            Dispatcher.VerifyAccess();
            StopCalls++;
            if (StopFailuresRemaining > 0) {
                StopFailuresRemaining--;
                throw new InvalidOperationException("Controlled stop failure.");
            }
            Running = false;
            return Task.CompletedTask;
        }

    }

    private sealed class MemoryConfigData : IConfigurationData {
        private readonly Dictionary<Type, object> Values = [];

        public IConfiguration<TConfig> Data<TConfig>(string name)
        where TConfig : class, INotifyPropertyChanged, new() {
            if (Values.TryGetValue(typeof(TConfig), out var config) == false) {
                Values[typeof(TConfig)] = config = new MemoryConfig<TConfig>();
            }
            return (IConfiguration<TConfig>)config;
        }
    }

    private sealed class MemoryConfig<TConfig> : IConfiguration<TConfig> where TConfig : class, new() {
        public TConfig Loaded { get; } = new();

        public Task<TConfig> Load(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Loaded);
        }

        event EventHandler IConfiguration.Changed {
            add { }
            remove { }
        }
    }
    private sealed class HostFixture : IDisposable {
        private readonly Dispatcher Dispatcher;

        internal FileSystemProvider Provider { get; }
        internal TestPanel Panel { get; }
        internal TestPanelCollection Panels { get; }
        internal FileSystemTerminal Detail { get; }
        internal FileSystemTerminalControl Control { get; }
        internal TextBox Anchor { get; }
        internal Grid Root { get; }
        internal Window Window { get; }

        internal HostFixture(Dispatcher dispatcher,
                                  DirectoryInfo directory = null,
                                  Func<TerminalEmulator, ITerminalSessionAdapter> sessionAdapterFactory = null,
                                  TERMINAL terminalCommand = null) {
            Dispatcher = dispatcher;
            Provider = new FileSystemProvider(new FileSystemProviderFactory(),
                directory ?? new DirectoryInfo(Environment.SystemDirectory), caseSensitive: false, initialCapacity: 0);
            Panel = new TestPanel { Provider = Provider };
            Panels = new TestPanelCollection();
            Panels.Panels.Add(Panel);
            Detail = new FileSystemTerminal(Provider, Panels, Panel);
            Complete(Provider.Detail.ShowAsync(FileSystemTerminal.Key, Detail, CancellationToken.None), dispatcher);
            Control = sessionAdapterFactory is null
                ? new FileSystemTerminalControl { ViewModel = Detail }
                : new FileSystemTerminalControl(sessionAdapterFactory) { ViewModel = Detail };
            /*
             * Stand in for the command that imports populate in the application.
             */
            if (terminalCommand is not null) {
                Control.TerminalCommand = terminalCommand;
            }
            Control.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.Framework.Windows;component/Resources/Resources.xaml", UriKind.Relative)
            });
            Control.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.FileSystem.Windows;component/Resources/Styles/" +
                    "Brows.Style.FileSystemTerminal.xaml", UriKind.Relative)
            });
            Anchor = new TextBox();
            Root = new Grid();
            Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.RowDefinitions.Add(new RowDefinition());
            Root.Children.Add(Anchor);
            Grid.SetRow(Control, 1);
            Root.Children.Add(Control);
            Window = new Window {
                Content = Root,
                Left = -10000,
                Width = 400,
                Height = 200,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            Window.Show();
            Window.UpdateLayout();
            DispatcherTools.Pump(dispatcher);
        }

        public void Dispose() {
            Window.Close();
            DispatcherTools.Pump(Dispatcher);
            Complete(Provider.DisposeAsync().AsTask(), Dispatcher);
        }
    }
}
