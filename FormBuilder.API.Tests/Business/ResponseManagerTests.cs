using Xunit;
using Moq;
using FormBuilder.API.Business.Implementations;
using FormBuilder.API.DataAccess.Interfaces;
using FormBuilder.API.DTOs.Form;
using FormBuilder.API.Models;
using FormBuilder.API.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FormBuilder.API.Tests.Business
{
    public class ResponseManagerTests : IDisposable
    {
        private readonly Mock<IResponseRepository> _responseRepositoryMock;
        private readonly Mock<IFormRepository> _formRepositoryMock;
        private readonly Mock<IFileAttachmentRepository> _fileAttachmentRepositoryMock;
        private readonly MySqlDbContext _dbContext;
        private readonly ResponseManager _responseManager;

        public ResponseManagerTests()
        {
            _responseRepositoryMock = new Mock<IResponseRepository>();
            _formRepositoryMock = new Mock<IFormRepository>();
            _fileAttachmentRepositoryMock = new Mock<IFileAttachmentRepository>();
            
            // Create actual in-memory database context
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            var options = new DbContextOptionsBuilder<MySqlDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .UseInternalServiceProvider(serviceProvider)
                .Options;
            
            _dbContext = new MySqlDbContext(options);
            
            _responseManager = new ResponseManager(
                _responseRepositoryMock.Object,
                _formRepositoryMock.Object,
                _fileAttachmentRepositoryMock.Object,
                _dbContext
            );
        }

        private ClaimsPrincipal CreateUserPrincipal(string userId = "1", string role = "Learner")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        [Fact]
        public void GetPublishedForms_ReturnsOnlyPublishedForms()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Title = "Published Form",
                    Description = "Description",
                    Status = FormStatus.Published,
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            QuestionId = "q1",
                            QuestionText = "Question 1",
                            Type = "text",
                            Required = true,
                            Options = new List<Option>()
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published))
                .Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.Single(result);
            Assert.Equal("Published Form", result[0].Title);
            Assert.Equal(FormStatusDto.Published, result[0].Status);
        }

        [Fact]
        public void GetResponsesByForm_ReturnsAllResponses()
        {
            // Arrange
            var responses = new List<Response>
            {
                new Response { Id = 1, FormId = "form1", UserId = 1 },
                new Response { Id = 2, FormId = "form1", UserId = 2 }
            };

            _responseRepositoryMock.Setup(x => x.GetByFormId("form1"))
                .Returns(responses);

            // Act
            var result = _responseManager.GetResponsesByForm("form1");

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, r => Assert.Equal("form1", r.FormId));
        }

        [Fact]
        public void GetResponseById_ExistingResponse_ReturnsSuccess()
        {
            // Arrange
            var response = new Response { Id = 1, FormId = "form1", UserId = 1 };
            _responseRepositoryMock.Setup(x => x.GetById("1"))
                .Returns(response);

            // Act
            var result = _responseManager.GetResponseById("1");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Response retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.Id);
        }

        [Fact]
        public void GetResponseById_NonExistentResponse_ReturnsFalse()
        {
            // Arrange
            _responseRepositoryMock.Setup(x => x.GetById("999"))
                .Returns((Response)null);

            // Act
            var result = _responseManager.GetResponseById("999");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Response not found", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_ValidSubmission_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Question 1",
                        Type = "text",
                        Required = true,
                        Options = new List<Option>()
                    }
                }
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Answer 1" }
                }
            };

            var user = CreateUserPrincipal("1");

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Response submitted successfully", result.Message);
            Assert.NotNull(result.Data);
            _responseRepositoryMock.Verify(x => x.Add(It.IsAny<Response>()), Times.Once);
        }

        [Fact]
        public void SubmitResponse_InvalidUserId_ReturnsFalse()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "form1" };
            var user = new ClaimsPrincipal(); // No claims

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid user ID.", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_EmptyFormId_ReturnsFalse()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "" };
            var user = CreateUserPrincipal("1");

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form ID is required.", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_InvalidFormId_ReturnsFalse()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "invalid" };
            var user = CreateUserPrincipal("1");

            _formRepositoryMock.Setup(x => x.GetById("invalid"))
                .Returns((Form)null);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid form ID.", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_UnpublishedForm_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Draft
            };

            var dto = new FormSubmissionDto { FormId = "form1" };
            var user = CreateUserPrincipal("1");

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot submit to an unpublished form.", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_FileTooLarge_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "large.pdf",
                        FileType = "application/pdf",
                        FileSize = 6 * 1024 * 1024, // 6MB
                        Base64Content = "base64content"
                    }
                }
            };

            var user = CreateUserPrincipal("1");
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("exceeds maximum size of 5MB", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_InvalidFileType_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "file.exe",
                        FileType = "application/exe",
                        FileSize = 1024,
                        Base64Content = "base64content"
                    }
                }
            };

            var user = CreateUserPrincipal("1");
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("File type", result.Message);
            Assert.Contains("is not allowed", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_MissingRequiredAnswer_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Required Question",
                        Type = "text",
                        Required = true
                    }
                }
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>() // Empty answers
            };

            var user = CreateUserPrincipal("1");
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Required Question", result.Message);
            Assert.Contains("is required", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_MissingRequiredFileUpload_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Upload Document",
                        Type = "fileupload",
                        Required = true
                    }
                }
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = null // No file uploads
            };

            var user = CreateUserPrincipal("1");
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("File upload for 'Upload Document' is required", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void SubmitResponse_WithCheckboxOptions_FormatsCorrectly()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Select Options",
                        Type = "checkbox",
                        MultipleChoice = true,
                        Required = false,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "opt1", Value = "Option 1" },
                            new Option { OptionId = "opt2", Value = "Option 2" }
                        }
                    }
                }
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Option 1,Option 2" }
                }
            };

            var user = CreateUserPrincipal("1");
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            _responseRepositoryMock.Verify(x => x.Add(It.Is<Response>(r =>
                r.Details.Any(d => d.Answer.Contains("opt1") && d.Answer.Contains("opt2"))
            )), Times.Once);
        }

        [Fact]
        public void SubmitResponse_WithRadioOption_FormatsCorrectly()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Select One",
                        Type = "radio",
                        Required = false,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "opt1", Value = "Yes" },
                            new Option { OptionId = "opt2", Value = "No" }
                        }
                    }
                }
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Yes" }
                }
            };

            var user = CreateUserPrincipal("1");
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            _responseRepositoryMock.Verify(x => x.Add(It.Is<Response>(r =>
                r.Details.Any(d => d.Answer.Contains("opt1"))
            )), Times.Once);
        }

        [Fact]
        public void SubmitResponse_WithFileUploads_SavesAttachments()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "document.pdf",
                        FileType = "application/pdf",
                        FileSize = 1024,
                        Base64Content = "base64content"
                    }
                }
            };

            var user = CreateUserPrincipal("1");
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.True(result.Success);
            _fileAttachmentRepositoryMock.Verify(x => x.AddRange(It.Is<List<FileAttachment>>(
                files => files.Count == 1 && files[0].FileName == "document.pdf"
            )), Times.Once);
            _fileAttachmentRepositoryMock.Verify(x => x.SaveChanges(), Times.Once);
        }

        [Fact]
        public void GetFileAttachment_ExistingFile_ReturnsSuccess()
        {
            // Arrange
            var file = new FileAttachment
            {
                Id = 1,
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "document.pdf",
                FileType = "application/pdf",
                Base64Content = "base64content"
            };

            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseAndQuestion(1, "q1"))
                .Returns(file);

            // Act
            var result = _responseManager.GetFileAttachment(1, "q1");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("File retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal("document.pdf", result.Data.FileName);
        }

        [Fact]
        public void GetFileAttachment_NonExistentFile_ReturnsFalse()
        {
            // Arrange
            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseAndQuestion(999, "q1"))
                .Returns((FileAttachment)null);

            // Act
            var result = _responseManager.GetFileAttachment(999, "q1");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("File not found", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void GetResponseWithFiles_ExistingResponse_ReturnsSuccess()
        {
            // Arrange
            var response = new Response
            {
                Id = 1,
                FormId = "form1",
                UserId = 1,
                SubmittedAt = DateTime.UtcNow
            };

            var files = new List<FileAttachment>
            {
                new FileAttachment
                {
                    Id = 1,
                    ResponseId = 1,
                    QuestionId = "q1",
                    FileName = "file1.pdf",
                    FileType = "application/pdf",
                    FileSize = 1024,
                    UploadedAt = DateTime.UtcNow
                }
            };

            _responseRepositoryMock.Setup(x => x.GetById("1")).Returns(response);
            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseId(1)).Returns(files);

            // Act
            var result = _responseManager.GetResponseWithFiles(1);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Response retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetResponseWithFiles_NonExistentResponse_ReturnsFalse()
        {
            // Arrange
            _responseRepositoryMock.Setup(x => x.GetById("999"))
                .Returns((Response)null);

            // Act
            var result = _responseManager.GetResponseWithFiles(999);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Response not found", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void GetPublishedForms_HandlesNullOptions_ReturnsEmptyArray()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Title = "Form with null options",
                    Status = FormStatus.Published,
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            QuestionId = "q1",
                            QuestionText = "Question",
                            Type = "text",
                            Options = null // Null options
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published))
                .Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.Single(result);
            Assert.Empty(result[0].Questions[0].Options);
        }

        [Fact]
        public void SubmitResponse_UsesNameIdClaim_WhenNameIdentifierMissing()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            var dto = new FormSubmissionDto { FormId = "form1" };
            
            var claims = new List<Claim> { new Claim("nameId", "2") };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, user);

            // Assert
            Assert.True(result.Success);
            _responseRepositoryMock.Verify(x => x.Add(It.Is<Response>(r => r.UserId == 2)), Times.Once);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
        }
    }
}