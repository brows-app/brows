using System.ComponentModel.Composition;

namespace Brows {
    [InheritedExport(typeof(IProgram))]
    public interface IProgramExport : IProgram {
    }
}
