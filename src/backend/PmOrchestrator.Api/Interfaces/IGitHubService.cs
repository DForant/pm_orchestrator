using PmOrchestrator.Api.Models;

namespace PmOrchestrator.Api.Interfaces;

public interface IGitHubService
{
    Task<GitHubIssueDto> CreateIssueAsync(
        string title,
        string body,
        IReadOnlyCollection<string> labels,
        CancellationToken cancellationToken = default);
}
