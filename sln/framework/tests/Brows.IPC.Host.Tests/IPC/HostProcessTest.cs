using NUnit.Framework;
using Brows.CLI;
using Brows.IPC.HostCommands;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC;

[TestFixture, NonParallelizable]
internal sealed class HostProcessTest {
    private const string FileName = "Brows.IPC.Sample.Host.exe";

    [TestCase("")]
    [TestCase("about")]
    public async Task About_ReturnsNameOfProgramOnFirstLine(string commandLine) {
        BrowsTest.Ignore(BrowsTestEnv.GitHubAction);
        var data = await CliProgram.Run(FileName, commandLine, default);
        var lines = data.Output;
        var firstLine = lines.Split('\n')[0].Trim();
        Assert.That(firstLine, Is.EqualTo("Brows IPC Sample Host"));
    }

    private static async Task<(CliProgramData Post, CliProgramData Call)> Run(Type delegateType,
                                                                              int? postCount = null,
                                                                              int? listenCount = null,
                                                                              int maxLogLength = 1000000,
                                                                              int maxOutputLength = 1000000,
                                                                              string postTo = null,
                                                                              string listenOn = null) {
        /*
         * Tests in different target frameworks run concurrently in different processes,
         * so we need to use a unique port in each process.
         */
        var port =
#if NET462
            8123
#endif
#if NET8_0
            8124
#endif
#if NET10_0
            8125
#endif
            ;
        using (var cts = new CancellationTokenSource()) {
            var post = new Post.Param {
                Delegate = delegateType?.Name,
                EndPoints = postTo ?? $"{port}",
            };
            var call = new Call.Param {
                Delegate = delegateType?.Name,
                EndPoint = listenOn ?? $"{port}",
            };
            var postDataCount = 0;
            var postRun = CliProgram.Run(FileName, post, token: cts.Token, info: new() {
                MaxLogLength = maxLogLength,
                MaxOutputLength = maxOutputLength,
                OnLog = data => {
                    Debug.WriteLine(data);
                },
                OnOutput = data => {
                    var count = ++postDataCount;
                    if (count >= postCount) {
                        cts.Cancel();
                    }
                }
            });
            var callDataCount = 0;
            var callRun = CliProgram.Run(FileName, call, token: cts.Token, info: new() {
                MaxLogLength = maxLogLength,
                MaxOutputLength = maxOutputLength,
                OnLog = data => {
                    Debug.WriteLine(data);
                },
                OnOutput = data => {
                    var count = ++callDataCount;
                    if (count >= listenCount) {
                        cts.Cancel();
                    }
                }
            });
            var postData = await postRun;
            var callData = await callRun;
            return (postData, callData);
        }
    }

    class Run_Test1_Delegate : ObjectDelegate {
        protected sealed override Task<bool> Subscribe(CancellationToken token) {
            _ = Task.Run(cancellationToken: token, function: async () => {
                await Post(new Rx { N = 34.567 }, token);
            });
            return Task.FromResult(true);
        }

        public sealed override IObjectTypeResolver TypeResolver => ObjectTypeResolver.Lookup([
            typeof(Tx),
            typeof(Rx)]);

        public sealed override async Task<object> Call(object input, CancellationToken token) {
            if (input is Rx rx) {
                return new Tx { S = rx.N.ToString() };
            }
            await Task.CompletedTask;
            return null;
        }

        public class Tx {
            public string S { get; set; }
        }

        public class Rx {
            public double N { get; set; }
        }

        class Factory : Factory<Run_Test1_Delegate> { }
    }

    [Test]
    public async Task Run_Test1_LogsListenData() {
        BrowsTest.Ignore(BrowsTestEnv.GitHubAction);
        var result = await Run(typeof(Run_Test1_Delegate), listenCount: 1);
        var actual = result.Call.OutputLines[0];
        var expected = @"{""r"":{""v"":1,""j"":{""t"":""Rx"",""s"":""*"",""v"":""*"",""o"":{""N"":34.567}}}}";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task Run_Test1_LogsListenData2() {
        BrowsTest.Ignore(BrowsTestEnv.GitHubAction);
        var result = await Run(typeof(Run_Test1_Delegate), listenCount: 2);
        var actual = result.Call.OutputLines[1];
        var expected = @"{""w"":{""v"":1,""j"":{""t"":""Tx"",""s"":""*"",""v"":""*"",""o"":{""S"":""34.567""}}}}";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task Run_Test1_LogsPostData() {
        BrowsTest.Ignore(BrowsTestEnv.GitHubAction);
        var result = await Run(typeof(Run_Test1_Delegate), postCount: 1);
        var actual = result.Post.OutputLines[0];
        var expected = @"{""v"":1,""j"":{""t"":""Rx"",""s"":""*"",""v"":""*"",""o"":{""N"":34.567}}}";
        Assert.That(actual, Is.EqualTo(expected));
    }

    class Run_Test2_Delegate : ObjectDelegate {
        protected sealed override Task<bool> Subscribe(CancellationToken token) {
            _ = Task.Run(cancellationToken: token, function: async () => {
                for (; ; )
                {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    await Post(new Foo { Now = DateTime.UtcNow }, token);
                }
            });
            return Task.FromResult(true);
        }

        public sealed override IObjectTypeResolver TypeResolver => ObjectTypeResolver.Lookup([
            typeof(Foo),
            typeof(Bar)]);

        public sealed override async Task<object> Call(object input, CancellationToken token) {
            if (input is Foo foo) {
                return new Bar(foo.Now);
            }
            await Task.CompletedTask;
            return null;
        }

        public class Foo {
            public DateTime Now { get; set; }
        }

        public class Bar(DateTime then) {
            public DateTime Then { get; } = then;
        }

        class Factory : Factory<Run_Test2_Delegate> { }
    }

    [TestCase(999)]
    public async Task Run_Test2_OutputsLotsOfData(int count) {
        BrowsTest.Ignore(BrowsTestEnv.GitHubAction);
        var result = await Run(typeof(Run_Test2_Delegate), listenCount: count);
        Assert.That(
            result.Call.OutputLines.Count,
            Is.AtLeast(count));
    }

    [TestCase(100, 100)]
    [TestCase(5, 10)]
    public async Task Run_Test2_TruncatesOutput(int listenCount, int maxOutputLength) {
        BrowsTest.Ignore(BrowsTestEnv.GitHubAction);
        var result = await Run(typeof(Run_Test2_Delegate), listenCount: listenCount, maxOutputLength: maxOutputLength);
        Assert.That(
            result.Call.Output.Length,
            Is.AtMost(maxOutputLength));
    }
}
