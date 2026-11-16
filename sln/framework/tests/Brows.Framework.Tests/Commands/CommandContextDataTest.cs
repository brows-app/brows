using System.Collections.Generic;
using System.Linq;

namespace Brows.Commands;

[TestFixture]
internal sealed class CommandContextDataTest {
    [Test]
    public void Remove_StaleIndex_ReturnsNull() {
        var list = new List<string> { "one", "two" };
        var data = new Data(Mock.Of<ICommand>(), index: 5, list);

        /*
         * The backing list shrank after the data was created, so the index no
         * longer exists. Removing must not throw and must end the chain.
         */
        list.RemoveAt(0);

        Assert.That(data.Remove(), Is.Null);
        Assert.That(list, Is.EqualTo(new[] { "two" }));
    }

    [Test]
    public void Remove_NegativeIndex_ReturnsNull() {
        var list = new List<string> { "one" };
        var data = new Data(Mock.Of<ICommand>(), index: -1, list);

        Assert.That(data.Remove(), Is.Null);
        Assert.That(list, Is.EqualTo(new[] { "one" }));
    }

    [Test]
    public void Remove_ValidIndex_RemovesAndReturnsNext() {
        var list = new List<string> { "one", "two", "three" };
        var data = new Data(Mock.Of<ICommand>(), index: 1, list);

        var next = data.Remove();

        Assert.That(list, Is.EqualTo(new[] { "one", "three" }));
        Assert.That(next, Is.InstanceOf<Data>());
        Assert.That(((Data)next).Index, Is.EqualTo(1));
        Assert.That(((Data)next).Item, Is.EqualTo("three"));
    }

    [Test]
    public void Previous_EmptyList_ReturnsNull() {
        var list = new List<string>();
        var data = new Data(Mock.Of<ICommand>(), index: 0, list);

        /*
         * An empty list has no previous item. Returning a data with a negative
         * index would make the next Item access evaluate List[-1] and throw.
         */
        Assert.That(data.Previous(), Is.Null);
    }

    [Test]
    public void Previous_WrapsAroundToLast() {
        var list = new List<string> { "one", "two", "three" };
        var data = new Data(Mock.Of<ICommand>(), index: 0, list);

        var previous = data.Previous();

        Assert.That(previous, Is.InstanceOf<Data>());
        Assert.That(((Data)previous).Index, Is.EqualTo(2));
        Assert.That(((Data)previous).Item, Is.EqualTo("three"));
    }

    [Test]
    public void Next_EmptyList_WrapsWithoutThrowing() {
        var list = new List<string>();
        var data = new Data(Mock.Of<ICommand>(), index: 0, list);

        var next = data.Next();

        Assert.That(next, Is.InstanceOf<Data>());
        Assert.That(((Data)next).Item, Is.Null);
    }

    private sealed class Data : CommandContextData<string> {
        public Data(ICommand command, int index, IList<string> list) : base(command, index, list) {
        }

        protected override CommandContextData<string> Create(int index, IList<string> list) {
            return new Data(Mock.Of<ICommand>(), index, list);
        }
    }
}
