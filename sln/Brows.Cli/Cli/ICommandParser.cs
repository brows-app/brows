namespace Brows.Cli {
    public interface ICommandParser {
        void Parse(string s, object obj);
    }
}
