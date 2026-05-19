using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PmOrchestrator.Api.Interfaces;
using PmOrchestrator.Api.Models;

namespace PmOrchestrator.Api.Services;

public class GitHubService : IGitHubService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubService> _logger;
    private readonly string _token;
    private readonly string _repositoryName;

    public GitHubService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GitHubService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _token = configuration["GitHub:Token"]
            ?? throw new InvalidOperationException("Configuration key 'GitHub:Token' is required.");
        _repositoryName = configuration["GitHub:RepositoryName"]
            ?? throw new InvalidOperationException("Configuration key 'GitHub:RepositoryName' is required.");
    }

    public async Task<GitHubIssueDto> CreateIssueAsync(
        string title,
        string body,
        IReadOnlyCollection<string> labels,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Issue title is required.", nameof(title));
        }

        if (!IsRepositoryNameValid(_repositoryName))
        {
            throw new InvalidOperationException("Configuration key 'GitHub:RepositoryName' must be in the format 'owner/repository'.");
        }

        var requestUri = $"https://api.github.com/repos/{_repositoryName}/issues";
        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            labels
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.UserAgent.ParseAdd("PmOrchestrator");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient("GitHub");
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "GitHub issue creation failed with status code {StatusCode}.",
                response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

        var root = document.RootElement;

        return new GitHubIssueDto
        {
            Number = root.GetProperty("number").GetInt32(),
            Title = root.GetProperty("title").GetString() ?? string.Empty,
            HtmlUrl = root.GetProperty("html_url").GetString() ?? string.Empty
        };
    }

    private static bool IsRepositoryNameValid(string repositoryName)
    {
        var segments = repositoryName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 2;
    }
}
