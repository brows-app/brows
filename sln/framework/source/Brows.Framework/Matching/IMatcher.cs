namespace Brows.Matching;

public interface IMatcher {
    bool Matches(string s);
    bool Matches(string s, out IMatched matched);
}
