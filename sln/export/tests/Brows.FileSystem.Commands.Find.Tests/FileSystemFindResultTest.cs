using Brows.Commands;
using Brows.Gui;
using Brows.Support;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;

namespace Brows;

[TestFixture]
public sealed class FileSystemFindResultTest {
    private static FileSystemFindResult Result(DirectoryInfo root) {
        return new FileSystemFindResult(root, new FindParameter { Pattern = "*" }, "find", null);
    }

    [Test]
    public void Add_RaisesOneResetForTheWholeBatch() {
        using var fixture = new TempDirectory();
        var first = fixture.CreateFile("first.txt");
        var second = fixture.CreateFile("second.txt");
        var result = Result(fixture.Root);
        var changes = new List<NotifyCollectionChangedEventArgs>();
        var rows = (ObservableCollection<FoundInInfo>)result.Source;
        rows.CollectionChanged += (_, change) => changes.Add(change);

        result.Add(new FoundInInfo[] {
            new(first, fixture.Root),
            new(second, fixture.Root)
        });

        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(changes, Has.Count.EqualTo(1));
        Assert.That(changes[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Reset));
    }

    [Test]
    public void Controller_AttachmentSynchronizesAndClearsSelection() {
        using var fixture = new TempDirectory();
        var first = new FoundInInfo(fixture.CreateFile("first.txt"), fixture.Root);
        var second = new FoundInInfo(fixture.CreateFile("second.txt"), fixture.Root);
        var third = new FoundInInfo(fixture.CreateFile("third.txt"), fixture.Root);
        var result = Result(fixture.Root);
        var controlled = (IControlled<IFileSystemFindResultController>)result;
        var original = new TestController(first);
        var replacement = new TestController(second);

        controlled.Controller = original;
        Assert.That(result.CurrentItem, Is.SameAs(first));

        controlled.Controller = replacement;
        Assert.That(result.CurrentItem, Is.SameAs(second));

        replacement.SetCurrentItem(third);
        Assert.That(result.CurrentItem, Is.SameAs(third));

        controlled.Controller = null;
        Assert.That(result.CurrentItem, Is.Null);

        original.RaiseCurrentChanged();
        Assert.That(result.CurrentItem, Is.Null);
    }

    [Test]
    public void Exception_UpdatesFailureState() {
        using var fixture = new TempDirectory();
        var result = Result(fixture.Root);

        result.Exception = new IOException("The root could not be read.");
        Assert.That(result.Failed, Is.True);

        result.Exception = null;
        Assert.That(result.Failed, Is.False);
    }

    private sealed class TestController : IFileSystemFindResultController {
        public event EventHandler Loaded {
            add { }
            remove { }
        }
        public event EventHandler CurrentChanged;

        public FoundInInfo CurrentItem { get; private set; }

        public TestController(FoundInInfo currentItem) {
            CurrentItem = currentItem;
        }

        public void RaiseCurrentChanged() {
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetCurrentItem(FoundInInfo currentItem) {
            CurrentItem = currentItem;
            RaiseCurrentChanged();
        }
    }
}
