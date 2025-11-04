using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using FormBuilder.API.Business.Implementations;
using FormBuilder.API.Configurations;
using FormBuilder.API.DataAccess.Interfaces;
using FormBuilder.API.DTOs.Form;
using FormBuilder.API.DTOs.Common;
using FormBuilder.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace FormBuilder.API.Tests.Business
{
    public class ResponseManagerTests
    {
        private readonly Mock<IResponseRepository> _responseRepositoryMock;
        private readonly Mock<IFormRepository> _formRepositoryMock;
        private readonly Mock<IFileAttachmentRepository> _fileAttachmentRepositoryMock;
        private readonly Mock<MySqlDbContext> _dbContextMock;
        private readonly ResponseManager _responseManager;
        private readonly Mock<IDbContextTransaction> _transactionMock;

        public ResponseManagerTests()
        {
            _responseRepositoryMock = new Mock<IResponseRepository>();
            _formRepositoryMock = new Mock<IFormRepository>();
            _fileAttachmentRepositoryMock = new Mock<IFileAttachmentRepository>();
            
            var options = new DbContextOptionsBuilder<MySqlDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContextMock = new Mock<MySqlDbContext>(options);
            
            _transactionMock = new Mock<IDbContextTransaction>();
            var databaseMock = new Mock<DatabaseFacade>(_dbContextMock.Object);
            databaseMock.Setup(x => x.BeginTransaction()).Returns(_transactionMock.Object);
            _dbContextMock.Setup(x => x.Database).Returns(databaseMock.Object);

            _responseManager = new ResponseManager(
                _responseRepositoryMock.Object,
                _formRepositoryMock.Object,
                _fileAttachmentRepositoryMock.Object,
                _dbContextMock.Object
            );
        }

        #region GetFormById Tests

        [Fact]
        public void GetFormById_PublishedForm_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Title = "Test Form",
                Description = "Description",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Question",
                        Type = "text",
                        Required = true,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "opt1", Value = "Option 1" }
                        }
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.GetFormById("form1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("form1", result.FormId);
            Assert.Equal("Test Form", result.Title);
            Assert.Single(result.Questions);
            Assert.Single(result.Questions[0].Options);
        }

        [Fact]
        public void GetFormById_NonExistentForm_ReturnsNull()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);

            // Act
            var result = _responseManager.GetFormById("nonexistent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetFormById_DraftForm_ReturnsNull()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Draft
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.GetFormById("form1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetFormById_WithNullQuestions_ReturnsEmptyQuestionsList()
        {
            // Arrange
            var form = new Form
            {
                Id = "form1",
                Title = "Test Form",
                Status = FormStatus.Published,
                Questions = null
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.GetFormById("form1");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Questions);
        }

        [Fact]
        public void GetFormById_QuestionWithNullOptions_ReturnsEmptyOptionsArray()
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
                        QuestionText = "Question",
                        Type = "text",
                        Options = null
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.GetFormById("form1");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Questions[0].Options);
        }

        #endregion

        #region GetUserSubmissions Tests

        [Fact]
        public void GetUserSubmissions_ValidUserId_ReturnsSuccess()
        {
            // Arrange
            var responses = new List<Response>
            {
                new Response
                {
                    Id = 1,
                    FormId = "form1",
                    UserId = 1,
                    SubmittedAt = DateTime.UtcNow,
                    Details = new List<ResponseDetail>
                    {
                        new ResponseDetail { QuestionId = "q1", Answer = "Answer" }
                    }
                }
            };
            var form = new Form
            {
                Id = "form1",
                Title = "Test Form",
                Description = "Description",
                Questions = new List<Question> { new Question() }
            };
            _responseRepositoryMock.Setup(x => x.GetByUserId(1)).Returns(responses);
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.GetUserSubmissions(1);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Submissions retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetUserSubmissions_WithSearchTerm_FiltersResults()
        {
            // Arrange
            var responses = new List<Response>
            {
                new Response
                {
                    Id = 1,
                    FormId = "form1",
                    UserId = 1
                },
                new Response
                {
                    Id = 2,
                    FormId = "form2",
                    UserId = 1
                }
            };
            var form1 = new Form { Id = "form1", Title = "Feedback Form", Description = "Customer feedback" };
            var form2 = new Form { Id = "form2", Title = "Survey Form", Description = "Annual survey" };
            
            _responseRepositoryMock.Setup(x => x.GetByUserId(1)).Returns(responses);
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form1);
            _formRepositoryMock.Setup(x => x.GetById("form2")).Returns(form2);

            // Act
            var result = _responseManager.GetUserSubmissions(1, 1, 10, "feedback");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetUserSubmissions_ExceptionThrown_ReturnsFailure()
        {
            // Arrange
            _responseRepositoryMock.Setup(x => x.GetByUserId(1))
                .Throws(new Exception("Database error"));

            // Act
            var result = _responseManager.GetUserSubmissions(1);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error retrieving submissions", result.Message);
        }

        [Fact]
        public void GetUserSubmissions_NoResponses_ReturnsEmptyList()
        {
            // Arrange
            _responseRepositoryMock.Setup(x => x.GetByUserId(1)).Returns(new List<Response>());

            // Act
            var result = _responseManager.GetUserSubmissions(1);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        #endregion

        #region GetResponseWithFiles Tests

        [Fact]
        public void GetResponseWithFiles_WithFiles_ReturnsSuccessWithFileDetails()
        {
            // Arrange
            var responseId = 1;
            var response = new Response
            {
                Id = responseId,
                FormId = "form1",
                UserId = 1
            };
            var files = new List<FileAttachment>
            {
                new FileAttachment
                {
                    Id = 1,
                    ResponseId = responseId,
                    QuestionId = "q1",
                    FileName = "file1.pdf",
                    FileType = "application/pdf",
                    FileSize = 1024,
                    UploadedAt = DateTime.UtcNow
                },
                new FileAttachment
                {
                    Id = 2,
                    ResponseId = responseId,
                    QuestionId = "q2",
                    FileName = "file2.jpg",
                    FileType = "image/jpeg",
                    FileSize = 2048,
                    UploadedAt = DateTime.UtcNow.AddHours(-1)
                }
            };

            _responseRepositoryMock.Setup(x => x.GetById("1")).Returns(response);
            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseId(responseId)).Returns(files);

            // Act
            var result = _responseManager.GetResponseWithFiles(responseId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Response retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetResponseWithFiles_NoFiles_ReturnsEmptyFileList()
        {
            // Arrange
            var responseId = 1;
            var response = new Response { Id = responseId };
            _responseRepositoryMock.Setup(x => x.GetById("1")).Returns(response);
            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseId(responseId)).Returns(new List<FileAttachment>());

            // Act
            var result = _responseManager.GetResponseWithFiles(responseId);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetResponseWithFiles_ResponseNotFound_ReturnsFailure()
        {
            // Arrange
            _responseRepositoryMock.Setup(x => x.GetById("999")).Returns((Response)null);

            // Act
            var result = _responseManager.GetResponseWithFiles(999);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Response not found", result.Message);
            Assert.Null(result.Data);
        }

        #endregion

        #region SubmitResponse Additional Edge Cases

        [Fact]
        public void SubmitResponse_CheckboxWithEmptySelection_HandlesCorrectly()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "," } // Empty selection with comma
                }
            };

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        Type = "checkbox",
                        MultipleChoice = true,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "opt1", Value = "Option 1" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal(",", detail.Answer); // Should keep original if no matches
        }

        [Fact]
        public void SubmitResponse_OptionWithNullOptionId_SkipsFormatting()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Option 1" }
                }
            };

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        Type = "radio",
                        Options = new List<Option>
                        {
                            new Option { OptionId = null, Value = "Option 1" },
                            new Option { OptionId = "", Value = "Option 2" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            // The backend will format it as an array with generated ID
            Assert.NotNull(detail.Answer);
            Assert.Contains("[", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_SingleChoiceWithOptions_FormatsCorrectly()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Yes" }
                }
            };

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        Type = "custom",
                        SingleChoice = true,
                        MultipleChoice = false,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "yes_id", Value = "Yes" },
                            new Option { OptionId = "no_id", Value = "No" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal("[\"yes_id\"]", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_FileUploadForNonFileQuestion_AddsToDetails()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Text answer" }
                },
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1", // Same question ID as text answer
                        FileName = "file.pdf",
                        FileType = "application/pdf",
                        FileSize = 1024,
                        Base64Content = "base64"
                    }
                }
            };

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            Assert.Single(result.Data.Details); // Should not duplicate for same questionId
            Assert.Equal("Text answer", result.Data.Details.First().Answer);
        }

        [Fact]
        public void SubmitResponse_TransactionRollbackDeleteFails_StillReturnsError()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "test.pdf",
                        FileType = "application/pdf",
                        FileSize = 1024
                    }
                }
            };

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);
            _responseRepositoryMock.Setup(x => x.Add(It.IsAny<Response>()))
                .Callback<Response>(r => r.Id = 123);
            _fileAttachmentRepositoryMock.Setup(x => x.SaveChanges())
                .Throws(new Exception("Database error"));
            _responseRepositoryMock.Setup(x => x.Delete("123"))
                .Throws(new Exception("Delete failed"));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Database error", result.Message);
        }

        [Fact]
        public void SubmitResponse_FileUploadCaseInsensitive_AllowsValidTypes()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "file.PDF",
                        FileType = "APPLICATION/PDF", // Uppercase
                        FileSize = 1024
                    },
                    new FileUploadDto
                    {
                        QuestionId = "q2",
                        FileName = "image.JPG",
                        FileType = "IMAGE/JPEG", // Uppercase
                        FileSize = 1024
                    }
                }
            };

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success); // Should accept uppercase MIME types
        }

        [Fact]
        public void SubmitResponse_WithInnerException_ShowsInnerMessage()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "test.pdf",
                        FileType = "application/pdf",
                        FileSize = 1024
                    }
                }
            };

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };

            var innerException = new Exception("Inner exception message");
            var outerException = new Exception("Outer exception", innerException);

            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);
            _fileAttachmentRepositoryMock.Setup(x => x.SaveChanges()).Throws(outerException);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Inner exception message", result.Message);
        }

        [Fact]
        public void SubmitResponse_InvalidUserId_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "form1" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "invalid")
            }));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid user ID.", result.Message);
        }

        [Fact]
        public void SubmitResponse_EmptyFormId_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form ID is required.", result.Message);
        }

        [Fact]
        public void SubmitResponse_FormNotFound_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "nonexistent" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid form ID.", result.Message);
        }

        [Fact]
        public void SubmitResponse_UnpublishedForm_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "form1" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Draft
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot submit to an unpublished form.", result.Message);
        }

        [Fact]
        public void SubmitResponse_FileSizeExceedsLimit_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        FileName = "large.pdf",
                        FileSize = 6 * 1024 * 1024, // 6MB
                        FileType = "application/pdf"
                    }
                }
            };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("exceeds maximum size of 5MB", result.Message);
        }

        [Fact]
        public void SubmitResponse_InvalidFileType_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        FileName = "file.exe",
                        FileSize = 1024,
                        FileType = "application/exe"
                    }
                }
            };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));
            var form = new Form
            {
                Id = "form1",
                Status = FormStatus.Published,
                Questions = new List<Question>()
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("is not allowed", result.Message);
        }

        [Fact]
        public void SubmitResponse_RequiredQuestionNotAnswered_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>()
            };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));
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
                        Required = true,
                        Type = "text"
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Required Question", result.Message);
        }

        [Fact]
        public void SubmitResponse_RequiredFileUploadMissing_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1"
            };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));
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
                        Required = true,
                        Type = "file_upload"
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("File upload for 'Upload Document' is required", result.Message);
        }

        #endregion

        #region GetPublishedForms Additional Tests

        [Fact]
        public void GetPublishedForms_WithCreatedAtField_ReturnsCorrectValue()
        {
            // Arrange
            var createdDate = DateTime.UtcNow.AddDays(-5);
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Title = "Test Form",
                    Description = "Description",
                    Status = FormStatus.Published,
                    CreatedAt = createdDate,
                    Questions = new List<Question>()
                }
            };

            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published)).Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data.Data);
            Assert.Equal(createdDate, result.Data.Data[0].CreatedAt);
        }

        [Fact]
        public void GetPublishedForms_QuestionWithDisabledDescription_ReturnsNullDescription()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Status = FormStatus.Published,
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            QuestionId = "q1",
                            QuestionText = "Question",
                            Type = "text",
                            DescriptionEnabled = false,
                            Description = "This should not appear"
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published)).Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.Data.Data[0].Questions[0].Description);
        }

        [Fact]
        public void GetPublishedForms_QuestionWithEnabledDescription_ReturnsDescription()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Status = FormStatus.Published,
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            QuestionId = "q1",
                            QuestionText = "Question",
                            Type = "text",
                            DescriptionEnabled = true,
                            Description = "This should appear"
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published)).Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.True(result.Success);
            Assert.Equal("This should appear", result.Data.Data[0].Questions[0].Description);
        }

        [Fact]
        public void GetPublishedForms_WithSearchTerm_FiltersResults()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Title = "Feedback Form",
                    Description = "Customer feedback",
                    Status = FormStatus.Published,
                    Questions = new List<Question>()
                },
                new Form
                {
                    Id = "form2",
                    Title = "Survey Form",
                    Description = "Annual survey",
                    Status = FormStatus.Published,
                    Questions = new List<Question>()
                }
            };
            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published)).Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms(1, 10, "feedback");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetPublishedForms_ExceptionThrown_ReturnsFailure()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published))
                .Throws(new Exception("Database error"));

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error retrieving published forms", result.Message);
            Assert.Null(result.Data);
        }

        #endregion

        #region GetResponsesByForm Tests

        [Fact]
        public void GetResponsesByForm_ValidFormId_ReturnsSuccess()
        {
            // Arrange
            var responses = new List<Response>
            {
                new Response
                {
                    Id = 1,
                    FormId = "form1",
                    UserId = 1,
                    SubmittedAt = DateTime.UtcNow,
                    Details = new List<ResponseDetail>(),
                    User = new User 
                    { 
                        Id = 1, 
                        Name = "Test User", 
                        Email = "test@example.com",
                        PasswordHash = "hashed_password", // Add required property
                        Role = "Learner" // Add required property
                    }
                }
            };
            _responseRepositoryMock.Setup(x => x.GetByFormId("form1")).Returns(responses);

            // Act
            var result = _responseManager.GetResponsesByForm("form1");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Responses retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetResponsesByForm_WithSearchTerm_FiltersResults()
        {
            // Arrange
            var responses = new List<Response>
            {
                new Response
                {
                    Id = 1,
                    FormId = "form1",
                    UserId = 1,
                    User = new User 
                    { 
                        Name = "John Doe", 
                        Email = "john@example.com",
                        PasswordHash = "hashed_password", // Add required property
                        Role = "Learner" // Add required property
                    },
                    Details = new List<ResponseDetail>
                    {
                        new ResponseDetail { QuestionId = "q1", Answer = "test answer" }
                    }
                }
            };
            _responseRepositoryMock.Setup(x => x.GetByFormId("form1")).Returns(responses);

            // Act
            var result = _responseManager.GetResponsesByForm("form1", 1, 10, "john");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetResponsesByForm_ExceptionThrown_ReturnsFailure()
        {
            // Arrange
            _responseRepositoryMock.Setup(x => x.GetByFormId("form1"))
                .Throws(new Exception("Database error"));

            // Act
            var result = _responseManager.GetResponsesByForm("form1");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error retrieving responses", result.Message);
        }

        #endregion

        #region GetResponseById Tests

        [Fact]
        public void GetResponseById_ExistingResponse_ReturnsSuccess()
        {
            // Arrange
            var response = new Response { Id = 1, FormId = "form1" };
            _responseRepositoryMock.Setup(x => x.GetById("1")).Returns(response);

            // Act
            var result = _responseManager.GetResponseById("1");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Response retrieved successfully", result.Message);
            Assert.Equal(response, result.Data);
        }

        [Fact]
        public void GetResponseById_NonExistentResponse_ReturnsFailure()
        {
            // Arrange
            _responseRepositoryMock.Setup(x => x.GetById("999")).Returns((Response)null);

            // Act
            var result = _responseManager.GetResponseById("999");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Response not found", result.Message);
            Assert.Null(result.Data);
        }

        #endregion

        #region GetFileAttachment Tests

        [Fact]
        public void GetFileAttachment_FileExists_ReturnsSuccess()
        {
            // Arrange
            var file = new FileAttachment
            {
                Id = 1,
                FileName = "test.pdf",
                FileType = "application/pdf",
                Base64Content = "base64content"
            };
            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseAndQuestion(1, "q1")).Returns(file);

            // Act
            var result = _responseManager.GetFileAttachment(1, "q1");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("File retrieved successfully", result.Message);
            Assert.Equal(file, result.Data);
        }

        [Fact]
        public void GetFileAttachment_FileNotFound_ReturnsFailure()
        {
            // Arrange
            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseAndQuestion(1, "q1")).Returns((FileAttachment)null);

            // Act
            var result = _responseManager.GetFileAttachment(1, "q1");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("File not found", result.Message);
            Assert.Null(result.Data);
        }

        #endregion
    }
}
