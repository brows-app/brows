using Domore.Conf.Logs;
using Brows.CLI;
using Brows.IPC.HostCommands;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC;

internal sealed class SampleClient {
    private async static Task Main() {
        LogConf.ConfigureLogging(@"
            log[console].config.default.format   = 
            log[console].config.default.severity = info
        ");
        var d = new SampleDelegate();
        var host = new ObjectCallHost(d);
        using (var tokenSource = new CancellationTokenSource()) {
            Console.CancelKeyPress += (s, e) => {
                /*
                 * Canceling the token effectively finishes the
                 * sample application. So, this sample can be
                 * exited by pressing Ctrl+C on the keyboard.
                 */
                tokenSource.Cancel();
                e.Cancel = true;
            };
            await host.Run(token: tokenSource.Token, info: new() {
                EndPoint = new(IPAddress.Loopback, 8123),
                OnStart = (info, token) => {
                    /*
                     * This method runs the "Host" part of the sample.
                     * It starts the host executable in a different
                     * process and returns immediately, not waiting for
                     * the process to complete.
                     */
                    _ = CliProgram.Run(
                        /*
                         * The host executable should be in the same
                         * directory as this .EXE, so all we need to
                         * provide is the file name.
                         */
                        "Brows.IPC.Sample.Host.exe",
                        /*
                         * The parameter passed to the host .exe 
                         * specifies the port and delegate to use
                         * for the object host. The delegate is 
                         * passed with a simple string, the name
                         * of the class, which will be resolved by
                         * the Factory imported with that same name.
                         */
                        new Post.Param {
                            Delegate = nameof(SampleDelegate),
                            EndPoints = $"{8123}"
                        },
                        new CliProgramInfo {
                            /*
                             * Setting InWindow = true makes the console
                             * window visible.
                             */
                            InWindow = true
                        },
                        token);
                    return Task.CompletedTask;
                }
            });
        }
    }
}
