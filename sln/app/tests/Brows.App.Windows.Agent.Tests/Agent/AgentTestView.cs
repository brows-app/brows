using Brows.Windows.Controls;
using System;
using System.Windows;
using System.Windows.Media;

namespace Brows.Agent;

/*
 * A Brows view that shows one child, used to give agent test windows the structure of Brows windows.
 */
internal sealed class AgentTestView : ModelView<object> {
    private UIElement Child { get; }

    protected sealed override int VisualChildrenCount => 1;

    protected sealed override Visual GetVisualChild(int index) {
        if (index != 0) {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The view has one child.");
        }
        return Child;
    }

    protected sealed override Size MeasureOverride(Size constraint) {
        Child.Measure(constraint);
        return Child.DesiredSize;
    }

    protected sealed override Size ArrangeOverride(Size arrangeBounds) {
        Child.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    public AgentTestView(UIElement child) : base(populateImports: false) {
        Child = child ?? throw new ArgumentNullException(nameof(child));
        AddVisualChild(Child);
    }
}
