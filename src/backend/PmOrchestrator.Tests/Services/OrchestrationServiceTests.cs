using Microsoft.Extensions.Logging;
using Moq;
using PmOrchestrator.Api.Interfaces;
using PmOrchestrator.Api.Models;
using PmOrchestrator.Api.Services;

namespace PmOrchestrator.Tests.Services;

public class OrchestrationServiceTests
{
    [Fact]
    public async Task DispatchWorkItemToGitHubAsync_WhenWorkItemIsValid_CreatesIssueAndReturnsHtmlUrl()
    {
        var workItem = new WorkItemDto
        {
            Id = 42,
            Title = "Fix production bug",
            Description = "Steps to reproduce the issue.",
            Tags = ["bug", "urgent"],
            AreaPath = "PmOrchestrator\\Backend"
        };

        var issue = new GitHubIssueDto
        {
            Number = 7,
            Title = "Fix production bug",
            HtmlUrl = "https://github.com/contoso/pm_orchestrator/issues/7"
        };

        var (sut, azureDevOpsMock, gitHubMock, _) = CreateService(workItem, issue);

        var result = await sut.DispatchWorkItemToGitHubAsync("42");

        Assert.Equal("https://github.com/contoso/pm_orchestrator/issues/7", result);

        azureDevOpsMock.Verify(x => x.GetWorkItemAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        gitHubMock.Verify(
            x => x.CreateIssueAsync(
                "Fix production bug",
                It.Is<string>(body => body.Contains("Fix production bug") && body.Contains("Steps to reproduce the issue.")),
                It.Is<IReadOnlyCollection<string>>(labels => labels.Contains("bug") && labels.Contains("urgent")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchWorkItemToGitHubAsync_WhenWorkItemIdIsNotAnInteger_ThrowsArgumentException()
    {
        var (sut, _, _, _) = CreateService(new WorkItemDto(), new GitHubIssueDto());

        var exception = await Record.ExceptionAsync(() => sut.DispatchWorkItemToGitHubAsync("not-a-number"));

        Assert.NotNull(exception);
        Assert.IsType<ArgumentException>(exception);
        Assert.Contains("not-a-number", exception.Message);
    }

    [Fact]
    public async Task DispatchWorkItemToGitHubAsync_WhenAzureDevOpsThrows_PropagatesException()
    {
        var azureDevOpsMock = new Mock<IAzureDevOpsService>();
        azureDevOpsMock
            .Setup(x => x.GetWorkItemAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AzDO unavailable"));

        var gitHubMock = new Mock<IGitHubService>();
        var logger = new Mock<ILogger<OrchestrationService>>();
        var sut = new OrchestrationService(azureDevOpsMock.Object, gitHubMock.Object, logger.Object);

        var exception = await Record.ExceptionAsync(() => sut.DispatchWorkItemToGitHubAsync("1"));

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("AzDO unavailable", exception.Message);
        gitHubMock.Verify(x => x.CreateIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchWorkItemToGitHubAsync_WhenDescriptionIsEmpty_BodyContainsFallbackText()
    {
        var workItem = new WorkItemDto
        {
            Id = 10,
            Title = "Empty description item",
            Description = string.Empty,
            Tags = [],
            AreaPath = "PmOrchestrator"
        };

        var issue = new GitHubIssueDto
        {
            Number = 3,
            Title = "Empty description item",
            HtmlUrl = "https://github.com/contoso/pm_orchestrator/issues/3"
        };

        var (sut, _, gitHubMock, _) = CreateService(workItem, issue);

        await sut.DispatchWorkItemToGitHubAsync("10");

        gitHubMock.Verify(
            x => x.CreateIssueAsync(
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains("_No description provided._")),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (OrchestrationService Sut, Mock<IAzureDevOpsService> AzureDevOps, Mock<IGitHubService> GitHub, Mock<ILogger<OrchestrationService>> Logger) CreateService(
        WorkItemDto workItem,
        GitHubIssueDto gitHubIssue)
    {
        var azureDevOpsMock = new Mock<IAzureDevOpsService>();
        azureDevOpsMock
            .Setup(x => x.GetWorkItemAsync(workItem.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var gitHubMock = new Mock<IGitHubService>();
        gitHubMock
            .Setup(x => x.CreateIssueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gitHubIssue);

        var logger = new Mock<ILogger<OrchestrationService>>();
        var sut = new OrchestrationService(azureDevOpsMock.Object, gitHubMock.Object, logger.Object);

        return (sut, azureDevOpsMock, gitHubMock, logger);
    }
}
