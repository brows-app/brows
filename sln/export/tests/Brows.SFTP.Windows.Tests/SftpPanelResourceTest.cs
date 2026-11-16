using Brows.Composition;
using Brows.Data;
using Brows.Entries;
using Brows.Providers;
using Brows.Windows;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class SftpPanelResourceTest {
    private static Application App => field ??= System.Windows.Application.Current ??
        new System.Windows.Application {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

    private static TextBlock FindTextBlock(DependencyObject root) {
        if (root is TextBlock textBlock) {
            return textBlock;
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            var found = FindTextBlock(VisualTreeHelper.GetChild(root, i));
            if (found is not null) {
                return found;
            }
        }
        return null;
    }

    private static void Render(DataTemplate template, object value, string expected) {
        var window = new Window {
            Width = 480,
            Height = 120,
            ShowInTaskbar = false,
            Content = new ContentControl {
                Content = value,
                ContentTemplate = template
            }
        };
        try {
            window.Show();
            DispatcherTools.Pump(window.Dispatcher);
            window.UpdateLayout();
            var text = FindTextBlock(window);
            Assert.That(text?.Text, Is.EqualTo(expected));
        }
        finally {
            window.Close();
        }
    }

    [Test]
    public async Task Resources_DiscoverBothPanelTemplatesAndRenderFirstConnectionEntries() {
        _ = App;
        await ImportSandbox.Enter([new Brows.Resource.Resource()], () => {
            var components = AppComponentCollection.From(Imports.Current);
            var dictionaries = components.ResourceDictionaries.ToArray();
            var frameworkResources = new ResourceDictionary {
                Source = new Uri("/Brows.Framework.Windows;component/Resource/Resource.xaml", UriKind.Relative)
            };
            App.Resources.MergedDictionaries.Add(frameworkResources);

            try {
                foreach (var dictionary in dictionaries) {
                    App.Resources.MergedDictionaries.Add(dictionary);
                }
                var component = components.Single(item => item.ReferencedAssembly == "Brows.SFTP.Windows");
                var resourceDictionary = dictionaries.Single();
                var panelSelector = AppComponent.DataTemplateSelector["PanelProvider"];
                var sftpProvider = (SftpProvider)RuntimeHelpers.GetUninitializedObject(typeof(SftpProvider));
                var factory = new SftpProviderFactory();
                var uri = new Uri("sftp://user@example.test/");
                using var lease = factory.Acquire(uri);
                using var connectionProvider = new SftpConnectionProvider(factory, uri, null, lease);
                var import = new Mock<IImportAgent>();
                import.Setup(value => value.List<IEntryDataExport>(It.IsAny<bool>())).Returns([
                    new SftpConnectionEntryData.Kind(),
                    new SftpConnectionEntryData.Order(),
                    new SftpConnectionEntryData.Data()
                ]);
                typeof(Provider).GetProperty("Import", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(connectionProvider, import.Object);
                var sftpTemplate = panelSelector.SelectTemplate(sftpProvider, null);
                var connectionTemplate = panelSelector.SelectTemplate(connectionProvider, null);

                using (Assert.EnterMultipleScope()) {
                    Assert.That(resourceDictionary["PanelProvider_SftpProvider"], Is.SameAs(sftpTemplate));
                    Assert.That(resourceDictionary["PanelProvider_SftpConnectionProvider"],
                                Is.SameAs(connectionTemplate));
                    Assert.That(sftpTemplate.LoadContent().GetType().Name, Is.EqualTo("SftpProviderControl"));
                    Assert.That(connectionTemplate.LoadContent().GetType().Name,
                                Is.EqualTo("SftpConnectionProviderControl"));
                    Assert.That(App.TryFindResource("SftpEntry_Name"), Is.InstanceOf<DataTemplate>());
                    Assert.That(App.TryFindResource("SftpConnectionEntry_Kind"), Is.InstanceOf<DataTemplate>());
                    Assert.That(App.TryFindResource("SftpConnectionEntry_Data"), Is.InstanceOf<DataTemplate>());
                }

                var fingerprint = new SftpConnectionEntry(
                    connectionProvider,
                    0,
                    "Host key fingerprint",
                    "SHA256:example-fingerprint");
                var approval = new SftpConnectionEntry(
                    connectionProvider,
                    1,
                    "Trust first host key after checking the fingerprint",
                    SftpKnownHostStatus.NotFound,
                    _ => Task.FromResult(true));
                var kindTemplate = (DataTemplate)App.TryFindResource("SftpConnectionEntry_Kind");
                var dataTemplate = (DataTemplate)App.TryFindResource("SftpConnectionEntry_Data");

                using (Assert.EnterMultipleScope()) {
                    Assert.That(fingerprint.Action, Is.Null);
                    Assert.That(fingerprint.Data, Is.EqualTo("SHA256:example-fingerprint"));
                    Assert.That(approval.Action, Is.Not.Null);
                    Assert.That(approval.Data, Is.EqualTo(SftpKnownHostStatus.NotFound));
                    Render(kindTemplate, fingerprint, fingerprint.Kind);
                    Render(dataTemplate, fingerprint, "SHA256:example-fingerprint");
                    Render(kindTemplate, approval, approval.Kind);
                    Render(dataTemplate, approval, "NotFound");
                }
            }
            finally {
                foreach (var dictionary in dictionaries) {
                    App.Resources.MergedDictionaries.Remove(dictionary);
                }
                App.Resources.MergedDictionaries.Remove(frameworkResources);
            }

            return Task.CompletedTask;
        });
    }
}
