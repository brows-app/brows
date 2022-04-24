using NUnit.Framework;

namespace Brows.Cli.Tests {
    [TestFixture]
    public class CommandParserTest {
        private CommandParser Subject {
            get => _Subject ?? (_Subject = new CommandParser());
            set => _Subject = value;
        }
        private CommandParser _Subject;

        [SetUp]
        public void SetUp() {
            Subject = null;
        }

        class OneArgumentCommand {
            [CommandArgument]
            public string SomeParameter { get; set; }
        }

        [Test]
        public void Parse_SetsOneArgument() {
            var obj = new OneArgumentCommand();
            Subject.Parse("thevalue", obj);
            Assert.That(obj.SomeParameter, Is.EqualTo("thevalue"));
        }

        [Test]
        public void Parse_SetsOneQuotedArgumentWithSingleQuotes() {
            var obj = new OneArgumentCommand();
            Subject.Parse("'This is the value'", obj);
            Assert.That(obj.SomeParameter, Is.EqualTo("This is the value"));
        }

        [Test]
        public void Parse_SetsOneQuotedArgumentWithDoubleQuotes() {
            var obj = new OneArgumentCommand();
            Subject.Parse("\"This is the value\"", obj);
            Assert.That(obj.SomeParameter, Is.EqualTo("This is the value"));
        }

        [Test]
        public void Parse_SetsOneQuotedArgumentWithNestedQuotes() {
            var obj = new OneArgumentCommand();
            Subject.Parse("'This is \"the\" value'", obj);
            Assert.That(obj.SomeParameter, Is.EqualTo("This is \"the\" value"));
        }

        [Test]
        public void Parse_SetsOneQuotedArgumentWithReverseNestedQuotes() {
            var obj = new OneArgumentCommand();
            Subject.Parse("\"This is 'the' value\"", obj);
            Assert.That(obj.SomeParameter, Is.EqualTo("This is 'the' value"));
        }

        [Test]
        public void Parse_SetsOneQuotedArgumentThatLooksLikeASwitch() {
            var obj = new OneArgumentCommand();
            Subject.Parse("'--notaswitch'", obj);
            Assert.That(obj.SomeParameter, Is.EqualTo("--notaswitch"));
        }

        class TwoArgumentOrderedCommand {
            [CommandArgument(Order = 0)]
            public string P0 { get; set; }

            [CommandArgument(Order = 1)]
            public string P1 { get; set; }
        }

        [Test]
        public void Parse_SetsFirstArgumentOfTwoOrderedArguments() {
            var obj = new TwoArgumentOrderedCommand();
            Subject.Parse("onevalue twovalue", obj);
            Assert.That(obj.P0, Is.EqualTo("onevalue"));
        }

        [Test]
        public void Parse_SetsSecondArgumentOfTwoOrderedArguments() {
            var obj = new TwoArgumentOrderedCommand();
            Subject.Parse("onevalue twovalue", obj);
            Assert.That(obj.P1, Is.EqualTo("twovalue"));
        }

        class OneSwitchCommand {
            [CommandSwitch(Name = "myswitch", ShortName = 'm')]
            public bool MySwitch { get; set; }
        }

        [Test]
        public void Parse_SetsOneSwitchWithLongName() {
            var obj = new OneSwitchCommand();
            Subject.Parse("--myswitch", obj);
            Assert.That(obj.MySwitch, Is.True);
        }

        [Test]
        public void Parse_SetsOneSwitchWithShortName() {
            var obj = new OneSwitchCommand();
            Subject.Parse("-m", obj);
            Assert.That(obj.MySwitch, Is.True);
        }

        class SwitchIsArray {
            [CommandSwitch(ShortName = 'v')]
            public string[] Values { get; set; }
        }

        [Test]
        public void Parse_SetsSwitchArray() {
            var obj = new SwitchIsArray();
            Subject.Parse("-v 1 2 3", obj);
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, obj.Values);
        }

        class SwitchIsArrayOfInt {
            [CommandSwitch(ShortName = 'v')]
            public int[] Values { get; set; }
        }

        [Test]
        public void Parse_SetsSwitchArrayOfInt() {
            var obj = new SwitchIsArrayOfInt();
            Subject.Parse("-v 1 2 3", obj);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, obj.Values);
        }

        class ArgumentAndSwitchArray {
            [CommandArgument]
            public string TheArg { get; set; }

            [CommandSwitch(Name = "theswitch")]
            public double[] TheSwitch { get; set; }
        }

        [Test]
        public void Parse_SetsArgumentWithArgumentAndSwitchArray() {
            var obj = new ArgumentAndSwitchArray();
            Subject.Parse("somevalue --theswitch 2.2 3.3 4.4", obj);
            Assert.That(obj.TheArg, Is.EqualTo("somevalue"));
        }

        [Test]
        public void Parse_SetsSwitchWithArgumentAndSwitchArray() {
            var obj = new ArgumentAndSwitchArray();
            Subject.Parse("somevalue --theswitch 2.2 3.3 4.4", obj);
            CollectionAssert.AreEqual(new[] { 2.2, 3.3, 4.4 }, obj.TheSwitch);
        }

        class MultipleSwitches {
            [CommandSwitch(ShortName = 'x', Name = "thex")]
            public bool X { get; set; }

            [CommandSwitch(ShortName = 'y', Name = "they")]
            public bool Y { get; set; }

            [CommandSwitch(ShortName = 'z', Name = "thez")]
            public string Z { get; set; }
        }

        [Test]
        public void Parse_SetsMultipleSwitchesWithShortName() {
            var obj = new MultipleSwitches();
            Subject.Parse("-xyz helloworld", obj);
            CollectionAssert.AreEqual(new object[] { true, true, "helloworld" }, new object[] { obj.X, obj.Y, obj.Z });
        }

        [Test]
        public void Parse_SetsMultipleSwitchesWithLongNames() {
            var obj = new MultipleSwitches();
            Subject.Parse("--thex --they --thez helloworld", obj);
            CollectionAssert.AreEqual(new object[] { true, true, "helloworld" }, new object[] { obj.X, obj.Y, obj.Z });
        }

        [Test]
        public void Parse_SetsMultipleSwitchesWithMixedNames() {
            var obj = new MultipleSwitches();
            Subject.Parse("--they -xz helloworld", obj);
            CollectionAssert.AreEqual(new object[] { true, true, "helloworld" }, new object[] { obj.X, obj.Y, obj.Z });
        }

        [Test]
        public void Parse_DoesNotSetBooleanSwitchIfNotPresent() {
            var obj = new MultipleSwitches();
            Subject.Parse("-xz helloworld", obj);
            CollectionAssert.AreEqual(new object[] { true, false, "helloworld" }, new object[] { obj.X, obj.Y, obj.Z });
        }

        class SwitchUsesPropertyName {
            [CommandSwitch]
            public string MyString { get; set; }
        }

        [Test]
        public void Parse_UsesPropertyNameForSwitch() {
            var obj = new SwitchUsesPropertyName();
            Subject.Parse("--MyString IsThis", obj);
            Assert.That(obj.MyString, Is.EqualTo("IsThis"));
        }

        class ArgumentAggregate {
            [CommandArgument(Aggregate = true)]
            public string LongArg { get; set; }
        }

        [Test]
        public void Parse_ParsesAggregateArgument() {
            var obj = new ArgumentAggregate();
            Subject.Parse("some long arg", obj);
            Assert.That(obj.LongArg, Is.EqualTo("some long arg"));
        }

        class ArgumentAggregateWithSwitch {
            [CommandSwitch]
            public string SwitchStr { get; set; }

            [CommandArgument(Aggregate = true)]
            public string LongArg { get; set; }
        }

        [Test]
        public void Parse_ParsesAggregateArgumentAndSwitch() {
            var obj = new ArgumentAggregateWithSwitch();
            Subject.Parse("the ARG text --SwitchStr sw", obj);
            Assert.That(obj.LongArg, Is.EqualTo("the ARG text"));
        }
    }
}
