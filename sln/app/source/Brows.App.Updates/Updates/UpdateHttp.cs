using System.Net.Http;

namespace Brows.Updates;

internal static class UpdateHttp {
    internal static HttpClient Client { get; } = new();
}
