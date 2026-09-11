using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Apha.Sdo.Persistence.Functions;

/// <summary>AHR pipeline equivalent of <see cref="CreateSubmissionMetadata"/>; shares the insert repository.</summary>
public class CreateAhrSubmissionMetadata
{
    private const string ProcessName = "AHR";

    private readonly ILogger<CreateAhrSubmissionMetadata> _logger;
    private readonly ISubmissionMetadataRepository _repository;

    public CreateAhrSubmissionMetadata(
        ILogger<CreateAhrSubmissionMetadata> logger,
        ISubmissionMetadataRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    [Function("CreateAhrSubmissionMetadata")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        SubmissionMetadataRequest? submission;

        try
        {
            submission = await JsonSerializer.DeserializeAsync<SubmissionMetadataRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("The request body must be valid JSON.");
        }

        if (submission is null || !submission.IsValid())
        {
            return new BadRequestObjectResult(
                "referenceNumber, userId, submittedAt, name, fileLocation, and submissionProcessName are required.");
        }

        // AHR submissions must always be scoped to a laboratory and use the AHR process name.
        if (string.IsNullOrWhiteSpace(submission.LaboratoryId))
        {
            return new BadRequestObjectResult("laboratoryId is required for AHR submissions.");
        }

        submission = submission with { SubmissionProcessName = ProcessName };

        var submissionId = await _repository.CreateAsync(submission, cancellationToken);
        _logger.LogInformation("Created AHR submission metadata record {SubmissionId} for reference {ReferenceNumber}.",
            submissionId, submission.ReferenceNumber);

        return new CreatedResult(string.Empty, new { submissionId });
    }
}
