namespace PmOrchestrator.Api.Models;

public class WorkItemDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string AreaPath { get; set; } = string.Empty;
}
