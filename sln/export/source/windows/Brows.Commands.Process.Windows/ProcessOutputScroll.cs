using System.Windows;
using System.Windows.Controls;

namespace Brows;

internal static class ProcessOutputScroll {
    /*
     * The distance from the end, in pixels, within which the viewer counts as scrolled to the end.
     */
    private const double EndTolerance = 2;

    private static readonly DependencyProperty FollowingProperty = DependencyProperty.RegisterAttached(
        name: "Following",
        propertyType: typeof(bool),
        ownerType: typeof(ProcessOutputScroll),
        defaultMetadata: new PropertyMetadata(true));

    private static void OnFollowsEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is not ScrollViewer viewer) {
            return;
        }
        viewer.ScrollChanged -= ScrollViewer_ScrollChanged;
        viewer.DataContextChanged -= ScrollViewer_DataContextChanged;
        if (e.NewValue is true) {
            viewer.ScrollChanged += ScrollViewer_ScrollChanged;
            viewer.DataContextChanged += ScrollViewer_DataContextChanged;
        }
    }

    private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
        if (sender is not ScrollViewer viewer) {
            return;
        }
        if (e.OriginalSource != viewer) {
            return;
        }
        /*
         * When neither the content nor the viewport changed size, the offset moved on its own, by
         * the user or by a scroll to the end. Follow the output only while the offset is at the end.
         * Otherwise, the output grew or the viewport resized, so keep the end in view if following.
         */
        var offsetMovedOnItsOwn = e.ExtentHeightChange == 0 && e.ViewportHeightChange == 0;
        if (offsetMovedOnItsOwn) {
            var atEnd = viewer.VerticalOffset >= viewer.ScrollableHeight - EndTolerance;
            viewer.SetValue(FollowingProperty, atEnd);
            return;
        }
        if ((bool)viewer.GetValue(FollowingProperty)) {
            viewer.ScrollToEnd();
        }
    }

    private static void ScrollViewer_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        if (sender is not ScrollViewer viewer) {
            return;
        }
        /*
         * The output of a different process is displayed, so start at its latest output.
         */
        viewer.SetValue(FollowingProperty, true);
        viewer.ScrollToEnd();
    }

    public static readonly DependencyProperty FollowsEndProperty = DependencyProperty.RegisterAttached(
        name: "FollowsEnd",
        propertyType: typeof(bool),
        ownerType: typeof(ProcessOutputScroll),
        defaultMetadata: new PropertyMetadata(
            defaultValue: false,
            propertyChangedCallback: OnFollowsEndChanged));

    public static bool GetFollowsEnd(ScrollViewer viewer) {
        ArgumentNullException.ThrowIfNull(viewer);
        return (bool)viewer.GetValue(FollowsEndProperty);
    }

    public static void SetFollowsEnd(ScrollViewer viewer, bool value) {
        ArgumentNullException.ThrowIfNull(viewer);
        viewer.SetValue(FollowsEndProperty, value);
    }
}
