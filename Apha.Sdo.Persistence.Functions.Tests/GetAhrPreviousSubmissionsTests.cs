using Apha.Sdo.Persistence.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Xunit;

namespace Apha.Sdo.Persistence.Functions.Tests;

public class GetAhrPreviousSubmissionsTests
{
    [Fact]
    public async Task Run_WithAuthenticatedLaboratoryUser_ReturnsSummariesFilteredToAhrProcess()
    {
        var repository = new RecordingQueryRepository();
        var function = new GetAhrPreviousSubmissions(repository);
        var request = CreateRequest(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("laboratoryId", "LAB-123")], "Entra")));
        var expected = new SubmissionSummary(
            Guid.NewGuid(), "REF-001", "AHR", "notify@example.gov.uk", DateTime.UtcNow);
        repository.Results.Add(expected);

        var result = await function.Run(request, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Same(repository.Results, response.Value);
        Assert.Equal("LAB-123", repository.LaboratoryId);
        Assert.Equal("AHR", repository.ProcessName);
    }

    [Fact]
    public async Task Run_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        var repository = new RecordingQueryRepository();
        var function = new GetAhrPreviousSubmissions(repository);

        var result = await function.Run(CreateRequest(new ClaimsPrincipal()), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Null(repository.LaboratoryId);
    }

    [Fact]
    public async Task Run_WithoutLaboratoryClaim_ReturnsForbidden()
    {
        var repository = new RecordingQueryRepository();
        var function = new GetAhrPreviousSubmissions(repository);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "user@example.gov.uk")], "Entra"));

        var result = await function.Run(CreateRequest(principal), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(repository.LaboratoryId);
    }

    private static HttpRequest CreateRequest(ClaimsPrincipal user)
    {
        var context = new DefaultHttpContext { User = user };
        return context.Request;
    }

    private sealed class RecordingQueryRepository : ISubmissionQueryRepository
    {
        public string? LaboratoryId { get; private set; }
        public string? ProcessName { get; private set; }
        public List<SubmissionSummary> Results { get; } = [];

        public Task<IReadOnlyList<SubmissionSummary>> GetSummariesAsync(
            string laboratoryId,
            string? processName,
            CancellationToken cancellationToken)
        {
            LaboratoryId = laboratoryId;
            ProcessName = processName;
            return Task.FromResult<IReadOnlyList<SubmissionSummary>>(Results);
        }
    }
}
