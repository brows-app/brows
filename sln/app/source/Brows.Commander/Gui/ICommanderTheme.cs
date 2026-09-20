using Brows.Composition;

namespace Brows.Gui;

public interface ICommanderTheme : IExport {
    void Apply(CommanderTheme theme);
}
