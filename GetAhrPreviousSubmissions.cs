using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Apha.Sdo.Persistence.Functions;

/// <summary>AHR pipeline equivalent of <see cref="GetPreviousSubmissions"/>; shares the fetch repository, filtered to AHR submissions.</summary>
public sealed class GetAhrPreviousSubmissions
{
    private const string ProcessName = "AHR";

    private readonly ISubmissionQueryRepository _repository;

    public GetAhrPreviousSubmissions(ISubmissionQueryRepository repository)
    {
        _repository = repository;
    }

    [Function("GetAhrPreviousSubmissions")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (req.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return new UnauthorizedResult();
        }

        var laboratoryId = LaboratoryIdClaim.Get(req.HttpContext.User);
        if (laboratoryId is null)
        {
            return new ForbidResult();
        }

        var submissions = await _repository.GetSummariesAsync(laboratoryId, ProcessName, cancellationToken);
        return new OkObjectResult(submissions);
    }
}
