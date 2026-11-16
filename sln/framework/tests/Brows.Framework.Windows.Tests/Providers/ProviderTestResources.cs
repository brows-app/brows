using System;
using System.Windows;

namespace Brows.Providers;

internal static class ProviderTestResources {
    private static readonly Uri ResourcesUri = new(
        "pack://application:,,,/Brows.Framework.Windows;component/Resources/Resources.xaml");

    internal static void MergeInto(FrameworkElement element) {
        element.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = ResourcesUri
        });
    }
}
