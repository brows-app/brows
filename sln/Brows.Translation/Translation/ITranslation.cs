namespace Brows.Translation {
    public interface ITranslation {
        string Value(string key);
        string[] Alias(string key);
    }
}
