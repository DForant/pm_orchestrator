using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PmOrchestrator.Api.Exceptions;
using PmOrchestrator.Api.Interfaces;
using PmOrchestrator.Api.Models;

namespace PmOrchestrator.Api.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly string _organizationUrl;
    private readonly string _token;

    public AzureDevOpsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AzureDevOpsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _organizationUrl = configuration["AzureDevOps:OrganizationUrl"]
            ?? throw new InvalidOperationException("Configuration key 'AzureDevOps:OrganizationUrl' is required.");
        _token = configuration["AzureDevOps:Token"]
            ?? throw new InvalidOperationException("Configuration key 'AzureDevOps:Token' is required.");
    }

    public async Task<WorkItemDto> GetWorkItemAsync(int id, CancellationToken cancellationToken = default)
    {
        var requestUri = $"{_organizationUrl.TrimEnd('/')}/_apis/wit/workitems/{id}?api-version=7.1";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_token}")));

        var client = _httpClientFactory.CreateClient("AzureDevOps");
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Azure DevOps work item {WorkItemId} was not found.", id);
            throw new WorkItemNotFoundException(id);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogError("Azure DevOps request for work item {WorkItemId} was unauthorized.", id);
            throw new WorkItemAccessDeniedException();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Azure DevOps request for work item {WorkItemId} failed with status code {StatusCode}.",
                id,
                response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        var fields = root.GetProperty("fields");

        var description = ReadField(fields, "System.Description");
        if (string.IsNullOrWhiteSpace(description))
        {
            description = ReadField(fields, "System.ReproSteps");
        }

        var tagsRaw = ReadField(fields, "System.Tags");
        var tags = string.IsNullOrWhiteSpace(tagsRaw)
            ? []
            : tagsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        return new WorkItemDto
        {
            Id = root.GetProperty("id").GetInt32(),
            Title = ReadField(fields, "System.Title"),
            Description = description,
            Tags = tags,
            AreaPath = ReadField(fields, "System.AreaPath")
        };
    }

    private static string ReadField(JsonElement fields, string fieldName)
    {
        if (fields.TryGetProperty(fieldName, out var value))
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
