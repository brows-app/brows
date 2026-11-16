using System.Threading.Tasks;

namespace Brows;

[SetUpFixture]
[NonParallelizable]
internal sealed class GitNativeTestSetup {
    [OneTimeSetUp]
    public async Task LoadNativeGit() {
        await GitNativeTestLoader.Load(CancellationToken.None);
    }

    [OneTimeTearDown]
    public void UnloadNativeGit() {
        GitNativeTestLoader.Unload();
    }
}
