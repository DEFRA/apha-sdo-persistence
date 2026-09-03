CREATE TABLE dbo.Batch_Submission_Message
(
    SubmissionID UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    UploadReferenceNumber NVARCHAR(50) NOT NULL,
    UserId NVARCHAR(50) NOT NULL,
    SubmissionDate DATETIME2 NOT NULL,
    FileName NVARCHAR(500) NOT NULL,
    FileLocation NVARCHAR(500) NOT NULL,
    SubmissionMnthYr NVARCHAR(10) NULL,
    SubmissionProcessName NVARCHAR(50) NOT NULL,
    CreatedDateTime DATETIME2 NOT NULL,
    CreatedBy NVARCHAR(50) NOT NULL,
    FailureCode NVARCHAR(100) NULL,
    FailureReason NVARCHAR(200) NULL,
    SubmissionStatus NVARCHAR(10) NOT NULL,
    ETLProcessStatus NVARCHAR(10) NOT NULL,
    UpdateDateTime DATETIME2 NOT NULL,
    UpdatedBy NVARCHAR(100) NOT NULL,
    LaboratoryId NVARCHAR(50) NULL
);