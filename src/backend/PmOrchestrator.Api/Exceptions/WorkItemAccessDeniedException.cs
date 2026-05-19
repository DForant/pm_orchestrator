namespace PmOrchestrator.Api.Exceptions;

public class WorkItemAccessDeniedException : Exception
{
    public WorkItemAccessDeniedException()
        : base("Access denied while retrieving work item from Azure DevOps.")
    {
    }
}
