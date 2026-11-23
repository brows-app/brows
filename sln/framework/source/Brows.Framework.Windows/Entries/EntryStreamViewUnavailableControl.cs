using Brows.Localization;

namespace Brows.Entries;

public sealed class EntryStreamViewUnavailableControl : EntryStreamGuiControl {
    internal ITranslationExport Translation { get; set; }

    protected sealed override string EntryStreamViewName => "Unavailable";

    /// <summary>
    /// Gets the translated text displayed by the control.
    /// </summary>
    public string Text => Translation?.Value("EntryStreamViewUnavailable");
}