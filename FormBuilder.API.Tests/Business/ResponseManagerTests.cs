using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using FormBuilder.API.Business.Implementations;
using FormBuilder.API.Business.Interfaces;
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

        #region GetPublishedForms Tests

        [Fact]
        public void GetPublishedForms_ReturnsPublishedForms()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Title = "Test Form",
                    Description = "Test Description",
                    Status = FormStatus.Published,
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            QuestionId = "q1",
                            QuestionText = "Question 1",
                            Type = "text",
                            Required = true,
                            DescriptionEnabled = true,
                            Description = "Question Description",
                            SingleChoice = false,
                            MultipleChoice = false,
                            Format = "text",
                            Order = 1,
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
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("form1", result[0].FormId);
            Assert.Equal("Test Form", result[0].Title);
            Assert.Single(result[0].Questions);
        }

        [Fact]
        public void GetPublishedForms_WithOptionsAndNoDescription_ReturnsCorrectMapping()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form2",
                    Title = "Form with Options",
                    Description = "Test",
                    Status = FormStatus.Published,
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            QuestionId = "q1",
                            QuestionText = "Multiple Choice",
                            Type = "checkbox",
                            Required = false,
                            DescriptionEnabled = false,
                            Description = "Should not show",
                            SingleChoice = false,
                            MultipleChoice = true,
                            Order = 1,
                            Options = new List<Option>
                            {
                                new Option { OptionId = "opt1", Value = "Option 1" },
                                new Option { OptionId = "opt2", Value = "Option 2" }
                            }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published))
                .Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.NotNull(result);
            var question = result[0].Questions[0];
            Assert.Null(question.Description); // DescriptionEnabled is false
            Assert.Equal(2, question.Options.Length);
            Assert.Contains("Option 1", question.Options);
        }

        #endregion

        #region GetResponsesByForm Tests

        [Fact]
        public void GetResponsesByForm_ReturnsResponses()
        {
            // Arrange
            var formId = "form1";
            var responses = new List<Response>
            {
                new Response { Id = 1, FormId = formId },
                new Response { Id = 2, FormId = formId }
            };

            _responseRepositoryMock.Setup(x => x.GetByFormId(formId))
                .Returns(responses);

            // Act
            var result = _responseManager.GetResponsesByForm(formId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        #endregion

        #region GetResponseById Tests

        [Fact]
        public void GetResponseById_ResponseExists_ReturnsSuccess()
        {
            // Arrange
            var responseId = "1";
            var response = new Response { Id = 1, FormId = "form1" };

            _responseRepositoryMock.Setup(x => x.GetById(responseId))
                .Returns(response);

            // Act
            var result = _responseManager.GetResponseById(responseId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Response retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetResponseById_ResponseNotFound_ReturnsFailure()
        {
            // Arrange
            var responseId = "999";

            _responseRepositoryMock.Setup(x => x.GetById(responseId))
                .Returns((Response)null);

            // Act
            var result = _responseManager.GetResponseById(responseId);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Response not found", result.Message);
            Assert.Null(result.Data);
        }

        #endregion

        #region SubmitResponse Tests

        [Fact]
        public void SubmitResponse_InvalidUserId_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "form1" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid user ID.", result.Message);
        }

        [Fact]
        public void SubmitResponse_NonNumericUserId_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "form1" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "abc")
            }));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid user ID.", result.Message);
        }

        [Fact]
        public void SubmitResponse_ZeroUserId_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "form1" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "0")
            }));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid user ID.", result.Message);
        }

        [Fact]
        public void SubmitResponse_UsesNameIdClaim_WhenNameIdentifierMissing()
        {
            // Arrange
            var dto = new FormSubmissionDto { FormId = "" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("nameId", "1")
            }));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form ID is required.", result.Message);
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
            var dto = new FormSubmissionDto { FormId = "form1" };
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1")
            }));

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns((Form)null);

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
                Status = FormStatus.Draft,
                Questions = new List<Question>()
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot submit to an unpublished form.", result.Message);
        }

        [Fact]
        public void SubmitResponse_FileTooLarge_ReturnsFailure()
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
                        FileName = "large.pdf",
                        FileType = "application/pdf",
                        FileSize = 6 * 1024 * 1024 // 6MB
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

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("exceeds maximum size", result.Message);
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
                        QuestionId = "q1",
                        FileName = "file.exe",
                        FileType = "application/x-executable",
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

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not allowed", result.Message);
        }

        [Fact]
        public void SubmitResponse_RequiredFileQuestionMissing_ReturnsFailure()
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
                        QuestionText = "Upload Document",
                        Type = "file",
                        Required = true
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("File upload for 'Upload Document' is required", result.Message);
        }

        [Fact]
        public void SubmitResponse_RequiredQuestionMissing_ReturnsFailure()
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
                        QuestionText = "Name",
                        Type = "text",
                        Required = true
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Question 'Name' is required", result.Message);
        }

        [Fact]
        public void SubmitResponse_RequiredQuestionWithWhitespace_ReturnsFailure()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "   " }
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
                        QuestionText = "Name",
                        Type = "text",
                        Required = true
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Question 'Name' is required", result.Message);
        }

        [Fact]
        public void SubmitResponse_CheckboxWithMultipleValues_FormatsCorrectly()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Option 1, Option 2" }
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
                        QuestionText = "Select Options",
                        Type = "checkbox",
                        Required = false,
                        MultipleChoice = true,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "opt1", Value = "Option 1" },
                            new Option { OptionId = "opt2", Value = "Option 2" },
                            new Option { OptionId = "opt3", Value = "Option 3" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal("[\"opt1\",\"opt2\"]", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_RadioButton_FormatsCorrectly()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Option 2" }
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
                        Required = false,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "opt1", Value = "Option 1" },
                            new Option { OptionId = "opt2", Value = "Option 2" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal("[\"opt2\"]", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_Dropdown_FormatsCorrectly()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Value A" }
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
                        Type = "dropdown",
                        Required = false,
                        Options = new List<Option>
                        {
                            new Option { OptionId = "optA", Value = "Value A" },
                            new Option { OptionId = "optB", Value = "Value B" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal("[\"optA\"]", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_WithFileUploads_Success()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>(),
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "test.pdf",
                        FileType = "application/pdf",
                        FileSize = 1024,
                        Base64Content = "base64string"
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

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            _fileAttachmentRepositoryMock.Setup(x => x.AddRange(It.IsAny<List<FileAttachment>>()));
            _fileAttachmentRepositoryMock.Setup(x => x.SaveChanges());

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Response submitted successfully", result.Message);
            _fileAttachmentRepositoryMock.Verify(x => x.AddRange(It.IsAny<List<FileAttachment>>()), Times.Once);
            _fileAttachmentRepositoryMock.Verify(x => x.SaveChanges(), Times.Once);
        }

        [Fact]
        public void SubmitResponse_MultipleFilesForSameQuestion_GroupsCorrectly()
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
                        FileName = "file1.pdf",
                        FileType = "application/pdf",
                        FileSize = 1024,
                        Base64Content = "base64"
                    },
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "file2.pdf",
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

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var fileDetail = result.Data.Details.FirstOrDefault(d => d.QuestionId == "q1");
            Assert.NotNull(fileDetail);
            Assert.Contains("file1.pdf", fileDetail.Answer);
            Assert.Contains("file2.pdf", fileDetail.Answer);
        }

        [Fact]
        public void SubmitResponse_TransactionFailure_RollsBack()
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

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            _fileAttachmentRepositoryMock.Setup(x => x.SaveChanges())
                .Throws(new Exception("Database error"));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error:", result.Message);
            _transactionMock.Verify(x => x.Rollback(), Times.Once);
        }

        [Fact]
        public void SubmitResponse_NoAnswersNoFiles_CreatesEmptyDetails()
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
                Questions = new List<Question>()
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data.Details);
        }

      
        [Fact]
        public void SubmitResponse_RadioWithUnmatchedOption_KeepsOriginalAnswer()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Unknown Option" }
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
                            new Option { OptionId = "opt1", Value = "Option 1" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal("Unknown Option", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_CheckboxWithSomeMatchedOptions_FormatsMatchedOnly()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Option 1, Invalid, Option 2" }
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
                            new Option { OptionId = "opt1", Value = "Option 1" },
                            new Option { OptionId = "opt2", Value = "Option 2" }
                        }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal("[\"opt1\",\"opt2\"]", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_QuestionWithNullOptions_HandlesGracefully()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Text Answer" }
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
                        Type = "text",
                        Options = null
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal("Text Answer", detail.Answer);
        }

        [Fact]
        public void SubmitResponse_AnswerWithNullValue_HandlesAsEmpty()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = null }
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
                        Type = "text",
                        Required = false
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
            var detail = result.Data.Details.First();
            Assert.Equal(string.Empty, detail.Answer);
        }

        [Fact]
        public void SubmitResponse_TransactionFailureWithResponseId_AttemptsDelete()
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

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            _responseRepositoryMock.Setup(x => x.Add(It.IsAny<Response>()))
                .Callback<Response>(r => r.Id = 123);

            _fileAttachmentRepositoryMock.Setup(x => x.SaveChanges())
                .Throws(new Exception("Database error"));

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            _responseRepositoryMock.Verify(x => x.Delete("123"), Times.Once);
        }

        [Fact]
        public void SubmitResponse_FileUploadWithFileuploadType_HandlesCorrectly()
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
                        QuestionText = "Upload Document",
                        Type = "fileupload", // lowercase fileupload type
                        Required = true
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("File upload for 'Upload Document' is required", result.Message);
        }

       
        [Fact]
        public void SubmitResponse_WithAllowedFileTypes_Success()
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
                        FileName = "doc.docx",
                        FileType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        FileSize = 1024
                    },
                    new FileUploadDto
                    {
                        QuestionId = "q2",
                        FileName = "image.jpg",
                        FileType = "image/jpeg",
                        FileSize = 2048
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

            _formRepositoryMock.Setup(x => x.GetById("form1"))
                .Returns(form);

            // Act
            var result = _responseManager.SubmitResponse(dto, claims);

            // Assert
            Assert.True(result.Success);
        }

        #endregion

        #region GetFileAttachment Tests

        [Fact]
        public void GetFileAttachment_FileExists_ReturnsSuccess()
        {
            // Arrange
            var responseId = 1;
            var questionId = "q1";
            var fileAttachment = new FileAttachment
            {
                Id = 1,
                ResponseId = responseId,
                QuestionId = questionId,
                FileName = "test.pdf"
            };

            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseAndQuestion(responseId, questionId))
                .Returns(fileAttachment);

            // Act
            var result = _responseManager.GetFileAttachment(responseId, questionId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("File retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal("test.pdf", result.Data.FileName);
        }

        [Fact]
        public void GetFileAttachment_FileNotFound_ReturnsFailure()
        {
            // Arrange
            var responseId = 999;
            var questionId = "q1";

            _fileAttachmentRepositoryMock.Setup(x => x.GetByResponseAndQuestion(responseId, questionId))
                .Returns((FileAttachment)null);

            // Act
            var result = _responseManager.GetFileAttachment(responseId, questionId);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("File not found", result.Message);
            Assert.Null(result.Data);
        }

        #endregion

        #region GetResponseWithFiles Tests

        [Fact]
        public void GetResponseWithFiles_ResponseNotFound_ReturnsFailure()
        {
            // Arrange
            var responseId = 999;

            _responseRepositoryMock.Setup(x => x.GetById("999"))
                .Returns((Response)null);

            // Act
            var result = _responseManager.GetResponseWithFiles(responseId);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Response not found", result.Message);
            Assert.Null(result.Data);
        }

       
        #endregion

        #region GetPublishedForms Edge Cases

        [Fact]
        public void GetPublishedForms_NoPublishedForms_ReturnsEmptyList()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published))
                .Returns(new List<Form>());

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetPublishedForms_WithEmptyQuestions_HandlesCorrectly()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Title = "Test Form",
                    Description = "Test Description",
                    Status = FormStatus.Published,
                    Questions = new List<Question>()
                }
            };

            _formRepositoryMock.Setup(x => x.GetByStatus(FormStatus.Published))
                .Returns(forms);

            // Act
            var result = _responseManager.GetPublishedForms();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Empty(result[0].Questions);
        }

        #endregion
    }
}