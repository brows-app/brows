using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryStreamImageControlTest {
    private static void WaitForTask(Task task, Dispatcher dispatcher) {
        var frame = new DispatcherFrame();
        var completed = dispatcher.InvokeAsync(async () => {
            try {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally {
                frame.Continue = false;
            }
        }).Task.Unwrap();
        Dispatcher.PushFrame(frame);
        completed.GetAwaiter().GetResult();
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void ChangeEntryStreamGui_SupersededByNewerCall_LeavesNewerCallCurrent(
        bool shareState,
        bool completeNewerFirst) {
        DispatcherTools.Run(dispatcher => {
            var stateA = new FakeState();
            var stateB = shareState ? stateA : new FakeState();
            var sourceA = new FakeSource();
            var sourceB = new FakeSource();
            var guiA = new FakeGui(stateA, sourceA);
            var guiB = new FakeGui(stateB, sourceB);
            var window = new Window {
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -10000
            };
            window.Resources.MergedDictionaries.Add(new ResourceDictionary {
                Source = new Uri("pack://application:,,,/Brows.Framework.Windows;component/Resources/Resources.xaml")
            });
            var control = new EntryStreamImageControl {
                ViewModel = guiA
            };
            try {
                window.Content = control;
                window.Show();
                window.UpdateLayout();
                Assert.That(sourceA.ConsumeEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                var loadA = control.CurrentLoadTask;
                control.ViewModel = guiB;
                Assert.That(sourceB.ConsumeEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                var loadB = control.CurrentLoadTask;

                if (completeNewerFirst) {
                    sourceB.Ready.TrySetResult(new FakeReady());
                    WaitForTask(loadB, dispatcher);
                }
                sourceA.Ready.TrySetResult(new FakeReady());
                WaitForTask(loadA, dispatcher);
                var sharedLoadIsPending = shareState && !completeNewerFirst;

                using (Assert.EnterMultipleScope()) {
                    Assert.That(((FakeView)stateA.Image).Loading, Is.EqualTo(sharedLoadIsPending));
                    Assert.That(((FakeView)stateB.Image).Loading, Is.EqualTo(!completeNewerFirst));
                }

                if (!completeNewerFirst) {
                    var image = (Image)control.Template.FindName("PART_Image", control);
                    image.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    Assert.That(sourceB.Token.IsCancellationRequested, Is.True);

                    sourceB.Ready.TrySetResult(new FakeReady());
                    WaitForTask(loadB, dispatcher);
                }

                using (Assert.EnterMultipleScope()) {
                    Assert.That(((FakeView)stateA.Image).Loading, Is.False);
                    Assert.That(((FakeView)stateB.Image).Loading, Is.False);
                    Assert.That(((FakeView)stateA.Image).CompletionCount, Is.EqualTo(1));
                    Assert.That(((FakeView)stateB.Image).CompletionCount, Is.EqualTo(1));
                }
            }
            finally {
                sourceA.Ready.TrySetResult(new FakeReady());
                sourceB.Ready.TrySetResult(new FakeReady());
                window.Close();
            }
        });
    }

    private sealed class FakeReady : IEntryStreamReady {
        public void Dispose() {
        }
    }

    private sealed class FakeView : IEntryStreamGuiView {
        internal int CompletionCount { get; private set; }

        public bool Loading { get; private set; }

        public void Change(bool? loading = null, bool? success = null) {
            if (loading.HasValue) {
                Loading = loading.Value;
            }
            if (loading == false) {
                CompletionCount++;
            }
        }
    }

    private sealed class FakeState : INotifyPropertyChanged, IEntryStreamGuiState {
#pragma warning disable CS0067 // The interface requires the event, but this fake's properties never change.
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        public IEntryStreamGuiView Text => null;

        public IEntryStreamGuiView Image =>
            _Image ??= new FakeView();
        private FakeView _Image;

        public IEntryStreamGuiView Media => null;
        public IEntryStreamGuiView Preview => null;
    }

    private sealed class FakeSource : IEntryStreamSource {
        internal TaskCompletionSource<IEntryStreamReady> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim ConsumeEntered { get; } = new(false);
        internal CancellationToken Token {
            get => _Token;
            private set {
                _Token = value;
                ConsumeEntered.Set();
            }
        }
        private CancellationToken _Token;

        public string EntryID => null;
        public string EntryName => null;
        public string RelativePath => null;
        public string SourceFile => null;
        public string SourceDirectory => null;
        public long StreamLength => 0;
        public Stream Stream => null;

        public Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            Token = token;
            return Ready.Task;
        }
    }

    private sealed class FakeGui : IEntryStreamGui {
        public FakeGui(IEntryStreamGuiState state, IEntryStreamSource source) {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public string View => null;
        public IEntryStreamSource Source { get; }
        public IEntryStreamGuiState State { get; }
        public IEntryStreamGuiOptions Options => null;

#pragma warning disable CS0067 // The interface requires the event, but this fake's properties never change.
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067
    }
}
