using Brows.Composition;
using Brows.Detail;
using Brows.Entries;
using Brows.MBox.Format;
using Brows.Panels;
using Brows.Providers;
using Brows.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class MBoxEntryPreviewControlTest {
    private MBoxTestDirectory _Directory;
    private MBoxProviderFactory _Factory;
    private MBoxProvider _Provider;
    private MBoxProvider _OtherProvider;
    private TestPanel _Panel;
    private TestPanelCollection _Panels;
    private Window _Window;
    private MBoxEntryPreviewControl _Control;
    private MBoxEntryPreview _Detail;
    private EntryObservationControl _ObservationControl;
    private EntryObservationControl _OtherObservationControl;

    [SetUp]
    public void SetUp() {
        _Directory = new MBoxTestDirectory();
    }

    [TearDown]
    public void TearDown() {
        _Control = null;
        _Detail = null;
        _ObservationControl = null;
        _OtherObservationControl = null;
        _Panel = null;
        _Panels = null;
        _Window?.Close();
        _Window = null;
        if (_Provider is not null) {
            ((IProvider)_Provider).End();
        }
        if (_OtherProvider is not null) {
            ((IProvider)_OtherProvider).End();
        }
        _Provider?.Dispose();
        _Provider = null;
        _OtherProvider?.Dispose();
        _OtherProvider = null;
        _Directory.Dispose();
    }

    private static void ProcessDispatcher() {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        _ = dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
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

    private static string Mailbox(params (string Subject, string Body)[] messages) {
        var text = new StringBuilder();
        for (var i = 0; i < messages.Length; i++) {
            if (i > 0) {
                /*
                 * The empty line that separates two records of a mailbox.
                 */
                text.Append('\n');
            }
            text.Append("From sender@example.com Thu Sep 17 17:54:37 +0000 2015\n");
            text.Append($"Subject: {messages[i].Subject}\n");
            text.Append("From: sender@example.com\n");
            text.Append("Date: Thu, 17 Sep 2015 17:54:37 +0000\n");
            text.Append('\n');
            text.Append(messages[i].Body);
            text.Append('\n');
        }
        return text.ToString();
    }

    /*
     * The provider needs an import agent, and the only way to give it one is to create it
     * through the factory, so that the composition fills the agent of the factory in.
     */
    private async Task Run(string mailbox, Func<Task> test) {
        _Factory = new MBoxProviderFactory();
        await ImportSandbox.Enter(Exports(_Factory), async () => {
            var path = Path.Combine(_Directory.Directory.FullName, "inbox.mbox");
            await File.WriteAllTextAsync(path, mailbox);
            _Panel = new TestPanel();
            _Panels = new TestPanelCollection { Panels = { _Panel } };
            var provider = await ((IProviderFactory)_Factory).CreateFor(path, _Panel, CancellationToken.None);
            _Provider = (MBoxProvider)provider;
            _Panel.Provider = _Provider;
            await ((IProvider)_Provider).Refresh(CancellationToken.None);
            _Detail = new MBoxEntryPreview(_Provider);
            var shown = await _Provider.Detail.ShowAsync(MBoxViews.Preview, _Detail, CancellationToken.None);
            Assert.That(shown, Is.True);
            await test();
        });
    }

    private async Task<MBoxProvider> CreateProvider(string fileName, string mailbox) {
        var path = Path.Combine(_Directory.Directory.FullName, fileName);
        await File.WriteAllTextAsync(path, mailbox);
        var provider = (MBoxProvider)await ((IProviderFactory)_Factory).CreateFor(
            path,
            new TestPanel(),
            CancellationToken.None);
        await ((IProvider)provider).Refresh(CancellationToken.None);
        return provider;
    }

    private static ResourceDictionary Load(string source) {
        return new ResourceDictionary {
            Source = new Uri(source, UriKind.Relative)
        };
    }

    /*
     * Shows the preview in a window that holds the resources of a theme of the application, so
     * that the resources of the theme are found before those of the fixture.
     */
    private void Attach(string theme) {
        Attach(window => {
            window.Resources.MergedDictionaries.Add(Load(
                $"/Brows.Framework.Windows;component/Resources/Themes/{theme}.FileExplorer.xaml"));
            window.Resources.MergedDictionaries.Add(Load(
                "/Brows.Framework.Windows;component/Resources/Resources.xaml"));
            window.Resources.MergedDictionaries.Add(Load(
                "/Brows.MBox.Windows;component/Resource/Resource.xaml"));
        });
    }

    private object Theme(string key) {
        return _Window.FindResource($"Brows.Theme.MBoxEntryPreview.{key}");
    }

    private object Font(string key) {
        return _Window.FindResource($"Brows.Font.MBoxEntryPreview.{key}");
    }

    private Border FindRoot() {
        return (Border)_Control.Template.FindName("Root", _Control);
    }

    private void Attach(Action<Window> prepare = null) {
        MBoxTestResources.Add();
        _Control = new MBoxEntryPreviewControl {
            ViewModel = _Detail
        };
        _ObservationControl = new EntryObservationControl {
            ViewModel = ((IEntryProvider)_Provider).Observation
        };
        _Window = new Window {
            Width = 800,
            Height = 600,
            Content = new Grid {
                RowDefinitions = {
                    new RowDefinition(),
                    new RowDefinition(),
                },
                Children = {
                    _ObservationControl,
                    _Control,
                }
            }
        };
        prepare?.Invoke(_Window);
        _Window.Show();
        ProcessDispatcher();
    }

    private void SelectCurrent(int index) {
        var observation = (IEntryObservation)((IEntryProvider)_Provider).Observation;
        var entry = observation.Observed[index];
        observation.Current(entry);
        ProcessDispatcher();
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

    [Test]
    public async Task Preview_ShowsTheBodyOfTheCurrentMessage() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Length > 0);
            Assert.That(_Control.Text, Does.Contain("First body."));
            Assert.That(_Control.Status, Is.Empty);
        });
    }

    [Test]
    public async Task Preview_ShowsNoSelectionWhenTheListHasNoCurrentMessage() {
        await Run(Mailbox(), async () => {
            Attach();
            await WaitFor(() => _Control.Status.Length > 0);
            Assert.That(_Control.Text, Is.Empty);
            Assert.That(_Control.Status, Is.EqualTo("No message is selected."));
        });
    }

    [Test]
    public async Task Preview_FollowsTheCurrentMessageOfTheList() {
        await Run(Mailbox(("First subject", "First body."),
                          ("Second subject", "Second body.")), async () => {
            Attach();
            SelectCurrent(1);
            await WaitFor(() => _Control.Text.Contains("Second body."));
            Assert.That(_Control.Text, Does.Contain("Second body."));
            Assert.That(_Control.Text, Does.Not.Contain("First body."));
        });
    }

    [Test]
    public async Task Preview_ShowsTheStatusInItsTemplate() {
        await Run(Mailbox(), async () => {
            Attach();
            await WaitFor(() => _Control.Status.Length > 0);
            Assert.That(_Control.StatusTextBlock.Text, Is.EqualTo("No message is selected."));
        });
    }

    [Test]
    public async Task Preview_ShowsTheTextInItsTemplate() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Contains("First body."));
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Control.BodyTextBox.Text, Does.Contain("First body."));
                Assert.That(_Control.StatusTextBlock.Text, Is.Empty);
            }
        });
    }

    [TestCase("Dark")]
    [TestCase("Light")]
    public async Task Preview_StylesTheBodyWithTheTheme(string theme) {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach(theme);
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Contains("First body."));
            var body = _Control.BodyTextBox;
            using (Assert.EnterMultipleScope()) {
                Assert.That(body.Background, Is.SameAs(Theme("Body.Background")));
                Assert.That(body.Foreground, Is.SameAs(Theme("Body.Foreground")));
                Assert.That(body.CaretBrush, Is.SameAs(Theme("Body.CaretBrush")));
                Assert.That(body.SelectionBrush, Is.SameAs(Theme("Body.SelectionBrush")));
                Assert.That(body.SelectionTextBrush, Is.SameAs(Theme("Body.SelectionTextBrush")));
                Assert.That(body.SelectionOpacity, Is.EqualTo(Theme("Body.SelectionOpacity")));
                Assert.That(body.BorderThickness, Is.EqualTo(new Thickness(0)));
                Assert.That(body.FontFamily, Is.SameAs(Font("FontFamily")));
                Assert.That(body.FontSize, Is.EqualTo(Font("FontSize")));
                Assert.That(body.FontWeight, Is.EqualTo(Font("FontWeight")));
            }
        });
    }

    [TestCase("Dark")]
    [TestCase("Light")]
    public async Task Preview_StylesTheStatusWithTheTheme(string theme) {
        await Run(Mailbox(), async () => {
            Attach(theme);
            await WaitFor(() => _Control.Status.Length > 0);
            var status = _Control.StatusTextBlock;
            var statusBar = (Border)_Control.Template.FindName("StatusBar", _Control);
            using (Assert.EnterMultipleScope()) {
                Assert.That(status.Foreground, Is.SameAs(Theme("Status.Foreground")));
                Assert.That(status.FontFamily, Is.SameAs(Font("Status.FontFamily")));
                Assert.That(status.FontSize, Is.EqualTo(Font("Status.FontSize")));
                Assert.That(statusBar.Background, Is.SameAs(Theme("Status.Background")));
                Assert.That(statusBar.BorderBrush, Is.SameAs(Theme("Status.BorderBrush")));
                Assert.That(FindRoot().Background, Is.SameAs(Theme("Background")));
            }
        });
    }

    private static string AttachmentRecord(string subject) {
        return "From sender@example.com Thu Sep 17 17:54:37 +0000 2015\n" +
               $"Subject: {subject}\n" +
               "Content-Type: multipart/mixed; boundary=\"mixed\"\n" +
               "\n" +
               "--mixed\n" +
               "Content-Type: text/plain; charset=utf-8\n" +
               "\n" +
               "Body with attachments.\n" +
               "--mixed\n" +
               "Content-Type: application/pdf\n" +
               "Content-Disposition: attachment; filename=\"report.pdf\"\n" +
               "Content-Transfer-Encoding: base64\n" +
               "\n" +
               "JVBERi0xLjQK\n" +
               "--mixed\n" +
               "Content-Type: image/png\n" +
               "Content-Disposition: attachment; filename=\"photo.png\"\n" +
               "Content-Transfer-Encoding: base64\n" +
               "\n" +
               "iVBORw0KGgo=\n" +
               "--mixed--\n";
    }

    private static List<T> FindVisuals<T>(DependencyObject root) where T : DependencyObject {
        var visuals = new List<T>();
        if (root is T visual) {
            visuals.Add(visual);
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            visuals.AddRange(FindVisuals<T>(VisualTreeHelper.GetChild(root, i)));
        }
        return visuals;
    }

    private T Part<T>(string name) where T : class {
        return _Control.Template.FindName(name, _Control) as T;
    }

    [TestCase("Dark")]
    [TestCase("Light")]
    public async Task Preview_ListsTheAttachmentsOfTheCurrentMessage(string theme) {
        await Run(AttachmentRecord("With attachments"), async () => {
            Attach(theme);
            SelectCurrent(0);
            await WaitFor(() => _Control.Attachments.Count == 2);
            ProcessDispatcher();
            var list = Part<ItemsControl>("PART_Attachments");
            var bar = Part<Border>("AttachmentBar");
            var label = Part<TextBlock>("PART_AttachmentsLabel");
            var texts = FindVisuals<TextBlock>(list);
            var names = texts.Where(text => text.Text is "report.pdf" or "photo.png").ToList();
            var types = texts.Where(text => text.Text is "application/pdf" or "image/png").ToList();
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Control.Text, Does.Contain("Body with attachments."));
                Assert.That(list.Items, Has.Count.EqualTo(2));
                Assert.That(bar.IsVisible, Is.True);
                Assert.That(label.Text, Is.EqualTo("Attachments"));
                Assert.That(names, Has.Count.EqualTo(2));
                Assert.That(types, Has.Count.EqualTo(2));
                Assert.That(names.Select(text => text.Foreground), Is.All.SameAs(Theme("Attachment.Foreground")));
                Assert.That(types.Select(text => text.Foreground),
                            Is.All.SameAs(Theme("Attachment.ContentType.Foreground")));
                Assert.That(label.Foreground, Is.SameAs(Theme("Attachments.Label.Foreground")));
                Assert.That(bar.Background, Is.SameAs(Theme("Attachments.Background")));
                Assert.That(bar.BorderBrush, Is.SameAs(Theme("Attachments.BorderBrush")));
                Assert.That(names[0].FontFamily, Is.SameAs(Font("Attachment.FontFamily")));
            }
        });
    }

    [Test]
    public async Task Preview_HidesTheAttachmentsWhenTheMessageHasNone() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach("Dark");
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Contains("First body."));
            ProcessDispatcher();
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Control.Attachments, Is.Empty);
                Assert.That(Part<Border>("AttachmentBar").IsVisible, Is.False);
            }
        });
    }

    [Test]
    public async Task Preview_ReplacesTheAttachmentsWhenTheCurrentMessageChanges() {
        var mailbox = AttachmentRecord("With attachments") + "\n" + Mailbox(("Plain subject", "Plain body."));
        await Run(mailbox, async () => {
            Attach("Dark");
            SelectCurrent(0);
            await WaitFor(() => _Control.Attachments.Count == 2);
            SelectCurrent(1);
            await WaitFor(() => _Control.Text.Contains("Plain body."));
            ProcessDispatcher();
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Control.Attachments, Is.Empty);
                Assert.That(Part<ItemsControl>("PART_Attachments").Items, Is.Empty);
                Assert.That(Part<Border>("AttachmentBar").IsVisible, Is.False);
            }
        });
    }

    [Test]
    public async Task Preview_ShowsTheStatusWhenThereIsOne() {
        await Run(Mailbox(), async () => {
            Attach("Dark");
            await WaitFor(() => _Control.Status.Length > 0);
            var statusBar = (Border)_Control.Template.FindName("StatusBar", _Control);
            Assert.That(statusBar.IsVisible, Is.True);
        });
    }

    [Test]
    public async Task Preview_HidesTheStatusWhenThereIsNone() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach("Dark");
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Contains("First body."));
            ProcessDispatcher();
            var statusBar = (Border)_Control.Template.FindName("StatusBar", _Control);
            using (Assert.EnterMultipleScope()) {
                Assert.That(_Control.Status, Is.Empty);
                Assert.That(statusBar.IsVisible, Is.False);
            }
        });
    }

    [TestCase("Dark")]
    [TestCase("Light")]
    public async Task Preview_MarksTheKeyboardFocus(string theme) {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach(theme);
            var unfocused = FindRoot().BorderBrush;
            Assert.That(_Control.RequestFocus(), Is.True);
            ProcessDispatcher();
            using (Assert.EnterMultipleScope()) {
                Assert.That(unfocused, Is.SameAs(Theme("BorderBrush")));
                Assert.That(FindRoot().BorderBrush, Is.SameAs(Theme("BorderBrush.Focus")));
                Assert.That(_Control.BodyTextBox.FocusVisualStyle, Is.Null);
            }
        });
    }

    [Test]
    public async Task Preview_IsTheControllerOfItsViewModel() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            Assert.That(_Detail.Controller, Is.SameAs(_Control));
        });
    }

    [Test]
    public async Task RequestFocus_MovesTheFocusToThePreview() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            var focused = _Control.RequestFocus();
            ProcessDispatcher();
            Assert.That(focused, Is.True);
            Assert.That(_Control.HasKeyboardFocus, Is.True);
        });
    }

    [Test]
    public async Task RequestFocusLater_MovesTheFocusToThePreview() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            var focused = false;
            _Control.BodyTextBox.GotKeyboardFocus += (_, _) => focused = true;
            _Control.RequestFocusLater(_Panel, _Panels, CancellationToken.None);
            ProcessDispatcher();
            Assert.That(focused, Is.True);
            Assert.That(_Control.HasKeyboardFocus, Is.True);
        });
    }

    [Test]
    public async Task Preview_AfterTheProviderIsEnded_KeepsTheTextOfTheLastMessage() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Length > 0);
            ((IProvider)_Provider).End();
            Assert.That(_Control.Text, Does.Contain("First body."));
        });
    }

    [Test]
    public async Task Preview_ShowsGuidanceForAnHtmlOnlyMessage() {
        var mailbox = "From sender@example.com Thu Sep 17 17:54:37 +0000 2015\n" +
                      "Subject: HTML only\n" +
                      "Content-Type: text/html; charset=utf-8\n" +
                      "\n" +
                      "<p>HTML body.</p>\n";
        await Run(mailbox, async () => {
            Attach();
            SelectCurrent(0);
            var guidance = "The message holds no plain text. Use MBox_Open or MBox_Export to read it.";
            await WaitFor(() => _Control.Status == guidance);
            Assert.That(_Control.Text, Is.Empty);
            Assert.That(_Control.Status, Is.EqualTo(guidance));
        });
    }

    [Test]
    public async Task Preview_ShowsGuidanceForAnHtmlOnlyAlternative() {
        var mailbox = "From sender@example.com Thu Sep 17 17:54:37 +0000 2015\n" +
                      "Subject: HTML alternative\n" +
                      "Content-Type: multipart/alternative; boundary=preview-boundary\n" +
                      "\n" +
                      "--preview-boundary\n" +
                      "Content-Type: text/html; charset=utf-8\n" +
                      "\n" +
                      "<p>HTML body.</p>\n" +
                      "--preview-boundary--\n";
        await Run(mailbox, async () => {
            Attach();
            SelectCurrent(0);
            var guidance = "The message holds no plain text. Use MBox_Open or MBox_Export to read it.";
            await WaitFor(() => _Control.Status == guidance);
            Assert.That(_Control.Text, Is.Empty);
        });
    }

    [Test]
    public async Task Preview_LeavesAnEmptyPlainTextMessageWithoutHtmlOnlyGuidance() {
        var mailbox = "From sender@example.com Thu Sep 17 17:54:37 +0000 2015\n" +
                      "Subject: Empty plain text\n" +
                      "Content-Type: text/plain; charset=utf-8\n" +
                      "\n";
        await Run(mailbox, async () => {
            Attach();
            var observation = (IEntryObservation)_Provider.Observation;
            var entry = _Provider.Entries[0];
            Assert.That(observation.Current(entry), Is.True);
            var result = await _Provider.Preview(entry, CancellationToken.None);
            await WaitFor(() => _Control.Status != "The message is being read.");
            ProcessDispatcher();
            Assert.That(result.State, Is.EqualTo(MBoxBodyState.Complete));
            Assert.That(result.Html, Is.Null);
            Assert.That(_Control.Text, Is.Empty);
            Assert.That(_Control.Status, Is.Empty);
        });
    }

    [Test]
    public async Task Preview_RebindsToTheProviderOfTheCurrentViewModel() {
        await Run(Mailbox(("First subject", "First provider body."),
                          ("Other subject", "Other first-provider body.")), async () => {
            Attach();
            SelectCurrent(0);
            _OtherProvider = await CreateProvider(
                "other.mbox",
                Mailbox(("Second subject", "Second provider body.")));
            _OtherObservationControl = new EntryObservationControl {
                ViewModel = ((IEntryProvider)_OtherProvider).Observation
            };
            ((Grid)_Window.Content).Children.Add(_OtherObservationControl);
            ProcessDispatcher();
            var otherDetail = new MBoxEntryPreview(_OtherProvider);
            _Control.ViewModel = otherDetail;
            Assert.That(((IEntryObservation)_OtherProvider.Observation).Current(_OtherProvider.Entries[0]), Is.True);
            await WaitFor(() => _Control.Text.Contains("Second provider body."));
            ((IEntryObservation)_Provider.Observation).Current(_Provider.Entries[1]);
            ProcessDispatcher();
            Assert.That(_Control.Text, Does.Contain("Second provider body."));
            Assert.That(_Control.Text, Does.Not.Contain("First provider body."));
        });
    }

    [Test]
    public async Task RequestFocusLater_DoesNotMoveFocusAfterThePreviewDetaches() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            var focusTarget = new Button { Content = "Focus target" };
            ((Grid)_Window.Content).Children.Add(focusTarget);
            Assert.That(focusTarget.Focus(), Is.True);
            var focused = false;
            _Control.BodyTextBox.GotKeyboardFocus += (_, _) => focused = true;
            _Control.RequestFocusLater(_Panel, _Panels, CancellationToken.None);
            _Control.ViewModel = null;
            ProcessDispatcher();
            Assert.That(focused, Is.False);
        });
    }

    [Test]
    public async Task RequestFocusLater_DoesNotMoveFocusAfterThePreviewRebinds() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            _OtherProvider = await CreateProvider(
                "other.mbox",
                Mailbox(("Second subject", "Second provider body.")));
            var focusTarget = new Button { Content = "Focus target" };
            ((Grid)_Window.Content).Children.Add(focusTarget);
            Assert.That(focusTarget.Focus(), Is.True);
            var focused = false;
            _Control.BodyTextBox.GotKeyboardFocus += (_, _) => focused = true;
            _Control.RequestFocusLater(_Panel, _Panels, CancellationToken.None);
            _Control.ViewModel = new MBoxEntryPreview(_OtherProvider);
            ProcessDispatcher();
            Assert.That(_Control.HasKeyboardFocus, Is.False);
            Assert.That(focused, Is.False);
        });
    }

    [Test]
    public async Task RequestFocusLater_DoesNotMoveFocusAfterPanelChanges() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            var focusTarget = new Button { Content = "Focus target" };
            ((Grid)_Window.Content).Children.Add(focusTarget);
            Assert.That(focusTarget.Focus(), Is.True);
            var focused = false;
            _Control.BodyTextBox.GotKeyboardFocus += (_, _) => focused = true;
            _Control.RequestFocusLater(_Panel, _Panels, CancellationToken.None);
            _Panels.Active = new TestPanel();
            ProcessDispatcher();
            Assert.That(focused, Is.False);
        });
    }

    [Test]
    public async Task RequestFocusLater_DoesNotMoveFocusAfterThePreviewIsReplaced() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            var focusTarget = new Button { Content = "Focus target" };
            ((Grid)_Window.Content).Children.Add(focusTarget);
            Assert.That(focusTarget.Focus(), Is.True);
            var focused = false;
            _Control.BodyTextBox.GotKeyboardFocus += (_, _) => focused = true;
            _Control.RequestFocusLater(_Panel, _Panels, CancellationToken.None);
            var replacement = new MBoxEntryPreview(_Provider);
            var shown = await _Provider.Detail.ShowAsync(MBoxViews.Preview, replacement, CancellationToken.None);
            Assert.That(shown, Is.True);
            ProcessDispatcher();
            Assert.That(focused, Is.False);
        });
    }

    [Test]
    public async Task RequestFocusLater_DoesNotMoveFocusAfterCancellation() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            var focusTarget = new Button { Content = "Focus target" };
            ((Grid)_Window.Content).Children.Add(focusTarget);
            Assert.That(focusTarget.Focus(), Is.True);
            var focused = false;
            _Control.BodyTextBox.GotKeyboardFocus += (_, _) => focused = true;
            using var source = new CancellationTokenSource();
            _Control.RequestFocusLater(_Panel, _Panels, source.Token);
            source.Cancel();
            ProcessDispatcher();
            Assert.That(focused, Is.False);
        });
    }

    [Test]
    public async Task Preview_ReattachesAfterUnloadAndReload() {
        await Run(Mailbox(("First subject", "First body.")), async () => {
            Attach();
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Contains("First body."));
            var container = (Grid)_Window.Content;
            container.Children.Remove(_Control);
            ProcessDispatcher();
            Assert.That(_Detail.Controller, Is.Null);
            container.Children.Add(_Control);
            ProcessDispatcher();
            Assert.That(_Detail.Controller, Is.SameAs(_Control));
            SelectCurrent(0);
            await WaitFor(() => _Control.Text.Contains("First body."));
        });
    }
}
