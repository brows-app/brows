namespace Brows {
    public abstract class EntryConfig {
        public abstract T Configure<T>() where T : new();
    }
}
