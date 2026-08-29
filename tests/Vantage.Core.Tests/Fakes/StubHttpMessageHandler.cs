using System.Net;

namespace Vantage.Core.Tests.Fakes;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

    private StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        this.handler = handler;
    }

    public static StubHttpMessageHandler WithStatus(HttpStatusCode status) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    public static StubHttpMessageHandler Delayed(TimeSpan delay, HttpStatusCode status) =>
        new(async (_, ct) =>
        {
            await Task.Delay(delay, ct);
            return new HttpResponseMessage(status);
        });

    public static StubHttpMessageHandler ReturningText(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }));

    public static StubHttpMessageHandler Sequence(params string[] bodies)
    {
        var index = 0;
        return new((_, _) =>
        {
            var body = bodies[Math.Min(index, bodies.Length - 1)];
            index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        });
    }

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> Bodies { get; } = new();

    public static StubHttpMessageHandler Capturing(HttpStatusCode status = HttpStatusCode.OK)
    {
        StubHttpMessageHandler? self = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            self!.Requests.Add(request);
            self.Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync());
            return new HttpResponseMessage(status);
        });
        self = handler;
        return handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        handler(request, cancellationToken);
}
