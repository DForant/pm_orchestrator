using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PmOrchestrator.Api.Services;

namespace PmOrchestrator.Tests.Services;

public class GitHubServiceTests
{
    [Fact]
    public async Task CreateIssueAsync_WhenResponseIsSuccessful_SendsExpectedRequestAndMapsResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        Task<string>? capturedPayloadTask = null;
        var responseBody = """
            {
              "number": 18,
              "title": "Generated execution issue",
              "html_url": "https://github.com/contoso/pm_orchestrator/issues/18"
            }
            """;

        var (sut, _, _) = CreateService(
            HttpStatusCode.Created,
            responseBody,
            request =>
            {
                capturedRequest = request;
                capturedPayloadTask = request.Content?.ReadAsStringAsync();
            });

        var result = await sut.CreateIssueAsync(
            "Generated execution issue",
            "Markdown context",
            ["backend"]);

        Assert.Equal(18, result.Number);
        Assert.Equal("Generated execution issue", result.Title);
        Assert.Equal("https://github.com/contoso/pm_orchestrator/issues/18", result.HtmlUrl);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.github.com/repos/contoso/pm_orchestrator/issues", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("example-token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Contains(capturedRequest.Headers.UserAgent, header => header.Product?.Name == "PmOrchestrator");
        Assert.Contains(capturedRequest.Headers.Accept, header => header.MediaType == "application/vnd.github+json");

        Assert.NotNull(capturedPayloadTask);
        var capturedPayload = await capturedPayloadTask!;
        using var payloadJson = JsonDocument.Parse(capturedPayload);
        Assert.Equal("Generated execution issue", payloadJson.RootElement.GetProperty("title").GetString());
        Assert.Equal("Markdown context", payloadJson.RootElement.GetProperty("body").GetString());
        Assert.Equal("backend", payloadJson.RootElement.GetProperty("labels")[0].GetString());
    }

    [Fact]
    public async Task CreateIssueAsync_WhenResponseIsNotSuccessful_LogsAndThrows()
    {
        var (sut, logger, _) = CreateService(HttpStatusCode.Unauthorized, "{}");

        var exception = await Record.ExceptionAsync(() => sut.CreateIssueAsync("title", "body", ["backend"]));

        Assert.NotNull(exception);
        Assert.IsType<HttpRequestException>(exception);
        VerifyLoggerCall(logger, LogLevel.Error, "failed with status code");
    }

    private static (GitHubService Sut, Mock<ILogger<GitHubService>> Logger, Mock<HttpMessageHandler> Handler) CreateService(
        HttpStatusCode statusCode,
        string responseBody,
        Action<HttpRequestMessage>? captureRequest = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback((HttpRequestMessage request, CancellationToken _) => captureRequest?.Invoke(request))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("GitHub")).Returns(httpClient);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:Token"] = "example-token",
                ["GitHub:RepositoryName"] = "contoso/pm_orchestrator"
            })
            .Build();

        var logger = new Mock<ILogger<GitHubService>>();
        var sut = new GitHubService(factory.Object, configuration, logger.Object);
        return (sut, logger, handler);
    }

    private static void VerifyLoggerCall(Mock<ILogger<GitHubService>> logger, LogLevel expectedLogLevel, string expectedMessageFragment)
    {
        logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == expectedLogLevel),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
