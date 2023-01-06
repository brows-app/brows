using System.ComponentModel.Composition;

namespace Brows {
    [InheritedExport(typeof(ICommand))]
    public interface ICommandExport : ICommand {
    }
}
