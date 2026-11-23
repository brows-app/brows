using Brows.Detail;
using Brows.Entries;
using Brows.Panels;
using Brows.Providers;
using Brows.Windows;
using Brows.Windows.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class FileSystemTerminalPackageIntegrationTest {
    private static void Complete(Task task, Dispatcher dispatcher) {
        if (task.IsCompleted == false) {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timeout.Tick += (_, _) => frame.Continue = false;
            timeout.Start();
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
            Dispatcher.PushFrame(frame);
            timeout.Stop();
        }
        Assert.That(task.IsCompleted, Is.True, "The package operation did not complete within ten seconds.");
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
            terminal.Paste("[Console]::Write([char]27+']0;" + prefix + "'+" +
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

    private static void Enter(TerminalEmulator terminal) {
        terminal.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(terminal), Environment.TickCount, Key.Enter) {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
    }

    private static DirectoryInfo CreateTemporaryDirectory(string name) {
        var temporary = Directory.CreateTempSubdirectory("Brows-Phase4-");
        return Directory.CreateDirectory(Path.Combine(temporary.FullName, name));
    }

    private static void AttachTerminalScreenshot(TerminalEmulator terminal, Dispatcher dispatcher, string name) {
        terminal.UpdateLayout();
        DispatcherTools.Pump(dispatcher);
        var width = Math.Max(1, (int)Math.Ceiling(terminal.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(terminal.ActualHeight));
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        image.Render(terminal);
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, name);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var output = File.Create(path)) {
            encoder.Save(output);
        }
        TestContext.AddTestAttachment(path, "Published package terminal frame observation.");
    }

    private static void AssertStartupFailureReported(TerminalHost host) {
        var format = FileSystemText.Value(host.Control.Translation, "FileSystemTerminal_Status_Failed");
        var argumentStart = format.IndexOf("{0}", StringComparison.Ordinal);
        var prefix = argumentStart < 0 ? format : format[..argumentStart];
        Assert.That(host.Control.Status.Text, Does.StartWith(prefix));
    }

    private static DirectoryInfo FindInaccessibleDirectory() {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        var candidates = new[] {
            Path.Combine(root, "System Volume Information"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "config")
        };
        foreach (var candidate in candidates) {
            if (Directory.Exists(candidate) == false) {
                continue;
            }
            try {
                using var entries = Directory.EnumerateFileSystemEntries(candidate).GetEnumerator();
                entries.MoveNext();
            }
            catch (UnauthorizedAccessException) {
                return new DirectoryInfo(candidate);
            }
        }
        return null;
    }

    private static void AssertStartedIn(TerminalHost host, string expectedDirectory, Dispatcher dispatcher) {
        var opened = host.Control.OpenAsync(CancellationToken.None);
        Complete(opened, dispatcher);
        Assert.That(opened.GetAwaiter().GetResult(), Is.True);
        var probe = ProbeTerminal(host.Control.TerminalView, dispatcher, 1);
        Assert.That(Path.TrimEndingDirectorySeparator(probe[1]),
            Is.EqualTo(Path.TrimEndingDirectorySeparator(expectedDirectory)).IgnoreCase);
        Assert.That(host.Control.TerminalView.IsRunning, Is.True);
    }

    private static void DeleteTemporaryDirectory(DirectoryInfo directory) {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var createdPath = Path.GetFullPath(directory.FullName);
        Assert.That(createdPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase), Is.True);
        if (Directory.Exists(createdPath)) {
            Directory.Delete(createdPath, recursive: true);
        }
        var parent = Directory.GetParent(createdPath);
        var parentPath = parent is null ? null : Path.GetFullPath(parent.FullName);
        var parentIsOwnedTemporaryDirectory = parent is not null &&
            parent.Name.StartsWith("Brows-Phase4-", StringComparison.Ordinal);
        var parentIsInsideTemporaryRoot = parentPath is not null &&
            parentPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase);
        if (parentIsOwnedTemporaryDirectory && parentIsInsideTemporaryRoot) {
            Directory.Delete(parentPath, recursive: true);
        }
    }

    [Test]
    [Explicit("Launches the published ConPTY package and PowerShell in a literal Unicode path.")]
    public void RealPackage_StartsInLiteralDirectoryWithSpacesQuotesAmpersandAndUnicode() {
        DispatcherTools.Run(dispatcher => {
            var directory = CreateTemporaryDirectory("start ' & ü");
            try {
                using var host = new TerminalHost(dispatcher, directory);
                AssertStartedIn(host, directory.FullName, dispatcher);
            }
            finally {
                DeleteTemporaryDirectory(directory);
            }
        });
    }

    [Test]
    [Explicit("Launches the published ConPTY package and PowerShell at a drive root.")]
    public void RealPackage_StartsAtDriveRoot() {
        DispatcherTools.Run(dispatcher => {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            using var host = new TerminalHost(dispatcher, new DirectoryInfo(root));
            AssertStartedIn(host, root, dispatcher);
            Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
        });
    }

    [Test]
    [Explicit("Launches the published ConPTY package from the existing HOMESHARE UNC path.")]
    public void RealPackage_StartsFromConfiguredUncShare() {
        DispatcherTools.Run(dispatcher => {
            var share = Environment.GetEnvironmentVariable("HOMESHARE");
            var shareIsUnc = share is not null && share.StartsWith("\\\\", StringComparison.Ordinal);
            if (shareIsUnc == false || Directory.Exists(share) == false) {
                Assert.Ignore("No accessible UNC directory is configured in HOMESHARE; " +
                    "no share or credentials were changed.");
            }
            using var host = new TerminalHost(dispatcher, new DirectoryInfo(share));
            AssertStartedIn(host, share, dispatcher);
            Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
        });
    }

    [Test]
    [Explicit(
        "Starts PowerShell against a directory deleted after provider creation; " +
        "verifies startup failure is reported.")]
    public void RealPackage_DeletedStartDirectoryReportsFailureAndLeavesProviderAvailable() {
        DispatcherTools.Run(dispatcher => {
            var directory = CreateTemporaryDirectory("deleted start directory");
            try {
                using var host = new TerminalHost(dispatcher, directory);
                Directory.Delete(directory.FullName, recursive: true);
                var opened = host.Control.OpenAsync(CancellationToken.None);
                Complete(opened, dispatcher);
                Assert.That(opened.GetAwaiter().GetResult(), Is.False);
                Assert.That(host.Control.TerminalView.IsRunning, Is.False);
                AssertStartupFailureReported(host);
                Assert.That(host.Provider.Directory.FullName, Is.EqualTo(directory.FullName));
                Assert.That(((IEntryProvider)host.Provider).Observation, Is.Not.Null);

                Directory.CreateDirectory(directory.FullName);
                var retry = host.Control.OpenAsync(CancellationToken.None);
                Complete(retry, dispatcher);
                Assert.That(retry.GetAwaiter().GetResult(), Is.True);
                var probe = ProbeTerminal(host.Control.TerminalView, dispatcher, 7);
                Assert.That(Path.TrimEndingDirectorySeparator(probe[1]),
                    Is.EqualTo(Path.TrimEndingDirectorySeparator(directory.FullName)).IgnoreCase);
                using var process = Process.GetProcessById(int.Parse(probe[2], CultureInfo.InvariantCulture));
                Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
                Assert.That(process.HasExited, Is.True);
            }
            finally {
                DeleteTemporaryDirectory(directory);
            }
        });
    }

    [Test]
    [Explicit("Uses an existing directory whose contents are unreadable to this process; does not alter ACLs.")]
    public void RealPackage_InaccessibleStartDirectoryReportsFailure() {
        DispatcherTools.Run(dispatcher => {
            var directory = FindInaccessibleDirectory();
            if (directory is null) {
                Assert.Ignore("No existing directory denies read access to this process; no ACLs were changed.");
            }
            using var host = new TerminalHost(dispatcher, directory);
            var opened = host.Control.OpenAsync(CancellationToken.None);
            Complete(opened, dispatcher);
            if (opened.GetAwaiter().GetResult()) {
                Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
                Assert.Ignore(
                    "This directory denies listing but still permits a process working directory; " +
                    "no ACLs were changed.");
            }
            Assert.That(host.Control.TerminalView.IsRunning, Is.False);
            AssertStartupFailureReported(host);
            Complete(host.Control.CloseAsync(CancellationToken.None), dispatcher);
        });
    }

    [Test]
    [Explicit("Runs real PowerShell sessions to verify exit reporting, restart, and per-panel process isolation.")]
    public void RealPackage_ExitRestartAndTwoPanelSessionsRemainIndependent() {
        DispatcherTools.Run(dispatcher => {
            var temporary = Directory.CreateTempSubdirectory("Brows-Phase4-panels-");
            var firstDirectory = Directory.CreateDirectory(Path.Combine(temporary.FullName, "first panel"));
            var secondDirectory = Directory.CreateDirectory(Path.Combine(temporary.FullName, "second panel"));
            try {
                var panels = new TestPanelCollection();
                using var first = new TerminalHost(dispatcher, firstDirectory, panels);
                using var second = new TerminalHost(dispatcher, secondDirectory, panels);
                var firstOpen = first.Control.OpenAsync(CancellationToken.None);
                Complete(firstOpen, dispatcher);
                Assert.That(firstOpen.GetAwaiter().GetResult(), Is.True);
                var firstProbe = ProbeTerminal(first.Control.TerminalView, dispatcher, 1);
                Assert.That(firstProbe[1], Is.EqualTo(firstDirectory.FullName));
                var firstProcessId = int.Parse(firstProbe[2], CultureInfo.InvariantCulture);
                using var firstProcess = Process.GetProcessById(firstProcessId);

                first.Control.TerminalView.Paste("Set-Location $env:SystemRoot");
                Enter(first.Control.TerminalView);
                var changedDirectory = ProbeTerminal(first.Control.TerminalView, dispatcher, 5);
                Assert.That(Path.TrimEndingDirectorySeparator(changedDirectory[1]),
                    Is.EqualTo(Path.TrimEndingDirectorySeparator(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows))).IgnoreCase);
                var reopenedShell = first.Control.OpenAsync(CancellationToken.None);
                Complete(reopenedShell, dispatcher);
                Assert.That(reopenedShell.GetAwaiter().GetResult(), Is.True);
                var retainedShellDirectory = ProbeTerminal(first.Control.TerminalView, dispatcher, 6);
                Assert.That(Path.TrimEndingDirectorySeparator(retainedShellDirectory[1]),
                    Is.EqualTo(Path.TrimEndingDirectorySeparator(changedDirectory[1])).IgnoreCase);
                Assert.That(int.Parse(retainedShellDirectory[2], CultureInfo.InvariantCulture),
                    Is.EqualTo(firstProcessId));
                Assert.That(first.Provider.Directory.FullName, Is.EqualTo(firstDirectory.FullName));
                var secondOpen = second.Control.OpenAsync(CancellationToken.None);
                Complete(secondOpen, dispatcher);
                Assert.That(secondOpen.GetAwaiter().GetResult(), Is.True);
                var secondProbe = ProbeTerminal(second.Control.TerminalView, dispatcher, 2);
                Assert.That(secondProbe[1], Is.EqualTo(secondDirectory.FullName));
                var secondProcessId = int.Parse(secondProbe[2], CultureInfo.InvariantCulture);
                using var secondProcess = Process.GetProcessById(secondProcessId);
                Assert.That(secondProcessId, Is.Not.EqualTo(firstProcessId));

                var exited = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<TerminalExitedEventArgs> onExited = (_, args) => exited.TrySetResult(args.ExitCode);
                first.Control.TerminalView.TerminalExited += onExited;
                try {
                    first.Control.TerminalView.Paste(
                        "[Console]::Write([char]27+'[31mBROWS-PHASE4-ANSI-FINAL'+" +
                        "[char]27+'[0m'+[char]10); exit 17");
                    Enter(first.Control.TerminalView);
                    Complete(exited.Task, dispatcher);
                    Assert.That(exited.Task.GetAwaiter().GetResult(), Is.EqualTo(17u));
                }
                finally {
                    first.Control.TerminalView.TerminalExited -= onExited;
                }
                Assert.That(first.Control.TerminalView.IsRunning, Is.False);
                Assert.That(firstProcess.HasExited, Is.True);
                AttachTerminalScreenshot(first.Control.TerminalView, dispatcher, "Phase4-Terminal-AfterExit.png");
                Assert.That(second.Control.TerminalView.IsRunning, Is.True);
                Assert.That(secondProcess.HasExited, Is.False);

                var restarted = first.Control.OpenAsync(CancellationToken.None);
                Complete(restarted, dispatcher);
                Assert.That(restarted.GetAwaiter().GetResult(), Is.True);
                var restartedProbe = ProbeTerminal(first.Control.TerminalView, dispatcher, 3);
                AttachTerminalScreenshot(first.Control.TerminalView, dispatcher, "Phase4-Terminal-AfterRestart.png");
                Assert.That(restartedProbe[1], Is.EqualTo(firstDirectory.FullName));
                Assert.That(int.Parse(restartedProbe[2], CultureInfo.InvariantCulture), Is.Not.EqualTo(firstProcessId));
                Assert.That(second.Control.TerminalView.IsRunning, Is.True);
                Assert.That(secondProcess.HasExited, Is.False);

                Complete(first.Control.CloseAsync(CancellationToken.None), dispatcher);
                Assert.That(first.Control.TerminalView.IsRunning, Is.False);
                Assert.That(second.Control.TerminalView.IsRunning, Is.True);
                Assert.That(secondProcess.HasExited, Is.False);
                var secondStillWorks = ProbeTerminal(second.Control.TerminalView, dispatcher, 4);
                Assert.That(secondStillWorks[1], Is.EqualTo(secondDirectory.FullName));
                Complete(second.Control.CloseAsync(CancellationToken.None), dispatcher);
                Assert.That(secondProcess.HasExited, Is.True);
            }
            finally {
                DeleteTemporaryDirectory(firstDirectory);
            }
        });
    }

    private sealed class TerminalHost : IDisposable {
        private readonly Dispatcher Dispatcher;
        private readonly bool OwnPanels;

        internal FileSystemProvider Provider { get; }
        internal TestPanel Panel { get; }
        internal TestPanelCollection Panels { get; }
        internal FileSystemTerminalControl Control { get; }
        internal Window Window { get; }

        internal TerminalHost(Dispatcher dispatcher, DirectoryInfo directory, TestPanelCollection panels = null) {
            Dispatcher = dispatcher;
            OwnPanels = panels is null;
            Panels = panels ?? new TestPanelCollection();
            Provider = new FileSystemProvider(new FileSystemProviderFactory(),
                directory, caseSensitive: false, initialCapacity: 0);
            Panel = new TestPanel { Provider = Provider };
            Panels.Panels.Add(Panel);
            var detail = new FileSystemTerminal(Provider, Panels, Panel);
            Complete(Provider.Detail.ShowAsync(FileSystemTerminal.Key, detail, CancellationToken.None), dispatcher);
            Control = new FileSystemTerminalControl { ViewModel = detail };
            Control.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.Framework.Windows;component/Resources/Resources.xaml", UriKind.Relative)
            });
            Control.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("/Brows.FileSystem.Windows;component/Resources/Styles/" +
                    "Brows.Style.FileSystemTerminal.xaml", UriKind.Relative)
            });
            Window = new Window {
                Content = Control, Left = -10000, Width = 500, Height = 240,
                ShowActivated = false, ShowInTaskbar = false
            };
            Window.Show();
            Window.UpdateLayout();
            DispatcherTools.Pump(dispatcher);
        }

        public void Dispose() {
            Complete(Control.CloseAsync(CancellationToken.None), Dispatcher);
            Window.Close();
            DispatcherTools.Pump(Dispatcher);
            Complete(Provider.DisposeAsync().AsTask(), Dispatcher);
            if (OwnPanels) {
                Panels.Panels.Remove(Panel);
            }
        }
    }
}
