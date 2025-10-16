using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FormBuilder.API.Configurations;
using FormBuilder.API.DataAccess.Implementations;
using FormBuilder.API.DataAccess.Interfaces;
using FormBuilder.API.Models;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace FormBuilder.API.Tests.DataAccess
{
    public class FormRepositoryTests
    {
        private readonly Mock<IMongoCollection<Form>> _mockCollection;
        private readonly Mock<MongoDbContext> _mockContext;
        private readonly FormRepository _repository;

        public FormRepositoryTests()
        {
            _mockCollection = new Mock<IMongoCollection<Form>>();
            _mockContext = new Mock<MongoDbContext>();
            _mockContext.Setup(x => x.Forms).Returns(_mockCollection.Object);
            _repository = new FormRepository(_mockContext.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializeFormsCollection()
        {
            // Assert
            _mockContext.Verify(x => x.Forms, Times.Once);
        }

        [Fact]
        public void Add_ShouldInsertForm()
        {
            // Arrange
            var form = new Form { Id = "1", Title = "Test Form" };

            // Act
            _repository.Add(form);

            // Assert
            _mockCollection.Verify(x => x.InsertOne(
                It.Is<Form>(f => f.Id == "1" && f.Title == "Test Form"),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetById_ShouldReturnForm_WhenFormExists()
        {
            // Arrange
            var formId = "1";
            var expectedForm = new Form { Id = formId, Title = "Test Form" };
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(new List<Form> { expectedForm });
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            // Act
            var result = _repository.GetById(formId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(formId, result.Id);
            Assert.Equal("Test Form", result.Title);
        }

        [Fact]
        public void GetAll_ShouldReturnAllForms()
        {
            // Arrange
            var forms = new List<Form>
            {
                new Form { Id = "1", Title = "Form 1" },
                new Form { Id = "2", Title = "Form 2" }
            };
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(forms);
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            // Act
            var result = _repository.GetAll();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void GetByStatus_ShouldReturnFormsWithSpecificStatus()
        {
            // Arrange
            var status = FormStatus.Published;
            var forms = new List<Form>
            {
                new Form { Id = "1", Title = "Form 1", Status = FormStatus.Published }
            };
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(forms);
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            // Act
            var result = _repository.GetByStatus(status);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.All(result, f => Assert.Equal(FormStatus.Published, f.Status));
        }

        [Fact]
        public void Update_ShouldReplaceForm()
        {
            // Arrange
            var form = new Form { Id = "1", Title = "Updated Form" };
            var replaceResult = new ReplaceOneResult.Acknowledged(1, 1, null);
            
            _mockCollection.Setup(x => x.ReplaceOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.Is<Form>(f => f.Id == "1" && f.Title == "Updated Form"),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(replaceResult);

            // Act
            _repository.Update(form);

            // Assert
            _mockCollection.Verify(x => x.ReplaceOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.Is<Form>(f => f.Id == "1"),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Delete_ShouldDeleteForm_WhenFormExists()
        {
            // Arrange
            var formId = "1";
            var deleteResult = new DeleteResult.Acknowledged(1);
            
            _mockCollection.Setup(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(deleteResult);

            // Act
            _repository.Delete(formId);

            // Assert
            _mockCollection.Verify(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Delete_ShouldThrowException_WhenFormDoesNotExist()
        {
            // Arrange
            var formId = "nonexistent";
            var deleteResult = new DeleteResult.Acknowledged(0);
            
            _mockCollection.Setup(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(deleteResult);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _repository.Delete(formId));
            Assert.Contains($"Form with ID {formId} could not be deleted or does not exist", exception.Message);
        }

        [Fact]
        public void CreateConfig_ShouldCreateNewFormWithBasicConfiguration()
        {
            // Arrange
            var title = "Test Form";
            var description = "Test Description";
            var createdBy = "user@test.com";

            // Act
            var result = _repository.CreateConfig(title, description, createdBy);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(title, result.Title);
            Assert.Equal(description, result.Description);
            Assert.Equal(FormStatus.Draft, result.Status);
            Assert.Equal(createdBy, result.CreatedBy);
            Assert.NotNull(result.Questions);
            Assert.Empty(result.Questions);
            
            _mockCollection.Verify(x => x.InsertOne(
                It.Is<Form>(f => 
                    f.Title == title && 
                    f.Description == description && 
                    f.CreatedBy == createdBy),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void CreateLayout_ShouldCreateNewForm_WhenFormDoesNotExist()
        {
            // Arrange
            var formId = "new-form";
            var questions = new List<Question> 
            { 
                new Question { QuestionText = "Question 1" } 
            };
            var updatedBy = "user@test.com";

            // Setup GetById to return null
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(new List<Form>());
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            // Act
            var result = _repository.CreateLayout(formId, questions, updatedBy);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(formId, result.Id);
            Assert.Equal("Untitled Form", result.Title);
            Assert.Equal(questions, result.Questions);
            
            _mockCollection.Verify(x => x.InsertOne(
                It.Is<Form>(f => f.Id == formId && f.Questions.Count == 1),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void CreateLayout_ShouldUpdateExistingForm_WhenFormExists()
        {
            // Arrange
            var formId = "existing-form";
            var existingForm = new Form { Id = formId, Title = "Existing Form" };
            var questions = new List<Question> 
            { 
                new Question { QuestionText = "New Question" } 
            };
            var updatedBy = "user@test.com";

            // Setup GetById to return existing form
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(new List<Form> { existingForm });
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            var updateResult = new UpdateResult.Acknowledged(1, 1, null);
            _mockCollection.Setup(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(updateResult);

            // Act
            var result = _repository.CreateLayout(formId, questions, updatedBy);

            // Assert
            _mockCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void DeleteFormAndResponses_ShouldDeleteFormAndAllResponses()
        {
            // Arrange
            var formId = "form-to-delete";
            var form = new Form { Id = formId, Title = "Form to Delete" };
            var responses = new List<Response>
            {
                new Response { Id = 1, FormId = formId },
                new Response { Id = 2, FormId = formId }
            };

            var mockResponseRepo = new Mock<IResponseRepository>();
            mockResponseRepo.Setup(x => x.GetByFormId(formId)).Returns(responses);

            // Setup GetById to return form
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(new List<Form> { form });
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            var deleteResult = new DeleteResult.Acknowledged(1);
            _mockCollection.Setup(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(deleteResult);

            // Act
            _repository.DeleteFormAndResponses(formId, mockResponseRepo.Object);

            // Assert
            mockResponseRepo.Verify(x => x.Delete(It.IsAny<string>()), Times.Exactly(2));
            _mockCollection.Verify(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void DeleteFormAndResponses_ShouldThrowException_WhenFormNotFound()
        {
            // Arrange
            var formId = "nonexistent";
            var mockResponseRepo = new Mock<IResponseRepository>();

            // Setup GetById to return null
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(new List<Form>());
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                _repository.DeleteFormAndResponses(formId, mockResponseRepo.Object));
            Assert.Contains($"Form with ID {formId} not found", exception.Message);
        }

        [Fact]
        public void DeleteFormAndResponses_ShouldNotDeleteForm_WhenResponseDeletionFails()
        {
            // Arrange
            var formId = "form-with-responses";
            var form = new Form { Id = formId };
            var responses = new List<Response>
            {
                new Response { Id = 1, FormId = formId },
                new Response { Id = 2, FormId = formId }
            };

            var mockResponseRepo = new Mock<IResponseRepository>();
            mockResponseRepo.Setup(x => x.GetByFormId(formId)).Returns(responses);
            mockResponseRepo.SetupSequence(x => x.Delete(It.IsAny<string>()))
                .Pass() // First deletion succeeds
                .Throws(new Exception("Deletion failed")); // Second fails

            // Setup GetById to return form
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.Setup(_ => _.Current).Returns(new List<Form> { form });
            mockCursor.SetupSequence(_ => _.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(true)
                .Returns(false);

            _mockCollection.Setup(x => x.FindSync(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<FindOptions<Form, Form>>(),
                It.IsAny<CancellationToken>()))
                .Returns(mockCursor.Object);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => 
                _repository.DeleteFormAndResponses(formId, mockResponseRepo.Object));
            Assert.Contains("Only 1 of 2 responses could be deleted", exception.Message);
            
            // Verify form was not deleted
            _mockCollection.Verify(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void UpdateConfig_ShouldUpdateTitleAndDescription()
        {
            // Arrange
            var formId = "1";
            var title = "Updated Title";
            var description = "Updated Description";
            var updateResult = new UpdateResult.Acknowledged(1, 1, null);

            _mockCollection.Setup(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(updateResult);

            // Act
            _repository.UpdateConfig(formId, title, description);

            // Assert
            _mockCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void UpdateLayout_ShouldUpdateQuestions()
        {
            // Arrange
            var formId = "1";
            var questions = new List<Question>
            {
                new Question { QuestionText = "Question 1" },
                new Question { QuestionText = "Question 2" }
            };
            var updateResult = new UpdateResult.Acknowledged(1, 1, null);

            _mockCollection.Setup(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(updateResult);

            // Act
            _repository.UpdateLayout(formId, questions);

            // Assert
            _mockCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void PublishForm_ShouldUpdateStatusAndPublishInfo()
        {
            // Arrange
            var formId = "1";
            var publishedBy = "admin@test.com";
            var publishedAt = DateTime.UtcNow;
            var updateResult = new UpdateResult.Acknowledged(1, 1, null);

            _mockCollection.Setup(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(updateResult);

            // Act
            _repository.PublishForm(formId, publishedBy, publishedAt);

            // Assert
            _mockCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
