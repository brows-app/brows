namespace Brows.Globalization {
    internal class Translator {
        private readonly Dialect.Collection DialectCollection = new Dialect.Collection();
        public Dialect this[string name] => DialectCollection[name];
    }
}
