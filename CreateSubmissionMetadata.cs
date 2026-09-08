using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace Apha.Sdo.Persistence.Functions;

public class CreateSubmissionMetadata
{
    private readonly ILogger<CreateSubmissionMetadata> _logger;
    private readonly ISubmissionMetadataRepository _repository;

    public CreateSubmissionMetadata(
        ILogger<CreateSubmissionMetadata> logger,
        ISubmissionMetadataRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    [Function("CreateSubmissionMetadata")]
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

        var submissionId = await _repository.CreateAsync(submission, cancellationToken);
        _logger.LogInformation("Created submission metadata record {SubmissionId} for reference {ReferenceNumber}.",
            submissionId, submission.ReferenceNumber);

        return new CreatedResult(string.Empty, new { submissionId });
    }
}

public interface ISubmissionMetadataRepository
{
    Task<Guid> CreateAsync(SubmissionMetadataRequest submission, CancellationToken cancellationToken);
}

public sealed class SubmissionMetadataRepository : ISubmissionMetadataRepository
{
    private const string InsertCommand = """
        INSERT INTO dbo.Batch_Submission_Message (
            SubmissionID, UploadReferenceNumber, UserId, SubmissionDate, FileName, FileLocation,
            SubmissionMnthYr, SubmissionProcessName, NotificationEmail, CreatedDateTime, CreatedBy, FailureCode,
            FailureReason, SubmissionStatus, ETLProcessStatus, UpdateDateTime, UpdatedBy, LaboratoryId)
        VALUES (
            @SubmissionID, @UploadReferenceNumber, @UserId, @SubmissionDate, @FileName, @FileLocation,
            @SubmissionMnthYr, @SubmissionProcessName, @NotificationEmail, @CreatedDateTime, @CreatedBy, @FailureCode,
            @FailureReason, @SubmissionStatus, @ETLProcessStatus, @UpdateDateTime, @UpdatedBy, @LaboratoryId);
        """;

    private readonly string? _connectionString;

    public SubmissionMetadataRepository(IConfiguration configuration)
    {
        _connectionString = configuration["SqlConnectionString"];
    }

    public async Task<Guid> CreateAsync(SubmissionMetadataRequest submission, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("The SqlConnectionString application setting has not been configured.");
        }

        var submissionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(InsertCommand, connection);

        command.Parameters.Add("@SubmissionID", SqlDbType.UniqueIdentifier).Value = submissionId;
        command.Parameters.Add("@UploadReferenceNumber", SqlDbType.NVarChar, 50).Value = submission.ReferenceNumber!;
        command.Parameters.Add("@UserId", SqlDbType.NVarChar, 50).Value = submission.UserId!;
        command.Parameters.Add("@SubmissionDate", SqlDbType.DateTime2).Value = submission.SubmittedAt!.Value;
        command.Parameters.Add("@FileName", SqlDbType.NVarChar, 500).Value = submission.Name!;
        command.Parameters.Add("@FileLocation", SqlDbType.NVarChar, 500).Value = submission.FileLocation!;
        command.Parameters.Add("@SubmissionMnthYr", SqlDbType.NVarChar, 10).Value = DbValue(submission.SubmissionMnthYr);
        command.Parameters.Add("@SubmissionProcessName", SqlDbType.NVarChar, 50).Value = submission.SubmissionProcessName!;
        command.Parameters.Add("@NotificationEmail", SqlDbType.NVarChar, 320).Value = DbValue(submission.NotificationEmail);
        command.Parameters.Add("@CreatedDateTime", SqlDbType.DateTime2).Value = now;
        command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 50).Value = submission.UserId!;
        command.Parameters.Add("@FailureCode", SqlDbType.NVarChar, 100).Value = DbValue(submission.FailureCode);
        command.Parameters.Add("@FailureReason", SqlDbType.NVarChar, 200).Value = DbValue(submission.FailureReason);
        command.Parameters.Add("@SubmissionStatus", SqlDbType.NVarChar, 10).Value = submission.SubmissionStatus ?? "Received";
        command.Parameters.Add("@ETLProcessStatus", SqlDbType.NVarChar, 10).Value = submission.EtlProcessStatus ?? "Pending";
        command.Parameters.Add("@UpdateDateTime", SqlDbType.DateTime2).Value = now;
        command.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = submission.UserId!;
        command.Parameters.Add("@LaboratoryId", SqlDbType.NVarChar, 50).Value = DbValue(submission.LaboratoryId);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return submissionId;
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}

public sealed class SubmissionMetadataRequest
{
    public string? ReferenceNumber { get; init; }
    public string? UserId { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public string? Name { get; init; }
    public string? FileLocation { get; init; }
    public string? SubmissionMnthYr { get; init; }
    public string? SubmissionProcessName { get; init; }
    public string? NotificationEmail { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public string? SubmissionStatus { get; init; }
    public string? EtlProcessStatus { get; init; }
    public string? LaboratoryId { get; init; }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ReferenceNumber) && ReferenceNumber.Length <= 50 &&
        !string.IsNullOrWhiteSpace(UserId) && UserId.Length <= 50 &&
        SubmittedAt is not null &&
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 500 &&
        !string.IsNullOrWhiteSpace(FileLocation) && FileLocation.Length <= 500 &&
        !string.IsNullOrWhiteSpace(SubmissionProcessName) && SubmissionProcessName.Length <= 50;
}