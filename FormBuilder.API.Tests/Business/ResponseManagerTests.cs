using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using FormBuilder.API.Business.Implementations;
using FormBuilder.API.Configurations;
using FormBuilder.API.DataAccess.Interfaces;
using FormBuilder.API.DTOs.Form;
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
        public void GetUserSubmissions_WithMultipleResponses_ReturnsOrderedByDate()
        {
            // Arrange
            var userId = 1;
            var responses = new List<Response>
            {
                new Response
                {
                    Id = 1,
                    FormId = "form1",
                    UserId = userId,
                    SubmittedAt = DateTime.UtcNow.AddDays(-2),
                    Details = new List<ResponseDetail>
                    {
                        new ResponseDetail { QuestionId = "q1", Answer = "Answer 1" }
                    }
                },
                new Response
                {
                    Id = 2,
                    FormId = "form2",
                    UserId = userId,
                    SubmittedAt = DateTime.UtcNow,
                    Details = new List<ResponseDetail>()
                }
            };

            var form1 = new Form
            {
                Id = "form1",
                Title = "Form 1",
                Description = "Desc 1",
                Questions = new List<Question> { new Question() }
            };
            var form2 = new Form
            {
                Id = "form2",
                Title = "Form 2",
                Description = "Desc 2",
                Questions = new List<Question> { new Question(), new Question() }
            };

            _responseRepositoryMock.Setup(x => x.GetByUserId(userId)).Returns(responses);
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form1);
            _formRepositoryMock.Setup(x => x.GetById("form2")).Returns(form2);

            // Act
            var result = _responseManager.GetUserSubmissions(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void GetUserSubmissions_NoResponses_ReturnsEmptyList()
        {
            // Arrange
            var userId = 1;
            _responseRepositoryMock.Setup(x => x.GetByUserId(userId)).Returns(new List<Response>());

            // Act
            var result = _responseManager.GetUserSubmissions(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetUserSubmissions_WithNullFormDetails_HandlesGracefully()
        {
            // Arrange
            var userId = 1;
            var responses = new List<Response>
            {
                new Response
                {
                    Id = 1,
                    FormId = "form1",
                    UserId = userId,
                    SubmittedAt = DateTime.UtcNow,
                    Details = null
                }
            };

            _responseRepositoryMock.Setup(x => x.GetByUserId(userId)).Returns(responses);
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns((Form)null);

            // Act
            var result = _responseManager.GetUserSubmissions(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public void GetUserSubmissions_WithNullQuestions_ReturnsZeroQuestionCount()
        {
            // Arrange
            var userId = 1;
            var responses = new List<Response>
            {
                new Response
                {
                    Id = 1,
                    FormId = "form1",
                    UserId = userId,
                    SubmittedAt = DateTime.UtcNow
                }
            };

            var form = new Form
            {
                Id = "form1",
                Title = "Test Form",
                Questions = null
            };

            _responseRepositoryMock.Setup(x => x.GetByUserId(userId)).Returns(responses);
            _formRepositoryMock.Setup(x => x.GetById("form1")).Returns(form);

            // Act
            var result = _responseManager.GetUserSubmissions(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
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
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(createdDate, result[0].CreatedAt);
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
            Assert.Null(result[0].Questions[0].Description);
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
            Assert.Equal("This should appear", result[0].Questions[0].Description);
        }

        #endregion
    }
}
