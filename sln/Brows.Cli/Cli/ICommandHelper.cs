using System;

namespace Brows.Cli {
    public interface ICommandHelper {
        ICommandHelp Help(Type type);
    }
}
