using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PmOrchestrator.Api.Exceptions;
using PmOrchestrator.Api.Services;

namespace PmOrchestrator.Tests.Services;

public class AzureDevOpsServiceTests
{
    [Fact]
    public async Task GetWorkItemAsync_WhenResponseIsSuccessful_MapsWorkItemDto()
    {
        var responseBody = """
            {
              "id": 42,
              "fields": {
                "System.Title": "Fix production bug",
                "System.ReproSteps": "Fallback description",
                "System.Tags": "bug, urgent, customer",
                "System.AreaPath": "PmOrchestrator\\Backend"
              }
            }
            """;

        var (sut, _, _) = CreateService(HttpStatusCode.OK, responseBody);

        var result = await sut.GetWorkItemAsync(42);

        Assert.Equal(42, result.Id);
        Assert.Equal("Fix production bug", result.Title);
        Assert.Equal("Fallback description", result.Description);
        Assert.Equal(["bug", "urgent", "customer"], result.Tags);
        Assert.Equal("PmOrchestrator\\Backend", result.AreaPath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, typeof(WorkItemNotFoundException), LogLevel.Warning, "not found")]
    [InlineData(HttpStatusCode.Unauthorized, typeof(WorkItemAccessDeniedException), LogLevel.Error, "unauthorized")]
    [InlineData(HttpStatusCode.InternalServerError, typeof(HttpRequestException), LogLevel.Error, "failed with status code")]
    public async Task GetWorkItemAsync_WhenResponseIsNotSuccessful_LogsAndThrowsExpectedException(
        HttpStatusCode statusCode,
        Type expectedExceptionType,
        LogLevel expectedLogLevel,
        string expectedMessageFragment)
    {
        var (sut, logger, _) = CreateService(statusCode, "{}");

        var exception = await Record.ExceptionAsync(() => sut.GetWorkItemAsync(99));

        Assert.NotNull(exception);
        Assert.IsType(expectedExceptionType, exception);
        VerifyLoggerCall(logger, expectedLogLevel, expectedMessageFragment);
    }

    private static (AzureDevOpsService Sut, Mock<ILogger<AzureDevOpsService>> Logger, Mock<HttpMessageHandler> Handler) CreateService(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("AzureDevOps")).Returns(httpClient);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureDevOps:OrganizationUrl"] = "https://dev.azure.com/contoso",
                ["AzureDevOps:Token"] = "example-pat"
            })
            .Build();

        var logger = new Mock<ILogger<AzureDevOpsService>>();

        var sut = new AzureDevOpsService(factory.Object, configuration, logger.Object);
        return (sut, logger, handler);
    }

    private static void VerifyLoggerCall(Mock<ILogger<AzureDevOpsService>> logger, LogLevel expectedLogLevel, string expectedMessageFragment)
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
