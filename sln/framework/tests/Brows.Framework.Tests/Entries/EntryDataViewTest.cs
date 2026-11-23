using Brows.Providers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Entries;

[TestFixture]
internal sealed class EntryDataViewTest {
    private sealed class TestProvider : Provider<TestEntry, AutoFitTestConfig> {
        internal AutoFitTestConfig Settings => Config;
        public TestProvider() : base("auto-fit-test") {
        }
    }

    private sealed class TestEntry : Entry<TestProvider> {
        public override string ID => "test";
        public TestEntry(TestProvider provider) : base(provider) {
        }
    }

    private sealed class AutoFitTestConfig : EntryConfig {
    }

    private sealed class Controller : IEntryObservationController {
        internal List<string> Calls { get; } = [];
        public event EventHandler ManualInteraction { add { } remove { } }
        public event EventHandler NothingSelected { add { } remove { } }
        public event EventHandler DraggingSelected { add { } remove { } }
        public event EventHandler CurrentEntryChanged { add { } remove { } }
        public bool AutoFit { set => Calls.Add($"AutoFit={value}"); }
        public object DraggingSource => null;
        public void Sort(IEntrySorting sorting) {
        }
        public void ClearData() {
            Calls.Add("Clear");
        }
        public bool Focus() {
            throw new NotSupportedException();
        }
        public bool Focused() {
            throw new NotSupportedException();
        }
        public bool AddData(IEntryDataDefinition definition) {
            throw new NotSupportedException();
        }
        public bool HasData(string key) {
            throw new NotSupportedException();
        }
        public bool RemoveData(string key) {
            throw new NotSupportedException();
        }
        public bool SizeToFit(string key) {
            throw new NotSupportedException();
        }
        public Task<bool> CurrentEntry(IEntry entry, CancellationToken token) {
            throw new NotSupportedException();
        }
        public bool CurrentEntry(IEntry entry) {
            throw new NotSupportedException();
        }
        public IEntry CurrentEntry() {
            throw new NotSupportedException();
        }
        public int CurrentPosition() {
            throw new NotSupportedException();
        }
        public bool CurrentPosition(int value) {
            throw new NotSupportedException();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Refresh_AppliesProviderSettingBeforeRebuildingColumns(bool enabled) {
        await using var provider = new TestProvider();
        await provider.Init(CancellationToken.None);
        provider.Settings.AutoFit = enabled;
        var view = new EntryDataView(provider, new EntryObservationSource());
        await view.Init(CancellationToken.None);
        var controller = new Controller();
        view.Controller = controller;
        Assert.That(controller.Calls, Is.EqualTo(new[] { $"AutoFit={enabled}", "Clear" }));
        provider.Settings.AutoFit = !enabled;
        controller.Calls.Clear();
        view.Refresh();
        Assert.That(controller.Calls, Is.EqualTo(new[] { $"AutoFit={!enabled}", "Clear" }));
    }
}
