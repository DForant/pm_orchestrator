using PmOrchestrator.Api.Models;

namespace PmOrchestrator.Api.Interfaces;

public interface IAzureDevOpsService
{
    Task<WorkItemDto> GetWorkItemAsync(int id, CancellationToken cancellationToken = default);
}
