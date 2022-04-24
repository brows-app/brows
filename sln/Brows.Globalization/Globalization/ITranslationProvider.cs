namespace Brows.Globalization {
    public interface ITranslationProvider {
        object Translator { get; }
        ITranslation Translation { get; }
    }
}
