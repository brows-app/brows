using System;
using System.Windows;
using System.Windows.Media;

namespace Brows;

/// <summary>
/// Adds the resources that the controls of the feature need, so that a test can show them.
/// </summary>
/// <remarks>
/// The brushes of the theme of the application live in an application project, so the test
/// defines the ones that the styles of the framework need.
/// </remarks>
internal static class MBoxTestResources {
    private static bool _Added;

    public static void Add() {
        if (_Added) {
            return;
        }
        var application = Application.Current ?? new Application {
            /*
             * A fixture shows a window of its own in every test, so the application must not
             * shut down when the last of them closes.
             */
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
        application.Resources["Brows.Theme.Foreground"] = new SolidColorBrush(Colors.Black) {
            Opacity = 0.9
        };
        /*
         * The list of the messages uses the styles of the framework, and the preview uses the
         * styles of the feature, so both dictionaries are needed.
         */
        application.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri(
                "/Brows.Framework.Windows;component/Resource/Resource.xaml",
                UriKind.Relative)
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri(
                "/Brows.MBox.Windows;component/Resource/Resource.xaml",
                UriKind.Relative)
        });
        _Added = true;
    }
}
