using Brows.CLI;
using System.Threading.Tasks;

namespace Brows;

internal sealed class Program {
    private static async Task<int> Main(string[] args) {
        var program = new CliProgram();
        await program.Task;
        return program.ErrorCode;
    }
}
