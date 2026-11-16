using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Brows;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class CommanderManualControlTest {
    private static IReadOnlyList<Type> NoticeTypes => field ??= typeof(CommanderManualControl).Assembly
        .GetTypes()
        .Where(type => type.Namespace is "Brows.Notices" or "Brows.Licenses")
        .Where(type => typeof(UserControl).IsAssignableFrom(type))
        .ToList();

    private static CommanderManualControl CreateControl() {
        _ = Application.Current ?? new Application();
        var control = new CommanderManualControl();
        control.Resources.MergedDictionaries.Add(new ResourceDictionary {
            Source = new Uri(
                "/Brows.Commands.Manual.Windows;component/Resource/Culture/_.xaml",
                UriKind.Relative)
        });
        control.Measure(new Size(800, 600));
        control.Arrange(new Rect(0, 0, 800, 600));
        control.UpdateLayout();
        return control;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject element) where T : DependencyObject {
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>()) {
            if (child is T match) {
                yield return match;
            }
            foreach (var descendant in Descendants<T>(child)) {
                yield return descendant;
            }
        }
    }

    private static List<object> Items(CommanderManualControl control) {
        var itemsControl = Descendants<ItemsControl>(control).Single();
        return itemsControl.Items.Cast<object>().ToList();
    }

    [Test]
    public void Items_IncludeEveryNoticeOnce() {
        var noticeTypes = NoticeTypes;
        var itemTypes = Items(CreateControl()).Select(item => item.GetType()).ToList();

        Assert.That(noticeTypes, Is.Not.Empty);
        Assert.That(itemTypes.Where(noticeTypes.Contains), Is.EquivalentTo(noticeTypes));
    }

    [Test]
    public void Items_WrapNoticeText() {
        var notices = Items(CreateControl()).OfType<UserControl>().ToList();
        var textBlocks = notices.SelectMany(Descendants<TextBlock>).ToList();

        Assert.That(notices, Has.Count.EqualTo(NoticeTypes.Count));
        Assert.That(textBlocks, Is.Not.Empty);
        Assert.That(textBlocks.Select(textBlock => textBlock.TextWrapping), Is.All.EqualTo(TextWrapping.Wrap));
    }
}
