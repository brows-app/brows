namespace Brows.Entries;

/// <summary>
/// Keeps prepared stream resources available until the consumer disposes this value.
/// </summary>
public interface IEntryStreamReady : IDisposable {
}
