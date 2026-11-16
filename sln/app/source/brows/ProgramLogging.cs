using Brows.Conf;
using Domore.Conf.Logs;
using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal static class ProgramLogging {
    private static readonly ILog Log = Logging.For(typeof(ProgramLogging));
    private static readonly SemaphoreSlim Locker = new(1, 1);

    private static async void Invalidated(object sender, EventArgs e) {
        try {
            await Configure(CancellationToken.None);
        }
        catch (Exception ex) {
            if (Log.Error()) {
                Log.Error(ex);
            }
        }
    }

    /*
     * The configuration is applied like every other configuration file: the shipped
     * default first, then the user's file. A change to the user's file applies both again.
     */
    internal static async Task Configure(CancellationToken token) {
        await Locker.WaitAsync(token);
        try {
            var info = await ConfigurationFileInfo.Load("log", Invalidated, token);
            await info.Configure(config => config.ConfigureLogging(), token);
        }
        finally {
            Locker.Release();
        }
    }
}
