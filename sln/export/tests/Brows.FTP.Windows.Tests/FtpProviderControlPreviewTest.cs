using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Brows.Providers;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
internal sealed class FtpProviderControlPreviewTest {
    private Application _Application;
    private ResourceDictionary _FrameworkResources;

    private static Control ProviderControl(string assemblyName, string typeName) {
        var assembly = Assembly.Load(assemblyName);
        var type = assembly.GetType($"Brows.{typeName}", throwOnError: true);
        return (Control)Activator.CreateInstance(type, nonPublic: true);
    }

    private static void AssertPreviewTemplate(Control providerControl, string detailName) {
        providerControl.ApplyTemplate();
        var grid = (ProviderGrid)VisualTreeHelper.GetChild(providerControl, 0);
        var key = grid.Resources.Keys
            .OfType<Type>()
            .Single(type => type.Name == detailName);
        var template = (DataTemplate)grid.Resources[key];
        var content = (ContentControl)template.LoadContent();
        var binding = BindingOperations.GetBinding(content, FrameworkElement.DataContextProperty);

        using (Assert.EnterMultipleScope()) {
            Assert.That(binding?.Path?.Path, Is.EqualTo("Provider.Observation.Current.Stream"));
            Assert.That(content.Style, Is.Not.Null);
            Assert.That(content.Style.Resources.Contains("TextControl"), Is.True);
            Assert.That(content.Style.Resources.Contains("ImageControl"), Is.True);
        }
    }

    [SetUp]
    public void SetUp() {
        _Application = Application.Current ?? new Application {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
        _FrameworkResources = new ResourceDictionary {
            Source = new Uri("/Brows.Framework.Windows;component/Resource/Resource.xaml", UriKind.Relative)
        };
        _Application.Resources.MergedDictionaries.Add(_FrameworkResources);
    }

    [TearDown]
    public void TearDown() {
        var resourcesWereAdded = _Application is not null && _FrameworkResources is not null;
        if (resourcesWereAdded) {
            _Application.Resources.MergedDictionaries.Remove(_FrameworkResources);
        }
        _FrameworkResources = null;
        _Application = null;
    }

    [Test]
    public void FtpProviderControl_RegistersASelectionBoundRemoteStreamPreviewTemplate() {
        AssertPreviewTemplate(ProviderControl("Brows.FTP.Windows", "FtpProviderControl"), "FtpEntryPreview");
    }

    [Test]
    public void FtpsProviderControl_RegistersASelectionBoundRemoteStreamPreviewTemplate() {
        AssertPreviewTemplate(ProviderControl("Brows.FTPS.Windows", "FtpsProviderControl"), "FtpEntryPreview");
    }
}
