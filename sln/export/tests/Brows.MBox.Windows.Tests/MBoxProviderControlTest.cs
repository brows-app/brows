using Brows.Composition;
using Brows.Detail;
using Brows.Entries;
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

using SHAPE = System.Windows.Shapes.Shape;

namespace Brows;

/// <summary>
/// Tests the control that the panel of a mailbox shows.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class MBoxProviderControlTest {
    private MBoxTestDirectory _Directory;
    private MBoxProviderFactory _Factory;
    private MBoxProvider _Provider;
    private Window _Window;
    private MBoxProviderControl _Control;

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
            /*
             * The user interface of the list shows the texts of the columns through the translation
             * of the application, so the fixture needs one to show the texts that the user sees.
             */
            new MBoxTranslation(),
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
            ViewModel = _Provider
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
        return FindVisuals<TextBlock>(root);
    }

    private static IReadOnlyList<T> FindVisuals<T>(DependencyObject root) where T : DependencyObject {
        var visuals = new List<T>();
        AddVisuals(root, visuals);
        return visuals;
    }

    private static void AddVisuals<T>(DependencyObject root, ICollection<T> visuals) where T : DependencyObject {
        if (root is T visual) {
            visuals.Add(visual);
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            AddVisuals(VisualTreeHelper.GetChild(root, i), visuals);
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

    private TextBlock FindTextBlock(string text) {
        return FindTextBlocks(_Control).FirstOrDefault(textBlock => textBlock.Text == text);
    }

    private static object ThemeBrush(string name) {
        return Application.Current.Resources[$"Brows.Theme.MBoxProvider.{name}"];
    }

    /*
     * The shapes of the icon that draw lines, which are those of the template of the icon that
     * have a stroke.
     */
    private IReadOnlyList<SHAPE> FindIconShapes() {
        var viewbox = FindVisuals<Viewbox>(FindProviderGrid(_Control)).First();
        return FindVisuals<SHAPE>(viewbox).Where(shape => shape.Stroke is not null).ToList();
    }

    /*
     * Shows the control while the mailbox is read, and again once the read is finished.
     */
    private async Task Read(Action whileReading, Action afterReading) {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Run(Mailbox(), new MBoxConfig(), async () => {
            _Provider.IndexGateForTesting = token => release.Task.WaitAsync(token);
            var refresh = ((IProvider)_Provider).Refresh(CancellationToken.None);
            try {
                Show();
                await WaitFor(() => FindTextBlock(_Provider.Status) is not null);
                Assert.That(_Provider.State, Is.EqualTo(MBoxProviderState.Loading));
                whileReading();
            }
            finally {
                /*
                 * The read is finished before the test ends, so that it does not continue on
                 * the context of a test that has already ended.
                 */
                release.TrySetResult();
                await refresh;
            }
            ProcessDispatcher();
            afterReading();
        }, refresh: false);
    }

    private void AssertInlineWithTheID(TextBlock textBlock) {
        var id = FindTextBlock(_Provider.ID);
        var idOrigin = id.TranslatePoint(new Point(0, 0), _Control);
        var origin = textBlock.TranslatePoint(new Point(0, 0), _Control);
        using (Assert.EnterMultipleScope()) {
            Assert.That(origin.Y, Is.EqualTo(idOrigin.Y).Within(0.5));
            Assert.That(origin.X, Is.GreaterThan(idOrigin.X + id.ActualWidth));
        }
    }

    private static ProviderGrid FindProviderGrid(MBoxProviderControl control) {
        var count = VisualTreeHelper.GetChildrenCount(control);
        if (count == 0) {
            return null;
        }
        return VisualTreeHelper.GetChild(control, 0) as ProviderGrid;
    }

    [Test]
    public void Resource_PanelProviderTemplate_ShowsMBoxProviderControl() {
        var resource = new ResourceDictionary {
            Source = new Uri("/Brows.MBox.Windows;component/Resource/Resource.xaml", UriKind.Relative)
        };
        var template = (DataTemplate)resource["PanelProvider_MBoxProvider"];
        Assert.That(template.LoadContent(), Is.TypeOf<MBoxProviderControl>());
    }

    [Test]
    public void ProviderControl_HoldsTheGridOfTheProvider() {
        Show();
        Assert.That(FindProviderGrid(_Control), Is.InstanceOf<ProviderGrid>());
    }

    [Test]
    public void ProviderControl_HoldsTheTemplateOfThePreview() {
        Show();
        var grid = FindProviderGrid(_Control);
        Assert.That(grid.Resources.Contains(typeof(MBoxEntryPreview)), Is.True);
        var template = (DataTemplate)grid.Resources[typeof(MBoxEntryPreview)];
        Assert.That(template.LoadContent(), Is.TypeOf<MBoxEntryPreviewControl>());
    }

    [Test]
    public async Task ProviderControl_ShowsThePreviewOfTheMailbox() {
        await Run(async () => {
            /*
             * The active detail is the view shown below the entries.
             */
            await ShowPreview();
            Show();
            var grid = FindProviderGrid(_Control);
            var preview = FindPreview(_Control);
            var list = FindEntryListView(_Control);
            using (Assert.EnterMultipleScope()) {
                Assert.That(grid.Provider, Is.SameAs(_Provider));
                Assert.That(list, Is.Not.Null);
                Assert.That(list.Items.Count, Is.EqualTo(1));
                Assert.That(list.Items[0], Is.SameAs(_Provider.Entries[0]));
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.ViewModel, Is.SameAs(_Provider.Detail.Active));
                Assert.That(((MBoxEntryPreview)_Provider.Detail.Active).Controller, Is.SameAs(preview));
                Assert.That(FindTextBlocks(_Control).Select(textBlock => textBlock.Text),
                            Does.Contain(_Provider.ID));
            }
            ((IEntryObservation)_Provider.Observation).Current(_Provider.Entries[0]);
            await WaitFor(() => preview.Text.Contains("First body."));
            Assert.That(preview.Text, Does.Contain("First body."));
            Assert.That(preview.BodyTextBox.Text, Does.Contain("First body."));
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
        await WaitFor(() => preview.Text.Contains("First body."));
        var observation = (IEntryObservation)_Provider.Observation;
        Assert.That(preview.RequestFocus(), Is.True);
        Assert.That(preview.HasKeyboardFocus, Is.True);
        Assert.That(observation.Focus(), Is.True);
        Assert.That(((IProviderFocus)observation).Get(), Is.True);
        Assert.That(preview.HasKeyboardFocus, Is.False);
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
    public async Task ProviderControl_ShowsTheIconOfTheProviderBeforeTheID() {
        await Run(() => {
            Show();
            var grid = FindProviderGrid(_Control);
            var stroke = ThemeBrush("Icon.Stroke");
            var icon = FindVisuals<SHAPE>(_Control)
                .Where(shape => ReferenceEquals(shape.Stroke, stroke))
                .ToList();
            var id = FindTextBlock(_Provider.ID);
            using (Assert.EnterMultipleScope()) {
                Assert.That(grid.Icon, Is.SameAs(_Provider));
                Assert.That(grid.IconTemplate, Is.SameAs(_Control.TryFindResource("Brows.Style.MBoxProvider.Icon")));
                Assert.That(icon, Is.Not.Empty);
                Assert.That(icon.Select(shape => shape.IsVisible), Is.All.True);
            }
            var iconOrigin = icon[0].TranslatePoint(new Point(0, 0), _Control);
            var idOrigin = id.TranslatePoint(new Point(0, 0), _Control);
            Assert.That(iconOrigin.X, Is.LessThan(idOrigin.X));
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ProviderControl_ShowsTheStatusAndErrorInLineWithTheID() {
        await Run("not a mailbox\n", new MBoxConfig(), () => {
            Show();
            AssertInlineWithTheID(FindTextBlock(_Provider.Status));
            AssertInlineWithTheID(FindTextBlock(_Provider.Error));
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ProviderControl_ShowsTheStatusInTheReadingColorWhileTheMailboxIsRead() {
        await Read(() => {
            var status = FindTextBlock(_Provider.Status);
            Assert.That(status.Foreground, Is.SameAs(ThemeBrush("StatusReadingForeground")));
            AssertInlineWithTheID(status);
        }, () => {
        });
    }

    [Test]
    public async Task ProviderControl_ShowsTheIconAndIDInTheReadingColorOnlyWhileTheMailboxIsRead() {
        var reading = ThemeBrush("ReadingForeground");
        await Read(() => {
            using (Assert.EnterMultipleScope()) {
                Assert.That(FindTextBlock(_Provider.ID).Foreground, Is.SameAs(reading));
                Assert.That(FindIconShapes(), Has.Count.EqualTo(3));
                Assert.That(FindIconShapes().Select(shape => shape.Stroke), Is.All.SameAs(reading));
            }
        }, () => {
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.State, Is.EqualTo(MBoxProviderState.Ready));
                Assert.That(FindTextBlock(_Provider.ID).Foreground, Is.Not.SameAs(reading));
                Assert.That(FindIconShapes().Select(shape => shape.Stroke), Has.None.SameAs(reading));
                Assert.That(FindIconShapes().Select(shape => shape.Stroke), Has.Some.SameAs(ThemeBrush("Icon.Stroke")));
            }
        });
    }

    [Test]
    public async Task ProviderControl_ShowsTheStatusInTheErrorColorWhenTheReadFails() {
        await Run("not a mailbox\n", new MBoxConfig(), () => {
            Show();
            var status = FindTextBlock(_Provider.Status);
            var error = FindTextBlock(_Provider.Error);
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.State, Is.EqualTo(MBoxProviderState.Failed));
                Assert.That(status.Foreground, Is.SameAs(ThemeBrush("StatusErrorForeground")));
                Assert.That(error.Foreground, Is.SameAs(ThemeBrush("ErrorForeground")));
            }
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ProviderControl_ShowsTheStatusInTheStatusColorWhenTheReadStopsAtALimit() {
        await Run(Mailbox(), new MBoxConfig { MaxEntries = 0 }, () => {
            Show();
            var status = FindTextBlock(_Provider.Status);
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.State, Is.EqualTo(MBoxProviderState.Partial));
                Assert.That(status.Foreground, Is.SameAs(ThemeBrush("StatusForeground")));
            }
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ProviderControl_HidesTheStatusAndErrorWhenTheyAreEmpty() {
        await Run(() => {
            Show();
            var statusStyle = _Control.TryFindResource("Brows.Style.MBoxProvider.Status");
            var errorStyle = _Control.TryFindResource("Brows.Style.MBoxProvider.Error");
            var hidden = FindTextBlocks(_Control)
                .Where(textBlock => ReferenceEquals(textBlock.Style, statusStyle) ||
                                    ReferenceEquals(textBlock.Style, errorStyle))
                .ToList();
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Provider.State, Is.EqualTo(MBoxProviderState.Ready));
                Assert.That(hidden, Has.Count.EqualTo(2));
                Assert.That(hidden.Select(textBlock => textBlock.Visibility), Is.All.EqualTo(Visibility.Collapsed));
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
