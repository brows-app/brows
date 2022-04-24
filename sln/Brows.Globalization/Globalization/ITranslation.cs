namespace Brows.Globalization {
    public interface ITranslation {
        string Value(string key);
        string[] Alias(string key);
    }
}
