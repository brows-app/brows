namespace Brows.Globalization {
    public static class Global {
        private static ITranslationProvider TranslationProvider =>
            _TranslationProvider ?? (
            _TranslationProvider = new TranslationProvider());
        private static ITranslationProvider _TranslationProvider;

        public static ITranslation Translation =>
            TranslationProvider.Translation;
    }
}
