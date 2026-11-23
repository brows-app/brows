using Brows.Detail;
using Brows.Entries;
using Brows.Gui;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Tests the command that moves the keyboard focus between the list of the messages of a
/// mailbox and their preview.
/// </summary>
[TestFixture]
[NonParallelizable]
internal sealed class PreviewFocusTest {
    private MBoxTestDirectory _Directory;
    private MBoxProvider _Provider;
    private TestPanel _Panel;
    private TestCommandContext _Context;
    private PreviewController _Preview;

    [SetUp]
    public async Task SetUp() {
        _Directory = new MBoxTestDirectory();
        _Provider = await MBoxTestProvider.Create(Mailbox("inbox.mbox"), new MBoxConfig());
        await MBoxTestProvider.Refresh(_Provider, CancellationToken.None);
        _Panel = new TestPanel {
            Column = 0,
            Provider = _Provider,
        };
        _Context = new TestCommandContext {
            Panels = { Panels = { _Panel } },
        };
        _Preview = new PreviewController();
    }

    [TearDown]
    public void TearDown() {
        ((IProvider)_Provider).End();
        _Provider.Dispose();
        _Directory.Dispose();
    }

    private string Mailbox(string name) {
        return MBoxTestFixture.WriteFile(
            _Directory.Directory,
            name,
            MBoxTestFixture.Mailbox(
                "First body.\n",
                new[] {
                    "Subject: First subject",
                    "From: sender@example.com",
                }));
    }

    private bool Work(string parameter) {
        _Context.Line = new TestCommandLine { Parameter = parameter };
        PreviewFocus command = new() { CommandCliProvider = new CommandCliProvider() };
        return ((ICommand)command).TriggeredWork(_Context);
    }

    private async Task ShowPreview() {
        var detail = new MBoxEntryPreview(_Provider);
        var shown = await _Provider.Detail.ShowAsync(MBoxViews.Preview, detail, CancellationToken.None);
        Assert.That(shown, Is.True);
        ((IControlled<IMBoxEntryPreviewController>)detail).Controller = _Preview;
    }

    [Test]
    public void PreviewFocus_TriggeredWork_ShowsThePreviewWhenItIsHidden() {
        Work("");
        Assert.That(_Provider.Detail.Active, Is.InstanceOf<MBoxEntryPreview>());
    }

    [Test]
    public void PreviewFocus_TriggeredWork_ReturnsFalseWhenThePreviewIsNeverShown() {
        /*
         * The preview is created by the command, but no control shows it, so its controller is
         * never attached and the preview cannot take the focus.
         */
        Assert.That(Work(""), Is.False);
    }

    [Test]
    public async Task PreviewFocus_TriggeredWork_AsksThePreviewToTakeTheFocus() {
        await ShowPreview();
        Assert.That(Work(""), Is.True);
        Assert.That(_Preview.FocusLaterCount, Is.EqualTo(1));
    }

    [Test]
    public async Task PreviewFocus_TriggeredWork_ReturnsFalseWhenThePreviewDoesNotOwnTheFocus() {
        await ShowPreview();
        Assert.That(Work("entries"), Is.False);
    }

    [Test]
    public async Task PreviewFocus_TriggeredWork_DoesNotQueueFocusAfterCancellation() {
        await ShowPreview();
        _Context.Token = new CancellationToken(canceled: true);
        Assert.That(Work(""), Is.False);
        Assert.That(_Preview.FocusLaterCount, Is.Zero);
    }

    [Test]
    public void PreviewFocus_TriggeredWork_ReturnsFalseWhenThereIsNoPreviewToLeave() {
        Assert.That(Work("entries"), Is.False);
    }

    [Test]
    public async Task PreviewFocus_TriggeredWork_MovesTheFocusToTheListWhenThePreviewOwnsIt() {
        await ShowPreview();
        _Preview.HasKeyboardFocus = true;
        var observation = new ObservationController();
        ((IControlled<IEntryObservationController>)((IEntryProvider)_Provider).Observation).Controller = observation;
        Assert.That(Work("entries"), Is.True);
        Assert.That(observation.FocusCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Preview_WaitForController_ReturnsTheCurrentControllerAfterReattachment() {
        var detail = new MBoxEntryPreview(_Provider);
        var first = new PreviewController();
        var second = new PreviewController();
        var controlled = (IControlled<IMBoxEntryPreviewController>)detail;
        controlled.Controller = first;
        controlled.Controller = null;
        var pending = detail.WaitForController(TimeSpan.FromSeconds(1), CancellationToken.None);
        controlled.Controller = second;
        Assert.That(await pending, Is.SameAs(second));
    }

    private sealed class PreviewController : IMBoxEntryPreviewController {
        public bool HasKeyboardFocus { get; set; }

        public int FocusLaterCount { get; private set; }

        public bool RequestFocus() {
            return true;
        }

        public void RequestFocusLater(IPanel panel, IPanelCollection panels, CancellationToken token) {
            FocusLaterCount++;
        }

        event EventHandler IController.Loaded {
            add { }
            remove { }
        }
    }

    private sealed class ObservationController : IEntryObservationController {
        public bool AutoFit { private get; set; }

        public int FocusCount { get; private set; }

        public object DraggingSource =>
            null;

        public void Sort(IEntrySorting sorting) {
        }

        public bool Focus() {
            FocusCount++;
            return true;
        }

        public bool Focused() {
            return true;
        }

        public bool AddData(IEntryDataDefinition definition) {
            return true;
        }

        public bool HasData(string key) {
            return true;
        }

        public bool RemoveData(string key) {
            return true;
        }

        public void ClearData() {
        }

        public bool SizeToFit(string key) {
            return true;
        }

        public Task<bool> CurrentEntry(IEntry item, CancellationToken token) {
            return Task.FromResult(true);
        }

        public bool CurrentEntry(IEntry item) {
            return true;
        }

        public IEntry CurrentEntry() {
            return null;
        }

        public int CurrentPosition() {
            return 0;
        }

        public bool CurrentPosition(int value) {
            return true;
        }

        event EventHandler IEntryObservationController.ManualInteraction {
            add { }
            remove { }
        }

        event EventHandler IEntryObservationController.NothingSelected {
            add { }
            remove { }
        }

        event EventHandler IEntryObservationController.DraggingSelected {
            add { }
            remove { }
        }

        event EventHandler IEntryObservationController.CurrentEntryChanged {
            add { }
            remove { }
        }
    }
}
