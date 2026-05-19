using Microsoft.AspNetCore.Mvc;
using Moq;
using PmOrchestrator.Api.Controllers;
using PmOrchestrator.Api.Exceptions;
using PmOrchestrator.Api.Interfaces;

namespace PmOrchestrator.Tests.Controllers;

public class OrchestrationControllerTests
{
    [Fact]
    public async Task DispatchTask_WhenServiceSucceeds_ReturnsOkWithGitHubIssueUrl()
    {
        const string workItemId = "42";
        const string expectedUrl = "https://github.com/contoso/pm_orchestrator/issues/7";

        var mockService = new Mock<IOrchestrationService>();
        mockService
            .Setup(x => x.DispatchWorkItemToGitHubAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        var controller = new OrchestrationController(mockService.Object);

        var result = await controller.DispatchTask(workItemId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value!;
        var urlProp = value.GetType().GetProperty("gitHubIssueUrl");
        Assert.NotNull(urlProp);
        Assert.Equal(expectedUrl, urlProp.GetValue(value));
    }

    [Fact]
    public async Task DispatchTask_WhenWorkItemNotFound_ReturnsNotFound()
    {
        const string workItemId = "99";

        var mockService = new Mock<IOrchestrationService>();
        mockService
            .Setup(x => x.DispatchWorkItemToGitHubAsync(workItemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkItemNotFoundException(99));

        var controller = new OrchestrationController(mockService.Object);

        var result = await controller.DispatchTask(workItemId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DispatchTask_WhenWorkItemIdIsInvalid_ReturnsBadRequest()
    {
        const string workItemId = "not-a-number";

        var mockService = new Mock<IOrchestrationService>();
        mockService
            .Setup(x => x.DispatchWorkItemToGitHubAsync(workItemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException($"Work item ID '{workItemId}' is not a valid integer.", nameof(workItemId)));

        var controller = new OrchestrationController(mockService.Object);

        var result = await controller.DispatchTask(workItemId, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DispatchTask_WhenAccessDenied_ReturnsForbidden()
    {
        const string workItemId = "5";

        var mockService = new Mock<IOrchestrationService>();
        mockService
            .Setup(x => x.DispatchWorkItemToGitHubAsync(workItemId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WorkItemAccessDeniedException());

        var controller = new OrchestrationController(mockService.Object);

        var result = await controller.DispatchTask(workItemId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }
}
