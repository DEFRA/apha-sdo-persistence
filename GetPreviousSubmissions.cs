using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Security.Claims;

namespace Apha.Sdo.Persistence.Functions;

public sealed class GetPreviousSubmissions
{
    private readonly ISubmissionQueryRepository _repository;

    public GetPreviousSubmissions(ISubmissionQueryRepository repository)
    {
        _repository = repository;
    }

    [Function("GetPreviousSubmissions")]
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

        var submissions = await _repository.GetSummariesAsync(laboratoryId, cancellationToken);
        return new OkObjectResult(submissions);
    }
}

internal static class LaboratoryIdClaim
{
    private static readonly string[] ClaimTypes =
    [
        "laboratoryId",
        "laboratory_id",
        "labId",
        "extension_LaboratoryId"
    ];

    public static string? Get(ClaimsPrincipal principal)
    {
        foreach (var claimType in ClaimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

public interface ISubmissionQueryRepository
{
    Task<IReadOnlyList<SubmissionSummary>> GetSummariesAsync(
        string laboratoryId,
        CancellationToken cancellationToken);
}

public sealed class SubmissionQueryRepository : ISubmissionQueryRepository
{
    private const string SelectCommand = """
        SELECT SubmissionID, UploadReferenceNumber, SubmissionProcessName,
               NotificationEmail, SubmissionDate
        FROM dbo.Batch_Submission_Message
        WHERE LaboratoryId = @LaboratoryId
        ORDER BY SubmissionDate DESC;
        """;

    private readonly string? _connectionString;

    public SubmissionQueryRepository(IConfiguration configuration)
    {
        _connectionString = configuration["SqlConnectionString"];
    }

    public async Task<IReadOnlyList<SubmissionSummary>> GetSummariesAsync(
        string laboratoryId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("The SqlConnectionString application setting has not been configured.");
        }

        var submissions = new List<SubmissionSummary>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectCommand, connection);
        command.Parameters.Add("@LaboratoryId", SqlDbType.NVarChar, 50).Value = laboratoryId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            submissions.Add(new SubmissionSummary(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetDateTime(4)));
        }

        return submissions;
    }
}

public sealed record SubmissionSummary(
    Guid SubmissionId,
    string ReferenceNumber,
    string Form,
    string? NotificationEmail,
    DateTime SubmittedDate);
