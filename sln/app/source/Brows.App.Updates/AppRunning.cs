using Brows.Updates;
using System.Threading.Tasks;

namespace Brows;

internal sealed class AppRunning : IAppRunningVersion, IAppRunningArchitecture, IAppUpdateInstallable {
    Task<string> IAppRunningVersion.Get(CancellationToken token) {
        return Task.FromResult(UpdateInstallation.Current.VersionText);
    }

    Task<string> IAppRunningArchitecture.Get(CancellationToken token) {
        return Task.FromResult(UpdateInstallation.Current.Architecture);
    }

    Task<bool> IAppUpdateInstallable.Get(CancellationToken token) {
        return Task.FromResult(UpdateInstallation.Current.Installable);
    }
}
