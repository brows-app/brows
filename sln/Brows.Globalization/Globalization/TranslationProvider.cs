namespace Brows.Globalization {
    public class TranslationProvider : ITranslationProvider {
        public string Dialect {
            get => ((Translation)Translation).Dialect;
            set => ((Translation)Translation).Dialect = value;
        }

        public object Translator =>
            ((Translation)Translation).Translator;

        public ITranslation Translation =>
            _Translation ?? (
            _Translation = new Translation());
        private ITranslation _Translation;
    }
}
