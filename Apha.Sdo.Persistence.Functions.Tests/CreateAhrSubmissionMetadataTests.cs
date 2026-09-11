using System.Text;
using Apha.Sdo.Persistence.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Apha.Sdo.Persistence.Functions.Tests;

public class CreateAhrSubmissionMetadataTests
{
    [Fact]
    public async Task Run_WithValidSubmission_PersistsWithAhrProcessNameAndReturnsCreated()
    {
        var repository = new RecordingRepository();
        var function = new CreateAhrSubmissionMetadata(NullLogger<CreateAhrSubmissionMetadata>.Instance, repository);
        var request = CreateRequest("""
            {
              "referenceNumber": "REF-001",
              "userId": "user@example.gov.uk",
              "submittedAt": "2026-08-28T10:00:00Z",
              "name": "submission.json",
              "fileLocation": "https://storage.example/submission.json",
              "submissionProcessName": "BR",
              "laboratoryId": "LAB-123"
            }
            """);

        var result = await function.Run(request, CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        Assert.Equal("AHR", repository.Submission?.SubmissionProcessName);
        Assert.Equal("LAB-123", repository.Submission?.LaboratoryId);
    }

    [Fact]
    public async Task Run_WithoutLaboratoryId_ReturnsBadRequestWithoutPersisting()
    {
        var repository = new RecordingRepository();
        var function = new CreateAhrSubmissionMetadata(NullLogger<CreateAhrSubmissionMetadata>.Instance, repository);
        var request = CreateRequest("""
            {
              "referenceNumber": "REF-001",
              "userId": "user@example.gov.uk",
              "submittedAt": "2026-08-28T10:00:00Z",
              "name": "submission.json",
              "fileLocation": "https://storage.example/submission.json",
              "submissionProcessName": "BR"
            }
            """);

        var result = await function.Run(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(repository.Submission);
    }

    [Fact]
    public async Task Run_WithInvalidJson_ReturnsBadRequestWithoutPersisting()
    {
        var repository = new RecordingRepository();
        var function = new CreateAhrSubmissionMetadata(NullLogger<CreateAhrSubmissionMetadata>.Instance, repository);

        var result = await function.Run(CreateRequest("not json"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(repository.Submission);
    }

    [Fact]
    public async Task Run_WhenMandatoryFieldsAreMissing_ReturnsBadRequestWithoutPersisting()
    {
        var repository = new RecordingRepository();
        var function = new CreateAhrSubmissionMetadata(NullLogger<CreateAhrSubmissionMetadata>.Instance, repository);

        var result = await function.Run(CreateRequest("{ \"referenceNumber\": \"REF-001\" }"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(repository.Submission);
    }

    private static HttpRequest CreateRequest(string json)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private sealed class RecordingRepository : ISubmissionMetadataRepository
    {
        public SubmissionMetadataRequest? Submission { get; private set; }

        public Task<Guid> CreateAsync(SubmissionMetadataRequest submission, CancellationToken cancellationToken)
        {
            Submission = submission;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
