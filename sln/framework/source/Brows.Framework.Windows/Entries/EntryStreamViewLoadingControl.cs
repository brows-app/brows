using Brows.Localization;

namespace Brows.Entries;

public sealed class EntryStreamViewLoadingControl : EntryStreamGuiControl {
    internal ITranslationExport Translation { get; set; }

    protected sealed override string EntryStreamViewName => "Loading";

    /// <summary>
    /// Gets the translated text displayed by the control.
    /// </summary>
    public string Text => Translation?.Value("EntryStreamViewLoading");
}