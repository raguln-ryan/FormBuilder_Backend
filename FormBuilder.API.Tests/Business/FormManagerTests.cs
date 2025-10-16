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
            _formRepositoryMock.Verify(x => x.Add(It.Is<Form>(f => 
                f.Title == "Test Form" && 
                f.Description == "Test Description" && 
                f.Status == FormStatus.Draft &&
                f.CreatedBy == "Admin"
            )), Times.Once);
        }

        [Fact]
        public void UpdateFormConfig_ExistingDraftForm_ReturnsSuccess()
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
                Title = "Updated Title",
                Description = "Updated Description"
            };

            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormConfig("form123", dto);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form configuration updated successfully", result.Message);
            Assert.Equal("Updated Title", result.Data.Title);
            Assert.Equal("Updated Description", result.Data.Description);
            _formRepositoryMock.Verify(x => x.Update(It.Is<Form>(f => 
                f.Title == "Updated Title" && 
                f.Description == "Updated Description"
            )), Times.Once);
        }

        [Fact]
        public void UpdateFormConfig_PublishedForm_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Published
            };

            var dto = new FormConfigRequestDto
            {
                Title = "Updated Title",
                Description = "Updated Description"
            };

            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormConfig("form123", dto);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot update configuration of a published form.", result.Message);
            Assert.Null(result.Data);
            _formRepositoryMock.Verify(x => x.Update(It.IsAny<Form>()), Times.Never);
        }

        [Fact]
        public void UpdateFormConfig_NonExistentForm_ReturnsFalse()
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
        public void CreateFormLayout_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Title = "Test Form",
                Description = "Test Description",
                Status = FormStatus.Draft
            };

            var dto = new FormLayoutRequestDto
            {
                FormId = "form123",
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Text = "Question 1",
                        Type = "text",
                        Required = true,
                        Order = 1,
                        Options = new[] { "Option1", "Option2" }
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.CreateFormLayout(dto, "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form layout created successfully", result.Message);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data.Questions);
            Assert.Equal("Question 1", result.Data.Questions[0].Text);
            _formRepositoryMock.Verify(x => x.Update(It.Is<Form>(f => 
                f.Questions.Count == 1 &&
                f.UpdatedBy == "Admin"
            )), Times.Once);
        }

        [Fact]
        public void CreateFormLayout_PublishedForm_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Published
            };

            var dto = new FormLayoutRequestDto { FormId = "form123" };
            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.CreateFormLayout(dto, "Admin");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot modify layout of a published form.", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void CreateFormLayout_NonExistentForm_ReturnsFalse()
        {
            // Arrange
            var dto = new FormLayoutRequestDto { FormId = "nonexistent" };
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);

            // Act
            var result = _formManager.CreateFormLayout(dto, "Admin");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form not found", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void UpdateFormLayout_DraftForm_ReturnsSuccess()
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
                FormId = "form123",
                Questions = new List<QuestionDto>
                {
                    new QuestionDto
                    {
                        Id = "q1",
                        Text = "Updated Question",
                        Type = "radio",
                        Options = new[] { "Yes", "No" },
                        Required = false,
                        Order = 1
                    }
                }
            };

            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.UpdateFormLayout("form123", dto);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form layout updated successfully", result.Message);
            Assert.NotNull(result.Data);
            _formRepositoryMock.Verify(x => x.Update(It.IsAny<Form>()), Times.Once);
        }

        [Fact]
        public void DeleteForm_WithResponses_DeletesAllAndReturnsSuccess()
        {
            // Arrange
            var form = new Form { Id = "form123" };
            var responses = new List<Response>
            {
                new Response { Id = 1 },
                new Response { Id = 2 }
            };

            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);
            _responseRepositoryMock.Setup(x => x.GetByFormId("form123")).Returns(responses);
            _responseRepositoryMock.Setup(x => x.Delete(It.IsAny<string>()));

            // Act
            var result = _formManager.DeleteForm("form123");

            // Assert
            Assert.True(result.Success);
            Assert.Contains("2 associated response(s) deleted successfully", result.Message);
            _responseRepositoryMock.Verify(x => x.Delete("1"), Times.Once);
            _responseRepositoryMock.Verify(x => x.Delete("2"), Times.Once);
            _formRepositoryMock.Verify(x => x.Delete("form123"), Times.Once);
        }

        [Fact]
        public void DeleteForm_NonExistentForm_ReturnsFalse()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);

            // Act
            var result = _formManager.DeleteForm("nonexistent");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form not found", result.Message);
            _formRepositoryMock.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void GetAllForms_ReturnsAllFormsAsDto()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form
                {
                    Id = "form1",
                    Title = "Form 1",
                    Description = "Description 1",
                    Status = FormStatus.Draft,
                    Questions = new List<Question>
                    {
                        new Question { QuestionId = "q1", QuestionText = "Q1", Type = "text" }
                    }
                },
                new Form
                {
                    Id = "form2",
                    Title = "Form 2",
                    Description = "Description 2",
                    Status = FormStatus.Published,
                    Questions = new List<Question>()
                }
            };

            _formRepositoryMock.Setup(x => x.GetAll()).Returns(forms);
            var principal = new ClaimsPrincipal();

            // Act
            var result = _formManager.GetAllForms(principal);

            // Assert
            var dtoList = Assert.IsType<List<FormLayoutResponseDto>>(result);
            Assert.Equal(2, dtoList.Count);
            Assert.Equal("Form 1", dtoList[0].Title);
            Assert.Equal("Form 2", dtoList[1].Title);
        }

        [Fact]
        public void GetFormById_ExistingForm_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Title = "Test Form",
                Description = "Description",
                Status = FormStatus.Draft,
                Questions = new List<Question>
                {
                    new Question 
                    { 
                        QuestionId = "q1", 
                        QuestionText = "Question",
                        Type = "text",
                        DescriptionEnabled = true,
                        Description = "Question Description",
                        Options = new List<Option>()
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
            Assert.Equal("Test Form", result.Data.Title);
            Assert.Single(result.Data.Questions);
        }

        [Fact]
        public void GetFormById_NonExistentForm_ReturnsFalse()
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
        public void PublishForm_ValidFormWithQuestions_ReturnsSuccess()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Questions = new List<Question> { new Question() }
            };

            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.PublishForm("form123", "Admin");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Form published successfully", result.Message);
            _formRepositoryMock.Verify(x => x.Update(It.Is<Form>(f => 
                f.Status == FormStatus.Published &&
                f.PublishedBy == "Admin" &&
                f.PublishedAt != null
            )), Times.Once);
        }

        [Fact]
        public void PublishForm_FormWithoutQuestions_ReturnsFalse()
        {
            // Arrange
            var form = new Form
            {
                Id = "form123",
                Status = FormStatus.Draft,
                Questions = null
            };

            _formRepositoryMock.Setup(x => x.GetById("form123")).Returns(form);

            // Act
            var result = _formManager.PublishForm("form123", "Admin");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Cannot publish a form without questions", result.Message);
            _formRepositoryMock.Verify(x => x.Update(It.IsAny<Form>()), Times.Never);
        }

        [Fact]
        public void PublishForm_NonExistentForm_ReturnsFalse()
        {
            // Arrange
            _formRepositoryMock.Setup(x => x.GetById("nonexistent")).Returns((Form)null);

            // Act
            var result = _formManager.PublishForm("nonexistent", "Admin");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Form not found", result.Message);
        }
    }
}