using System.Threading.Tasks;

namespace Brows.IPC;

internal sealed class SampleHost {
    /// <summary>
    /// Runs the sample host process.
    /// </summary>
    /// <remarks>
    /// This is the only code necessary to run the IPC host. It'll find the SampleDelegate
    /// declared in the Brows.IPC.Sample.Common.dll library because it is the only delegate
    /// that has a factory imported in the composition (DI) framework.
    /// 
    /// That delegate will be instantiated and used to post messages to the client. See the
    /// <see cref="SampleDelegate"/> class for more info.
    /// 
    /// Logs and data used by the process will be saved to and loaded from the local user's
    /// app data directory at the path %LOCALAPPDATA%\Brows\IPC\Brows IPC Sample Host.
    /// This path is built using the `name` parameter of the constructor for <see cref="HostProcess"/>.
    /// </remarks>
    /// <returns>
    /// The task of the process.
    /// </returns>
    private static Task Main() {
        var hostProcess = new HostProcess(name: "Brows IPC Sample Host");
        return hostProcess.Task;
    }
}
