using Brows.Providers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryObservationTest {
    [Test]
    public async Task Add_AfterEnd_AddsNothing() {
        var provider = new TestProvider();
        var observation = new EntryObservation<TestEntry>(0, null, null) {
            Provider = provider,
        };
        observation.End();
        await observation.Add([new TestEntry(provider, "first")], CancellationToken.None);
        Assert.That(observation.Observed, Is.Empty);
    }

    [Test]
    public async Task Add_DuringEnd_AddsNothing() {
        var provider = new TestProvider();
        var observation = new EntryObservation<TestEntry>(0, null, null) {
            Provider = provider,
        };
        await provider.Initialize(CancellationToken.None);
        provider.ConfigureAdd(1, 50);
        var firstChunkAdded = new TaskCompletionSource();
        observation.ObservedChanged += (sender, e) => {
            firstChunkAdded.TrySetResult();
        };
        var entries = new[] {
            new TestEntry(provider, "first"),
            new TestEntry(provider, "second"),
        };
        var add = observation.Add(entries, CancellationToken.None);
        await firstChunkAdded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observation.End();
        await add;
        Assert.That(observation.Observed, Is.Empty);
    }

    [Test]
    public async Task Add_DuringEnd_RaisesNoObservedChanged() {
        var provider = new TestProvider();
        var observation = new EntryObservation<TestEntry>(0, null, null) {
            Provider = provider,
        };
        await provider.Initialize(CancellationToken.None);
        provider.ConfigureAdd(1, 50);
        var observedChanged = 0;
        var firstChunkAdded = new TaskCompletionSource();
        observation.ObservedChanged += (sender, e) => {
            Interlocked.Increment(ref observedChanged);
            firstChunkAdded.TrySetResult();
        };
        var entries = new[] {
            new TestEntry(provider, "first"),
            new TestEntry(provider, "second"),
        };
        var add = observation.Add(entries, CancellationToken.None);
        await firstChunkAdded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observation.End();
        var countAtEnd = Volatile.Read(ref observedChanged);
        await add;
        Assert.That(observedChanged, Is.EqualTo(countAtEnd));
    }

    [Test]
    public async Task Remove_AfterEnd_RemovesNothing() {
        var provider = new TestProvider();
        var observation = new EntryObservation<TestEntry>(0, null, null) {
            Provider = provider,
        };
        await provider.Initialize(CancellationToken.None);
        var entry = new TestEntry(provider, "first");
        await observation.Add([entry], CancellationToken.None);
        Assert.That(observation.Observed, Has.Count.EqualTo(1));
        observation.End();
        await observation.Remove([entry], CancellationToken.None);
        Assert.That(observation.Observed, Is.Empty);
    }

    [Test]
    public void End_ClearsTheEntries() {
        var provider = new TestProvider();
        var observation = new EntryObservation<TestEntry>(0, null, null) {
            Provider = provider,
        };
        provider.Initialize(CancellationToken.None).GetAwaiter().GetResult();
        observation.Add([new TestEntry(provider, "first")], CancellationToken.None).GetAwaiter().GetResult();
        observation.End();
        Assert.That(observation.Observed, Is.Empty);
    }

    [Test]
    public void End_Twice_ThrowsNothing() {
        var provider = new TestProvider();
        var observation = new EntryObservation<TestEntry>(0, null, null) {
            Provider = provider,
        };
        observation.End();
        Assert.That(() => observation.End(), Throws.Nothing);
    }

    [Test]
    public void Add_CollectionChangedFailure_StillInitializesTheEntry() {
        var provider = new TestProvider();
        var source = new EntryObservationSource();
        var entry = new TestEntry(provider, "first");
        var collection = (ObservableCollection<IEntry>)source.Items;
        NotifyCollectionChangedEventHandler changed = (_, _) => {
            throw new InvalidOperationException("Injected collection notification failure.");
        };
        collection.CollectionChanged += changed;
        Assert.That(() => source.Add(entry), Throws.InvalidOperationException);
        collection.CollectionChanged -= changed;
        entry.Select = true;
        using (Assert.EnterMultipleScope()) {
            Assert.That(source.Observation, Is.EqualTo(new[] { entry }));
            Assert.That(source.Selection, Does.Contain(entry));
        }
        source.End();
    }

    [Test]
    public void Add_SelectedChangedFailure_ContinuesAddingEveryEntry() {
        var provider = new TestProvider();
        var source = new EntryObservationSource();
        var first = new TestEntry(provider, "first") { Select = true };
        var second = new TestEntry(provider, "second") { Select = true };
        EventHandler changed = (_, _) => {
            throw new InvalidOperationException("Injected selection notification failure.");
        };
        source.SelectionChanged += changed;
        Assert.That(() => source.Add(first, second), Throws.InvalidOperationException);
        source.SelectionChanged -= changed;
        using (Assert.EnterMultipleScope()) {
            Assert.That(source.Observation, Is.EqualTo(new[] { first, second }));
            Assert.That(source.Selection, Does.Contain(first));
            Assert.That(source.Selection, Does.Contain(second));
        }
        source.End();
    }

    private sealed class TestProvider : Provider<TestEntry, TestConfig> {
        public TestProvider() : base("test") {
        }

        public Task Initialize(CancellationToken token) {
            return Init(token);
        }

        public void ConfigureAdd(int size, int delay) {
            Config.Observe.Add.Size = size;
            Config.Observe.Add.Delay = delay;
        }
    }

    private sealed class TestConfig : EntryConfig {
    }

    private sealed class TestEntry : Entry<TestProvider> {
        public TestEntry(TestProvider provider, string name) : base(provider) {
            Name = name;
        }

        public override string ID =>
            Name;

        public override string Name {
            get;
        }
    }
}
