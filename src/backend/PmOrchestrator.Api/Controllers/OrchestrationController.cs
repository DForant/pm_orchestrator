using Microsoft.AspNetCore.Mvc;
using PmOrchestrator.Api.Exceptions;
using PmOrchestrator.Api.Interfaces;

namespace PmOrchestrator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrchestrationController : ControllerBase
{
    private readonly IOrchestrationService _orchestrationService;

    public OrchestrationController(IOrchestrationService orchestrationService)
    {
        _orchestrationService = orchestrationService;
    }

    [HttpPost("dispatch/{workItemId}")]
    public async Task<IActionResult> DispatchTask(string workItemId, CancellationToken cancellationToken)
    {
        try
        {
            var gitHubIssueUrl = await _orchestrationService.DispatchWorkItemToGitHubAsync(workItemId, cancellationToken);
            return Ok(new { gitHubIssueUrl });
        }
        catch (WorkItemNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (WorkItemAccessDeniedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }
}
