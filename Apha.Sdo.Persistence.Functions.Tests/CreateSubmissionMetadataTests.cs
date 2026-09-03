using System.Text;
using Apha.Sdo.Persistence.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Apha.Sdo.Persistence.Functions.Tests;

public class CreateSubmissionMetadataTests
{
    [Fact]
    public async Task Run_WithValidSubmission_PersistsMetadataAndReturnsCreated()
    {
        var repository = new RecordingRepository();
        var function = new CreateSubmissionMetadata(NullLogger<CreateSubmissionMetadata>.Instance, repository);
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
        Assert.Equal("REF-001", repository.Submission?.ReferenceNumber);
        Assert.Equal("LAB-123", repository.Submission?.LaboratoryId);
    }

    [Fact]
    public async Task Run_WithInvalidJson_ReturnsBadRequestWithoutPersisting()
    {
        var repository = new RecordingRepository();
        var function = new CreateSubmissionMetadata(NullLogger<CreateSubmissionMetadata>.Instance, repository);

        var result = await function.Run(CreateRequest("not json"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(repository.Submission);
    }

    [Fact]
    public async Task Run_WhenMandatoryFieldsAreMissing_ReturnsBadRequestWithoutPersisting()
    {
        var repository = new RecordingRepository();
        var function = new CreateSubmissionMetadata(NullLogger<CreateSubmissionMetadata>.Instance, repository);

        var result = await function.Run(CreateRequest("{ \"referenceNumber\": \"REF-001\" }"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(repository.Submission);
    }

    [Fact]
    public void IsValid_WhenReferenceNumberExceedsMaximumLength_ReturnsFalse()
    {
        var submission = ValidSubmission(referenceNumber: new string('a', 51));

        Assert.False(submission.IsValid());
    }

    [Fact]
    public void IsValid_WhenMandatoryFieldsArePresent_ReturnsTrue()
    {
        Assert.True(ValidSubmission().IsValid());
    }

    private static HttpRequest CreateRequest(string json)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentType = "application/json";
        return context.Request;
    }

    private static SubmissionMetadataRequest ValidSubmission(string referenceNumber = "REF-001") => new()
    {
        ReferenceNumber = referenceNumber,
        UserId = "user@example.gov.uk",
        SubmittedAt = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc),
        Name = "submission.json",
        FileLocation = "https://storage.example/submission.json",
        SubmissionProcessName = "BR"
    };

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