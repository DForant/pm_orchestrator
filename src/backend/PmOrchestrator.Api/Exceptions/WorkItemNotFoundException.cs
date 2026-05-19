namespace PmOrchestrator.Api.Exceptions;

public class WorkItemNotFoundException : Exception
{
    public WorkItemNotFoundException(int id)
        : base($"Work item with id '{id}' was not found.")
    {
    }
}
