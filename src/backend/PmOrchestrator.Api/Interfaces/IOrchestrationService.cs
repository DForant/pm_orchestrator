namespace PmOrchestrator.Api.Interfaces;

public interface IOrchestrationService
{
    // Fetches from AzDO, applies markdown templating, pushes to GitHub, and returns the resulting GitHub Issue URL.
    Task<string> DispatchWorkItemToGitHubAsync(string workItemId, CancellationToken cancellationToken = default);
}
