using NUnit.Framework;

namespace Brows.Cli {
    [TestFixture]
    public class CommandHelperTest {
        private CommandHelper Subject {
            get => _Subject ?? (_Subject = new CommandHelper());
            set => _Subject = value;
        }
        private CommandHelper _Subject;

        [SetUp]
        public void SetUp() {
            Subject = null;
        }

        class OneArgumentCommand {
            [CommandArgument]
            public string Path { get; set; }
        }

        [Test]
        public void Help_OneArgumentCommand() {
            var help = Subject.Help(typeof(OneArgumentCommand));
            Assert.That(help.HelpLine, Is.EqualTo("[Path]"));
        }

        class TwoArgumentCommand : OneArgumentCommand {
            [CommandArgument(Name = "cnt", Required = true)]
            public int Count { get; set; }
        }

        [Test]
        public void Help_TwoArgumentCommand() {
            var help = Subject.Help(typeof(TwoArgumentCommand));
            Assert.That(help.HelpLine, Is.EqualTo("cnt<int> [Path]"));
        }

        class TwoArgumentCommandWithSwitch : TwoArgumentCommand {
            [CommandSwitch]
            public string SomeOption { get; set; }
        }

        [Test]
        public void Help_TwoArgumentCommandWithSwitch() {
            var help = Subject.Help(typeof(TwoArgumentCommandWithSwitch));
            Assert.That(help.HelpLine, Is.EqualTo("cnt<int> [Path] [--SomeOption <string>]"));
        }

        class TwoArgumentCommandWith2Switches : TwoArgumentCommandWithSwitch {
            [CommandSwitch(Required = true, Name = "anotheropt")]
            public bool AnotherOption { get; set; }
        }

        [Test]
        public void Help_TwoArgumentCommandWith2Switches() {
            var help = Subject.Help(typeof(TwoArgumentCommandWith2Switches));
            Assert.That(help.HelpLine, Is.EqualTo("cnt<int> [Path] --anotheropt [--SomeOption <string>]"));
        }
    }
}
