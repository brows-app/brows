using Domore.Logs;
using NUnit.Framework;
using Brows.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.CLI;

[TestFixture]
internal sealed class CliProgramTest {
    private readonly List<LogEventArgs> Logs = [];

    private void Logging_Event(object sender, LogEventArgs e) {
        lock (Logs) {
            Logs.Add(e);
        }
    }

    private IEnumerable<string> CliOutput() {
        return Logs
            .SelectMany(log => log.LogList.Where(log => log.StartsWith("!#:")))
            .Select(log => log.Length > 3 ? log.Substring(3) : "");
    }

    [SetUp]
    public void SetUp() {
        Logs.Clear();
        Logging.Event -= Logging_Event;
        Logging.Event += Logging_Event;
        Logging.EventThreshold = LogSeverity.Info;
    }

    [Test]
    public async Task Output_UnknownCommand() {
        await ImportSandbox.Clean(async () => {
            var subject = new CliProgram(new() {
                CommandLine = "this-command-does-not-exist",
            });
            await subject.Task;
        });
        Assert.That(CliOutput(), Is.EqualTo(["Unknown command 'this-command-does-not-exist'"]));
    }

    private sealed class FooBarCommand : CliCommand<FooBarCommand.Param> {
        private static readonly ILog Log = Logging.For(typeof(FooBarCommand));

        protected sealed override Task<object> Run(Param param, ICliCommandContext context, CancellationToken token) {
            Log.Info(Out("FOO"),
                     Out("BAR"),
                     Out("Baz"));
            return Task.FromResult(default(object));
        }

        public sealed class Param {
        }
    }

    [Test]
    public async Task Output_ComesFromCommand() {
        await ImportSandbox.Clean(async () => {
            var subject = new CliProgram(new() {
                CommandLine = "foobar",
            });
            await subject.Task;
        });
        Assert.That(CliOutput(), Is.EqualTo(["FOO", "BAR", "Baz"]));
    }
}
