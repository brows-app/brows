using System;
using System.Windows;

namespace Brows.Gui {
    public static class UIElementExtension {
        private static readonly Point Origin = new Point(0, 0);

        public static bool IsInView(this UIElement uiElement, UIElement ancestor) {
            if (uiElement is null) throw new ArgumentNullException(nameof(uiElement));
            if (uiElement.IsVisible == false) {
                return false;
            }
            var transform = uiElement.TransformToAncestor(ancestor);
            var rect = new Rect(Origin, uiElement.RenderSize);
            var bounds = transform.TransformBounds(rect);
            var viewPort = new Rect(Origin, ancestor.RenderSize);
            return viewPort.IntersectsWith(bounds);
        }
    }
}
