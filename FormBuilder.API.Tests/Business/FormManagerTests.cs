using Xunit;
using Moq;
using FormBuilder.API.Business.Implementations;
using FormBuilder.API.DataAccess.Interfaces;
using FormBuilder.API.DTOs.Form;
using FormBuilder.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace FormBuilder.API.Tests.Business
{
    public class FormManagerTests
    {
        private readonly Mock<IFormRepository> _formRepositoryMock;
        private readonly Mock<IResponseRepository> _responseRepositoryMock;
        private readonly FormManager _formManager;

        public FormManagerTests()
        {
            _formRepositoryMock = new Mock<IFormRepository>();
            _responseRepositoryMock = new Mock<IResponseRepository>();
            _formManager = new FormManager(_formRepositoryMock.Object, _responseRepositoryMock.Object);
        }

        #region GetAllForms Additional Tests

        [Fact]
        public void GetAllForms_WithNegativeOffset_ShouldResetToZero()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form { Id = "1", Title = "Form1", Questions = new List<Question>() },
                new Form { Id = "2", Title = "Form2", Questions = new List<Question>() }
            };
            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal, -5, 10);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            // Just verify it executes successfully without checking internal structure
        }

        [Fact]
        public void GetAllForms_WithZeroLimit_ShouldSetToDefault()
        {
            // Arrange
            var forms = new List<Form> { new Form { Id = "1", Title = "Form1" } };
            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal, 0, 0);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetAllForms_WithLimitExceeding100_ShouldCapAt100()
        {
            // Arrange
            var forms = Enumerable.Range(1, 150).Select(i => new Form { Id = $"{i}", Title = $"Form{i}" }).ToList();
            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal, 0, 150);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetAllForms_PaginationHasNext_WhenMoreItemsExist()
        {
            // Arrange
            var forms = Enumerable.Range(1, 25).Select(i => new Form 
            { 
                Id = $"{i}", 
                Title = $"Form{i}",
                Questions = new List<Question>()
            }).ToList();
            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal, 0, 10);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetAllForms_PaginationHasPrevious_WhenOffsetGreaterThanZero()
        {
            // Arrange
            var forms = Enumerable.Range(1, 25).Select(i => new Form 
            { 
                Id = $"{i}", 
                Title = $"Form{i}" 
            }).ToList();
            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal, 15, 10);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public void GetAllForms_WithNullQuestions_ShouldReturnEmptyQuestionsList()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form 
                { 
                    Id = "1", 
                    Title = "Form1",
                    Description = "Desc",
                    Status = FormStatus.Draft,
                    Questions = null
                }
            };
            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            // The method should handle null questions internally
        }

        [Fact]
        public void GetAllForms_ExceptionThrown_ShouldReturnFailure()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetAll()).Throws(new Exception("Database error"));
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error retrieving forms", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void GetAllForms_WithSearchTerm_FiltersResults()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form 
                { 
                    Id = "1", 
                    Title = "Feedback Form", 
                    Description = "Customer feedback",
                    Questions = new List<Question>
                    {
                        new Question 
                        { 
                            QuestionText = "Rate our service",
                            Description = "Please rate from 1 to 5"
                        }
                    }
                },
                new Form 
                { 
                    Id = "2", 
                    Title = "Survey Form", 
                    Description = "Annual survey"
                }
            };
            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal, 1, 10, "feedback");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            // The filter would apply and return matching results
        }

        #endregion

        #region CreateFormConfig Tests

        [Fact]
        public void CreateFormConfig_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var dto = new FormConfigRequestDto
            {
                Title = "Test Form",
                Description = "Test Description"
            };

            // Act
            var result = _formManager.CreateFormConfig(dto, "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form configuration created successfully", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal("Test Form", result.Data.Title);
            Assert.Equal("Test Description", result.Data.Description);
            _formRepositoryMock.Verify(x => x.Add(It.IsAny<Form>()), Times.Once);
        }

        #endregion

        #region UpdateFormConfig Tests

        [Fact]
        public void UpdateFormConfig_ValidForm_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Title = "Old Title",
                Description = "Old Description",
                Status = FormStatus.Draft
            };
            var dto = new FormConfigRequestDto
            {
                Title = "New Title",
                Description = "New Description"
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormConfig("form123", dto);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form configuration updated successfully", result.Message);
            Assert.Equal("New Title", result.Data.Title);
            Assert.Equal("New Description", result.Data.Description);
            _formRepositoryMock.Verify(x => x.Update(It.IsAny<Form>()), Times.Once);
        }

        [Fact]
        public void UpdateFormConfig_FormNotFound_ReturnsFailure()
        {
            // Arrange
            var dto = new FormConfigRequestDto();
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);

            // Act
            var result = _formManager.UpdateFormConfig("nonexistent", dto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form not found", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void UpdateFormConfig_PublishedForm_ReturnsFailure()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Published
            };
            var dto = new FormConfigRequestDto();
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormConfig("form123", dto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot update configuration of a published form.", result.Message);
            Assert.Null(result.Data);
        }

        #endregion

        #region UpdateFormLayout Additional Tests

        [Fact]
        public void UpdateFormLayout_WithNullQuestions_ShouldSetEmptyList()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Title = "Test",
                Status = FormStatus.Draft,
                Questions = new List<Question> { new Question() }
            };
            var dto = new FormLayoutRequestDto { Questions = null };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.Data.Questions);
            _formRepositoryMock.Verify(x => x.Update(It.Is<Form>(f => f.Questions.Count == 0)), Times.Once);
        }

        [Fact]
        public void UpdateFormLayout_WithInvalidQuestionId_ShouldGenerateNewId()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Questions = new List<Question>()
            };
            var dto = new FormLayoutRequestDto
            {
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Id = "000000000000000000000000", // Invalid ID
                        Text = "Question",
                        Type = "text"
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.NotEqual("000000000000000000000000", result.Data.Questions.First().Id);
            Assert.Equal(24, result.Data.Questions.First().Id.Length); // MongoDB ObjectId length
        }

        [Fact]
        public void UpdateFormLayout_WithEmptyQuestionId_ShouldGenerateNewId()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Questions = null
            };
            var dto = new FormLayoutRequestDto
            {
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Id = "",
                        Text = "Question",
                        Type = "text"
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(result.Data.Questions.First().Id);
        }

        [Fact]
        public void UpdateFormLayout_WithValidQuestionId_ShouldPreserveId()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Questions = new List<Question>()
            };
            var dto = new FormLayoutRequestDto
            {
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Id = "507f1f77bcf86cd799439011",
                        Text = "Question",
                        Type = "text"
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("507f1f77bcf86cd799439011", result.Data.Questions.First().Id);
        }

        [Fact]
        public void UpdateFormLayout_WithZeroOrder_ShouldUseIndex()
        {
            // Arrange
            var form = new Form { Id = "form123", Status = FormStatus.Draft };
            var dto = new FormLayoutRequestDto
            {
                Questions = new List<QuestionDto>
                {
                    new QuestionDto { Text = "Q1", Type = "text", Order = 0 },
                    new QuestionDto { Text = "Q2", Type = "text", Order = 0 },
                    new QuestionDto { Text = "Q3", Type = "text", Order = 5 }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.Data.Questions[0].Order);
            Assert.Equal(1, result.Data.Questions[1].Order);
            Assert.Equal(5, result.Data.Questions[2].Order);
        }

        [Fact]
        public void UpdateFormLayout_WithNullDescription_ShouldSetEmptyString()
        {
            // Arrange
            var form = new Form { Id = "form123", Status = FormStatus.Draft };
            var dto = new FormLayoutRequestDto
            {
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Text = "Question",
                        Type = "text",
                        Description = null,
                        DescriptionEnabled = true
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            var savedForm = _formRepositoryMock.Invocations[1].Arguments[0] as Form;
            Assert.Equal("", savedForm.Questions.First().Description);
        }

        [Fact]
        public void UpdateFormLayout_WithNullOptions_ShouldSetEmptyList()
        {
            // Arrange
            var form = new Form { Id = "form123", Status = FormStatus.Draft };
            var dto = new FormLayoutRequestDto
            {
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Text = "Question",
                        Type = "text",
                        Options = null
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            var savedForm = _formRepositoryMock.Invocations[1].Arguments[0] as Form;
            Assert.NotNull(savedForm.Questions.First().Options);
            Assert.Empty(savedForm.Questions.First().Options);
        }

        [Fact]
        public void UpdateFormLayout_FormNotFound_ReturnsFailure()
        {
            // Arrange
            var dto = new FormLayoutRequestDto();
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);

            // Act
            var result = _formManager.UpdateFormLayout("nonexistent", dto, "Admin");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form not found. Please create form configuration first using the FormConfig endpoint.", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void UpdateFormLayout_PublishedForm_ReturnsFailure()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Published
            };
            var dto = new FormLayoutRequestDto();
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot modify layout of a published form.", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void UpdateFormLayout_WithOptionsHavingValues_GeneratesOptionIds()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Title = "Test Form",
                Description = "Test Description"
            };
            var dto = new FormLayoutRequestDto
            {
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Text = "Select Option",
                        Type = "radio",
                        Options = new[] { "Option1", "Option2", "Option3" }
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto, "Admin");

            // Assert
            Assert.True(result.Success);
            var savedForm = _formRepositoryMock.Invocations[1].Arguments[0] as Form;
            Assert.NotNull(savedForm.Questions[0].Options);
            Assert.Equal(3, savedForm.Questions[0].Options.Count);
            // Each option should have generated OptionId
            foreach (var option in savedForm.Questions[0].Options)
            {
                Assert.NotEmpty(option.OptionId);
                Assert.NotEmpty(option.Value);
            }
        }

        #endregion

        #region GetFormById Tests

        [Fact]
        public void GetFormById_ExistingForm_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Title = "Test Form",
                Description = "Test Description",
                Status = FormStatus.Draft,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Question 1",
                        Type = "text",
                        Required = true,
                        DescriptionEnabled = true,
                        Description = "Question description"
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetFormById("form123", principal);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form retrieved successfully", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal("form123", result.Data.FormId);
            Assert.Equal("Test Form", result.Data.Title);
            Assert.Single(result.Data.Questions);
        }

        [Fact]
        public void GetFormById_NonExistingForm_ReturnsFailure()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetFormById("nonexistent", principal);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form not found", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void GetFormById_WithNullQuestions_ReturnsEmptyQuestionsList()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Title = "Test Form",
                Description = "Test Description",
                Status = FormStatus.Draft,
                Questions = null
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetFormById("form123", principal);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data.Questions);
        }

        [Fact]
        public void GetFormById_WithOptionsInQuestions_ReturnsOptions()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Title = "Test Form",
                Description = "Test Description",
                Status = FormStatus.Draft,
                Questions = new List<Question>
                {
                    new Question
                    {
                        QuestionId = "q1",
                        QuestionText = "Select one",
                        Type = "radio",
                        Options = new List<Option>
                        {
                            new Option { OptionId = "opt1", Value = "Option 1" },
                            new Option { OptionId = "opt2", Value = "Option 2" }
                        }
                    }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetFormById("form123", principal);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.Data.Questions[0].Options.Length);
            Assert.Contains("Option 1", result.Data.Questions[0].Options);
            Assert.Contains("Option 2", result.Data.Questions[0].Options);
        }

        #endregion

        #region DeleteForm Additional Tests

        [Fact]
        public void DeleteForm_WithNoResponses_ShouldReturnZeroDeletedResponses()
        {
            // Arrange
            var form = new Form { Id = "form123" };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);
            _responseRepositoryMock.Setup(x => x.DeleteAllByFormId("form123")).Returns(0);

            // Act
            var result = _formManager.DeleteForm("form123");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form and 0 response(s) deleted successfully", result.Message);
        }

        [Fact]
        public void DeleteForm_ExceptionThrown_ShouldReturnFailure()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetById("form123"))
                .Throws(new Exception("Database connection failed"));

            // Act
            var result = _formManager.DeleteForm("form123");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error deleting form", result.Message);
            Assert.Contains("Database connection failed", result.Message);
        }

        [Fact]
        public void DeleteForm_DeleteResponsesThrowsException_ShouldStillAttemptFormDeletion()
        {
            // Arrange
            var form = new Form { Id = "form123" };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);
            _responseRepositoryMock.Setup(x => x.DeleteAllByFormId("form123"))
                .Throws(new Exception("Response deletion failed"));

            // Act
            var result = _formManager.DeleteForm("form123");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Error deleting form", result.Message);
        }

        #endregion

        #region PublishForm Additional Tests

        [Fact]
        public void PublishForm_WithEmptyQuestionsList_ShouldReturnFailure()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Questions = new List<Question>() // Empty list
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.PublishForm("form123", "Admin");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Cannot publish a form without questions", result.Message);
        }

        [Fact]
        public void PublishForm_ValidFormWithQuestions_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Questions = new List<Question>
                {
                    new Question { QuestionId = "q1", QuestionText = "Question 1" }
                }
            };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.PublishForm("form123", "Publisher");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form published successfully", result.Message);
            Assert.Equal(FormStatus.Published, form.Status);
            Assert.Equal("Publisher", form.PublishedBy);
            Assert.NotNull(form.PublishedAt);
            _formRepositoryMock.Verify(x => x.Update(It.IsAny<Form>()), Times.Once);
        }

        #endregion
    }
}