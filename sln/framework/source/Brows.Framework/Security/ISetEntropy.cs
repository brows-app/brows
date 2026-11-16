using Brows.Composition;
using System.Security;

namespace Brows.Security;

public interface ISetEntropy : IExport {
    void Work(SecureString entropy);
}
