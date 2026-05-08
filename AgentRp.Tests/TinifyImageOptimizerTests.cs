using System.Net;
using System.Net.Http.Headers;
using AgentRp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentRp.Tests;

public sealed class TinifyImageOptimizerTests
{
    static readonly byte[] PngBytes = [1, 2, 3];
    static readonly byte[] AvifBytes = [4, 5, 6];

    [Fact]
    public async Task OptimizeAsyncWithoutApiKeyReturnsOriginalWithoutAttempting()
    {
        var called = false;
        var optimizer = BuildOptimizer("", _ =>
        {
            called = true;
            return new(HttpStatusCode.InternalServerError);
        });

        var result = await optimizer.OptimizeAsync(new(PngBytes, "image/png", "image.png"));

        Assert.False(called);
        Assert.Same(PngBytes, result.Bytes);
        Assert.Equal("image/png", result.ContentType);
        Assert.False(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Equal(".png", result.FileExtension);
    }

    [Fact]
    public async Task OptimizeAsyncConvertsCompressedImageToAvif()
    {
        var requests = new List<HttpRequestMessage>();
        var optimizer = BuildOptimizer("key", request =>
        {
            requests.Add(request);
            if (request.RequestUri?.AbsolutePath == "/shrink")
                return new(HttpStatusCode.Created)
                {
                    Headers =
                    {
                        Location = new Uri("https://api.tinify.com/output/abc")
                    }
                };

            return new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(AvifBytes)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("image/avif")
                    }
                }
            };
        });

        var result = await optimizer.OptimizeAsync(new(PngBytes, "image/png", "image.png"));

        Assert.Equal(2, requests.Count);
        Assert.Equal(AvifBytes, result.Bytes);
        Assert.Equal("image/avif", result.ContentType);
        Assert.Equal(".avif", result.FileExtension);
        Assert.True(result.Attempted);
        Assert.True(result.Succeeded);
        Assert.Equal("tinify", result.Provider);
    }

    [Fact]
    public async Task OptimizeAsyncFallsBackToOriginalWhenTinifyReturnsError()
    {
        var optimizer = BuildOptimizer("key", _ => new(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":"Unauthorized","message":"Bad key"}""")
        });

        var result = await optimizer.OptimizeAsync(new(PngBytes, "image/png", "image.png"));

        Assert.Equal(PngBytes, result.Bytes);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(".png", result.FileExtension);
        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Equal("tinify", result.Provider);
        Assert.Contains("Bad key", result.ErrorMessage);
    }

    static TinifyImageOptimizer BuildOptimizer(string apiKey, Func<HttpRequestMessage, HttpResponseMessage> send)
    {
        var httpClient = new HttpClient(new DelegateHandler(send))
        {
            BaseAddress = new Uri("https://api.tinify.com/")
        };
        return new(httpClient, Options.Create(new TinifyOptions { ApiKey = apiKey }), NullLogger<TinifyImageOptimizer>.Instance);
    }

    sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}
