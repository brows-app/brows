namespace Brows {
    public interface IMatcher {
        bool Matches(string s);
        bool Matches(string s, out IMatched matched);
    }
}
