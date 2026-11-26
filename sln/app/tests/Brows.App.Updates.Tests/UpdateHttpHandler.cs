using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Brows;

internal sealed class UpdateHttpHandler : HttpMessageHandler {
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond;

    internal List<Uri> Requests { get; } = [];

    internal UpdateHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) {
        Respond = respond;
    }

    internal UpdateHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => Task.FromResult(respond(request))) {
    }

    internal static HttpResponseMessage Json<T>(T value) {
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        Requests.Add(request.RequestUri);
        return Respond(request, token);
    }
}
