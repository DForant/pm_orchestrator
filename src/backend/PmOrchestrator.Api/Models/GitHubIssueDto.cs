namespace PmOrchestrator.Api.Models;

public class GitHubIssueDto
{
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string HtmlUrl { get; set; } = string.Empty;
}
