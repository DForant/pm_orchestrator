using System.Text;
using PmOrchestrator.Api.Interfaces;

namespace PmOrchestrator.Api.Services;

public class OrchestrationService : IOrchestrationService
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<OrchestrationService> _logger;

    public OrchestrationService(
        IAzureDevOpsService azureDevOpsService,
        IGitHubService gitHubService,
        ILogger<OrchestrationService> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    public async Task<string> DispatchWorkItemToGitHubAsync(string workItemId, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(workItemId, out var id))
        {
            throw new ArgumentException($"Work item ID '{workItemId}' is not a valid integer.", nameof(workItemId));
        }

        _logger.LogInformation("Fetching Azure DevOps work item {WorkItemId}.", id);
        var workItem = await _azureDevOpsService.GetWorkItemAsync(id, cancellationToken);

        var body = BuildMarkdownBody(workItem.Id, workItem.Title, workItem.Description, workItem.AreaPath, workItem.Tags);

        _logger.LogInformation("Creating GitHub issue for work item {WorkItemId}.", id);
        var issue = await _gitHubService.CreateIssueAsync(workItem.Title, body, workItem.Tags, cancellationToken);

        _logger.LogInformation("GitHub issue created: {IssueUrl}.", issue.HtmlUrl);
        return issue.HtmlUrl;
    }

    private static string BuildMarkdownBody(int id, string title, string description, string areaPath, IReadOnlyCollection<string> tags)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"## 🗂 Work Item: {title} (#{id})");
        sb.AppendLine();
        sb.AppendLine("### Metadata");
        sb.AppendLine();
        sb.AppendLine("| Field     | Value |");
        sb.AppendLine("|-----------|-------|");
        sb.AppendLine($"| ID        | {id} |");
        sb.AppendLine($"| Area Path | {areaPath} |");
        sb.AppendLine($"| Tags      | {string.Join(", ", tags)} |");
        sb.AppendLine();
        sb.AppendLine("### Description");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(description) ? "_No description provided._" : description);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.Append("*Dispatched automatically by PmOrchestrator.*");

        return sb.ToString();
    }
}
