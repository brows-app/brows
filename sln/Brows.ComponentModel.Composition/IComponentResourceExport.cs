using System.ComponentModel.Composition;

namespace Brows {
    [InheritedExport(typeof(IComponentResource))]
    public interface IComponentResourceExport : IComponentResource {
    }
}
