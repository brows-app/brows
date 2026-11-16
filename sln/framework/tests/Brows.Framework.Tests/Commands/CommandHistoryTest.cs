using System.Linq;

namespace Brows.Commands;

[TestFixture]
internal sealed class CommandHistoryTest {
    [Test]
    public void Add_AddedLines_MostRecentFirst() {
        var history = new CommandHistory();
        history.Add(new TestLine("a"));
        history.Add(new TestLine("b"));
        history.Add(new TestLine("c"));

        Assert.That(history.AsEnumerable().Cast<TestLine>().Select(line => line.Input), Is.EqualTo(new[] { "c", "b", "a" }));
    }

    [Test]
    public void Add_ReExecutedLine_MovesToFront() {
        var history = new CommandHistory();
        history.Add(new TestLine("a"));
        history.Add(new TestLine("b"));
        history.Add(new TestLine("c"));

        /*
         * Re-executing "a" must move it to the front, not leave it in its
         * original slot.
         */
        history.Add(new TestLine("a"));

        Assert.That(history.AsEnumerable().Cast<TestLine>().Select(line => line.Input), Is.EqualTo(new[] { "a", "c", "b" }));
    }

    [Test]
    public void Add_SameContentLine_ReplacedNotDuplicated() {
        var history = new CommandHistory();
        history.Add(new TestLine("a"));
        history.Add(new TestLine("b"));
        history.Add(new TestLine("a", conf: "conf"));

        /*
         * Entries are keyed by input and conf content, so the same input with
         * a different conf is a separate entry, but the same input and conf
         * appears only once.
         */
        history.Add(new TestLine("a"));

        Assert.That(history.AsEnumerable().Cast<TestLine>().Select(line => line.Input), Is.EqualTo(new[] { "a", "a", "b" }));
        Assert.That(history.AsEnumerable().Cast<TestLine>().Select(line => line.Conf), Is.EqualTo(new[] { null, "conf", null }));
    }

    private sealed class TestLine : ICommandLine {
        public string Input { get; }
        public string Conf { get; }

        public TestLine(string input, string conf = null) {
            Input = input;
            Conf = conf;
        }

        public bool HasInput(out string input) {
            input = Input;
            return input is not null;
        }

        public bool HasCommand(out string command) {
            command = null;
            return false;
        }

        public bool HasTrigger(out string trigger) {
            trigger = null;
            return false;
        }

        public bool HasParameter(out string parameter) {
            parameter = null;
            return false;
        }

        public bool HasConf(out string conf) {
            conf = Conf;
            return conf is not null;
        }
    }
}
