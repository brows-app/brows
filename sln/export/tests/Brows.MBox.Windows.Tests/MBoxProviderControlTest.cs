using Brows.Composition;
using Brows.Detail;
using Brows.Entries;
using Brows.Localization;
using Brows.Panels;
using Brows.Providers;
using Brows.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brows;

/// <summary>
/// Tests the control that the panel of a mailbox shows.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class MBoxProviderControlTest {
    private static ITranslation _PreviousTranslation;

    private MBoxTestDirectory _Directory;
    private MBoxProviderFactory _Factory;
    private MBoxProvider _Provider;
    private Window _Window;
    private MBoxProviderControl _Control;

    [OneTimeSetUp]
    public void OneTimeSetUp() {
        /*
         * The user interface of the list shows the texts of the columns through the translation
         * of the application, so the fixture needs one to show the texts that the user sees.
         */
        _PreviousTranslation = Translation.Global;
        Translation.Global = new MBoxTranslation();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() {
        Translation.Global = _PreviousTranslation;
    }

    [SetUp]
    public void SetUp() {
        MBoxTestResources.Add();
        _Directory = new MBoxTestDirectory();
    }

    [TearDown]
    public void TearDown() {
        _Control = null;
        _Window?.Close();
        _Window = null;
        if (_Provider is not null) {
            ((IProvider)_Provider).End();
        }
        _Provider?.Dispose();
        _Provider = null;
        _Directory.Dispose();
    }

    private static IExport[] Exports(MBoxProviderFactory factory) {
        return [
            factory,
            new Brows.Data.MBoxEntryData.Subject(),
            new Brows.Data.MBoxEntryData.From(),
            new Brows.Data.MBoxEntryData.Date(),
            new Brows.Data.MBoxEntryData.To(),
            new Brows.Data.MBoxEntryData.MessageId(),
            new Brows.Data.MBoxEntryData.Labels(),
            new Brows.Data.MBoxEntryData.ThreadId(),
            new Brows.Data.MBoxEntryData.Size(),
        ];
    }

    private static string Mailbox() {
        return "From sender@example.com Thu Sep 17 17:54:37 +0000 2015\n" +
               "Subject: First subject\n" +
               "From: sender@example.com\n" +
               "Date: Thu, 17 Sep 2015 17:54:37 +0000\n" +
               "\n" +
               "First body.\n";
    }

    private static void ProcessDispatcher() {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        _ = dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static async Task WaitFor(Func<bool> condition) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            ProcessDispatcher();
            if (condition()) {
                return;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("The condition was not met in time.");
    }

    /*
     * The provider needs an import agent, and the only way to give it one is to create it
     * through the factory, so that the composition fills the agent of the factory in.
     */
    private Task Run(Func<Task> test) {
        return Run(Mailbox(), null, test);
    }

    private async Task Run(string mailbox, MBoxConfig config, Func<Task> test, bool refresh = true) {
        _Factory = config is null ? new MBoxProviderFactory() : new MBoxProviderFactory(config);
        await ImportSandbox.Enter(Exports(_Factory), async () => {
            var path = Path.Combine(_Directory.Directory.FullName, "inbox.mbox");
            await File.WriteAllTextAsync(path, mailbox);
            var panel = new TestPanel();
            var provider = await ((IProviderFactory)_Factory).CreateFor(path, panel, CancellationToken.None);
            _Provider = (MBoxProvider)provider;
            if (refresh) {
                await ((IProvider)_Provider).Refresh(CancellationToken.None);
            }
            await test();
        });
    }

    private void Show() {
        _Control = new MBoxProviderControl {
            DataContext = _Provider
        };
        _Window = new Window {
            Width = 800,
            Height = 600,
            Content = _Control
        };
        _Window.Show();
        ProcessDispatcher();
    }

    private static IReadOnlyList<TextBlock> FindTextBlocks(DependencyObject root) {
        var textBlocks = new List<TextBlock>();
        AddTextBlocks(root, textBlocks);
        return textBlocks;
    }

    private static void AddTextBlocks(DependencyObject root, ICollection<TextBlock> textBlocks) {
        if (root is TextBlock textBlock) {
            textBlocks.Add(textBlock);
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            AddTextBlocks(VisualTreeHelper.GetChild(root, i), textBlocks);
        }
    }

    private static ListView FindEntryListView(DependencyObject root) {
        if (root is ListView listView) {
            return listView;
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            var found = FindEntryListView(VisualTreeHelper.GetChild(root, i));
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    private static MBoxEntryPreviewControl FindPreview(DependencyObject root) {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is MBoxEntryPreviewControl preview) {
                return preview;
            }
            var found = FindPreview(child);
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    private async Task<MBoxEntryPreview> ShowPreview() {
        var detail = new MBoxEntryPreview(_Provider);
        var shown = await _Provider.Detail.ShowAsync(MBoxViews.Preview, detail, CancellationToken.None);
        Assert.That(shown, Is.True);
        return detail;
    }

    [Test]
    public void ProviderControl_HoldsTheGridOfTheProvider() {
        var control = new MBoxProviderControl();
        Assert.That(control.Content, Is.InstanceOf<ProviderGrid>());
    }

    [Test]
    public void ProviderControl_HoldsTheTemplateOfThePreview() {
        var control = new MBoxProviderControl();
        var grid = (ProviderGrid)control.Content;
        Assert.That(grid.Resources.Contains(typeof(MBoxEntryPreview)), Is.True);
    }

    [Test]
    public async Task ProviderControl_ShowsThePreviewOfTheMailbox() {
        await Run(async () => {
            /*
             * The active detail is the view shown below the entries.
             */
            await ShowPreview();
            Show();
            var grid = (ProviderGrid)_Control.Content;
            var preview = FindPreview(_Control);
            var list = FindEntryListView(_Control);
            using (Assert.EnterMultipleScope()) {
                Assert.That(grid.Provider, Is.SameAs(_Provider));
                Assert.That(list, Is.Not.Null);
                Assert.That(list.Items.Count, Is.EqualTo(1));
                Assert.That(list.Items[0], Is.SameAs(_Provider.Entries[0]));
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.DataContext, Is.SameAs(_Provider.Detail.Active));
                Assert.That(FindTextBlocks(_Control).Select(textBlock => textBlock.Text),
                            Does.Contain(_Provider.ID));
            }
            ((IEntryObservation)_Provider.Observation).Current(_Provider.Entries[0]);
            await WaitFor(() => preview.Controller.Text.Contains("First body."));
            Assert.That(preview.Controller.Text, Does.Contain("First body."));
        });
    }

    [Test]
    public async Task ProviderControl_MovesKeyboardFocusBetweenTheListAndPreview() {
        await Run(async () => {
            await ShowPreview();
            Show();
            var preview = FindPreview(_Control);
            ((IEntryObservation)_Provider.Observation).Current(_Provider.Entries[0]);
            await FocusPreviewAndList(preview);
        });
    }

    private async Task FocusPreviewAndList(MBoxEntryPreviewControl preview) {
        await WaitFor(() => preview.Controller.Text.Contains("First body."));
        var observation = (IEntryObservation)_Provider.Observation;
        Assert.That(preview.Controller.RequestFocus(), Is.True);
        Assert.That(preview.Controller.HasKeyboardFocus, Is.True);
        Assert.That(observation.Focus(), Is.True);
        Assert.That(((IProviderFocus)observation).Get(), Is.True);
        Assert.That(preview.Controller.HasKeyboardFocus, Is.False);
    }

    [Test]
    public async Task ProviderControl_RendersTheFailureStatusAndError() {
        await Run("not a mailbox\n", new MBoxConfig(), () => {
            Show();
            var renderedText = FindTextBlocks(_Control).Select(textBlock => textBlock.Text).ToArray();
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.Status, Is.Not.Empty);
                Assert.That(_Provider.Error, Is.Not.Empty);
                Assert.That(renderedText, Does.Contain(_Provider.Status));
                Assert.That(renderedText, Does.Contain(_Provider.Error));
            }
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ProviderControl_RendersTheEmptyMailboxStatus() {
        await Run(string.Empty, new MBoxConfig(), () => {
            Show();
            var renderedText = FindTextBlocks(_Control).Select(textBlock => textBlock.Text);
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.Status, Is.Not.Empty);
                Assert.That(renderedText, Does.Contain(_Provider.Status));
            }
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ProviderControl_RendersThePartialMailboxStatus() {
        await Run(Mailbox(), new MBoxConfig { MaxEntries = 0 }, () => {
            Show();
            var renderedText = FindTextBlocks(_Control).Select(textBlock => textBlock.Text);
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.Status, Is.Not.Empty);
                Assert.That(renderedText, Does.Contain(_Provider.Status));
            }
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ProviderControl_RendersTheCancelledMailboxStatus() {
        await Run(Mailbox(), new MBoxConfig(), async () => {
            await ((IProvider)_Provider).Refresh(new CancellationToken(canceled: true));
            Show();
            var renderedText = FindTextBlocks(_Control).Select(textBlock => textBlock.Text);
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.State, Is.EqualTo(MBoxProviderState.Cancelled));
                Assert.That(_Provider.Status, Is.Not.Empty);
                Assert.That(renderedText, Does.Contain(_Provider.Status));
            }
        }, refresh: false);
    }

    [Test]
    public async Task ProviderControl_RendersTheMissingMailboxFailure() {
        await Run(Mailbox(), new MBoxConfig(), async () => {
            File.Delete(_Provider.Path);
            await ((IProvider)_Provider).Refresh(CancellationToken.None);
            Show();
            var renderedText = FindTextBlocks(_Control).Select(textBlock => textBlock.Text).ToArray();
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.Status, Is.Not.Empty);
                Assert.That(_Provider.Error, Is.Not.Empty);
                Assert.That(renderedText, Does.Contain(_Provider.Status));
                Assert.That(renderedText, Does.Contain(_Provider.Error));
            }
        }, refresh: false);
    }
}
