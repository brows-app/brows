using Brows.Composition;
using System.Threading.Tasks;

namespace Brows.IPC;

[TestFixture]
internal sealed class ObjectCallDelegateTest {
    private sealed class FactoryTestDependency : IExport {
    }

    private sealed class FactoryTestSubject : ObjectCallDelegate {
        [ImportRequired]
        internal FactoryTestDependency Dependency { get; set; }

        public override IObjectTypeResolver TypeResolver =>
            throw new NotSupportedException();

        public override Task<object> Call(object input, CancellationToken token) {
            throw new NotSupportedException();
        }

        public sealed class Factory : Factory<FactoryTestSubject> {
        }
    }

    [Test]
    public async Task Factory_ConstructsObjectWithDependencies() {
        using var sandbox = ImportSandbox.Create(
            inject: () => [
                new FactoryTestDependency(),
                new FactoryTestSubject.Factory()]);
        var obj = default(IObjectCallDelegate);
        await sandbox.Enter(sandbox => {
            var factory = sandbox.Imports.Find<IObjectCallFactory>();
            obj = factory.Create();
        });
        Assert.That(obj,
            Is.InstanceOf<FactoryTestSubject>().With
                .Property(nameof(FactoryTestSubject.Dependency)).Not.Null);
    }
}
