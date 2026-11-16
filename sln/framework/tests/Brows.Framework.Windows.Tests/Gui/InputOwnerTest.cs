using System.Windows;
using System.Windows.Controls;

namespace Brows.Gui;

[TestFixture]
internal sealed class InputOwnerTest {
    [Test]
    public void Owner_InheritsWithinMarkedSubtreeWithoutAffectingSiblings() {
        DispatcherTools.Run(_ => {
            var owner = new object();
            var markedChild = new Border();
            var markedParent = new StackPanel();
            markedParent.Children.Add(markedChild);
            var sibling = new Border();
            var root = new StackPanel();
            root.Children.Add(markedParent);
            root.Children.Add(sibling);

            InputOwner.SetOwner(markedParent, owner);

            using (Assert.EnterMultipleScope()) {
                Assert.That(InputOwner.GetOwner(markedChild), Is.SameAs(owner));
                Assert.That(InputOwner.GetOwner(sibling), Is.Null);
                Assert.That(InputOwner.GetOwner(root), Is.Null);
            }
        });
    }

    [Test]
    public void Owner_ExplicitNullBlocksInheritanceUntilLocalValueIsCleared() {
        DispatcherTools.Run(_ => {
            var owner = new object();
            var child = new Border();
            var parent = new StackPanel();
            parent.Children.Add(child);
            InputOwner.SetOwner(parent, owner);

            InputOwner.SetOwner(child, null);

            Assert.That(InputOwner.GetOwner(child), Is.Null);
            Assert.That(InputOwner.GetOwner(parent), Is.SameAs(owner));

            child.ClearValue(InputOwner.OwnerProperty);

            Assert.That(InputOwner.GetOwner(child), Is.SameAs(owner));
        });
    }

    [Test]
    public void Owner_AncestorReplacementUpdatesDescendantsWithoutOverwritingLocalOwner() {
        DispatcherTools.Run(_ => {
            var first = new object();
            var replacement = new object();
            var localOwner = new object();
            var inheritedChild = new Border();
            var localChild = new Border();
            var parent = new StackPanel();
            parent.Children.Add(inheritedChild);
            parent.Children.Add(localChild);
            InputOwner.SetOwner(parent, first);
            InputOwner.SetOwner(localChild, localOwner);

            InputOwner.SetOwner(parent, replacement);

            Assert.That(InputOwner.GetOwner(inheritedChild), Is.SameAs(replacement));
            Assert.That(InputOwner.GetOwner(localChild), Is.SameAs(localOwner));
        });
    }
}
