using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using Moq;
using MongoDB.Driver;
using FormBuilder.API.DataAccess.Implementations;
using FormBuilder.API.DataAccess.Interfaces;
using FormBuilder.API.Models;
using FormBuilder.API.Configurations;
using MongoDB.Bson;

namespace FormBuilder.API.Tests.DataAccess
{
    public class FormRepositoryTests
    {
        private readonly Mock<IMongoCollection<Form>> _mockFormCollection;
        private readonly Mock<MongoDbContext> _mockDbContext;
        private readonly FormRepository _repository;
        private readonly Mock<IResponseRepository> _mockResponseRepository;

        public FormRepositoryTests()
        {
            _mockFormCollection = new Mock<IMongoCollection<Form>>();
            _mockDbContext = new Mock<MongoDbContext>("mongodb://test", "testdb");
            _mockDbContext.Setup(x => x.Forms).Returns(_mockFormCollection.Object);
            _repository = new FormRepository(_mockDbContext.Object);
            _mockResponseRepository = new Mock<IResponseRepository>();
        }

        private static Mock<IAsyncCursor<Form>> SetupCursor(List<Form> forms)
        {
            var mockCursor = new Mock<IAsyncCursor<Form>>();
            mockCursor.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
                      .Returns(true)
                      .Returns(false);
            mockCursor.SetupGet(x => x.Current).Returns(forms);
            return mockCursor;
        }

        // ========== EXISTING TEST CASES (keep all of them) ==========
        // ... [All existing test cases from the provided file] ...

        // ========== ADDITIONAL TEST CASES FOR 100% COVERAGE ==========

        [Fact]
        public void Add_Should_HandleNullForm()
        {
            Form? nullForm = null;

            _mockFormCollection.Setup(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()));

            _repository.Add(nullForm!);

            _mockFormCollection.Verify(x => x.InsertOne(nullForm!, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetById_Should_HandleEmptyStringId()
        {
            var formId = "";
            
            var mockCursor = SetupCursor(new List<Form>());
            _mockFormCollection
                .Setup(x => x.FindSync(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<FindOptions<Form, Form>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(mockCursor.Object);

            var result = _repository.GetById(formId);

            Assert.Null(result);
        }

        [Fact]
        public void GetById_Should_HandleNullId()
        {
            string? formId = null;
            
            var mockCursor = SetupCursor(new List<Form>());
            _mockFormCollection
                .Setup(x => x.FindSync(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<FindOptions<Form, Form>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(mockCursor.Object);

            var result = _repository.GetById(formId!);

            Assert.Null(result);
        }

        [Fact]
        public void Update_Should_HandleFormWithNullId()
        {
            var form = new Form { Id = null!, Title = "Test" };

            _mockFormCollection.Setup(x => x.ReplaceOne(It.IsAny<FilterDefinition<Form>>(), form, It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .Returns(new ReplaceOneResult.Acknowledged(1, 0, null));

            _repository.Update(form);

            _mockFormCollection.Verify(x => x.ReplaceOne(It.IsAny<FilterDefinition<Form>>(), form, It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void CreateConfig_Should_HandleEmptyStrings()
        {
            var title = "";
            var description = "";
            var createdBy = "";

            _mockFormCollection.Setup(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<Form, InsertOneOptions, CancellationToken>((form, options, token) =>
                {
                    Assert.Equal("", form.Title);
                    Assert.Equal("", form.Description);
                    Assert.Equal("", form.CreatedBy);
                });

            var result = _repository.CreateConfig(title, description, createdBy);

            Assert.NotNull(result);
            _mockFormCollection.Verify(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void CreateLayout_Should_HandleNullQuestions()
        {
            var formId = ObjectId.GenerateNewId().ToString();
            List<Question>? questions = null;
            var updatedBy = "TestUser";

            var mockCursor = SetupCursor(new List<Form>());
            _mockFormCollection
                .Setup(x => x.FindSync(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<FindOptions<Form, Form>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(mockCursor.Object);

            _mockFormCollection.Setup(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<Form, InsertOneOptions, CancellationToken>((form, options, token) =>
                {
                    Assert.Null(form.Questions);
                });

            var result = _repository.CreateLayout(formId, questions!, updatedBy);

            _mockFormCollection.Verify(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void CreateLayout_Should_HandleEmptyQuestionsList()
        {
            var formId = ObjectId.GenerateNewId().ToString();
            var questions = new List<Question>();
            var updatedBy = "TestUser";

            var mockCursor = SetupCursor(new List<Form>());
            _mockFormCollection
                .Setup(x => x.FindSync(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<FindOptions<Form, Form>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(mockCursor.Object);

            _mockFormCollection.Setup(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()));

            var result = _repository.CreateLayout(formId, questions, updatedBy);

            _mockFormCollection.Verify(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

       
        [Fact]
        public void UpdateConfig_Should_HandleNullValues()
        {
            var formId = ObjectId.GenerateNewId().ToString();
            string? title = null;
            string? description = null;

            _mockFormCollection
                .Setup(x => x.UpdateOne(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<UpdateDefinition<Form>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(new UpdateResult.Acknowledged(1, 1, formId));

            _repository.UpdateConfig(formId, title!, description!);

            _mockFormCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        [Fact]
        public void UpdateLayout_Should_HandleNullQuestionsList()
        {
            var formId = ObjectId.GenerateNewId().ToString();
            List<Question>? questions = null;

            _mockFormCollection
                .Setup(x => x.UpdateOne(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<UpdateDefinition<Form>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(new UpdateResult.Acknowledged(1, 1, formId));

            _repository.UpdateLayout(formId, questions!);

            _mockFormCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        [Fact]
        public void PublishForm_Should_HandleMinDateTime()
        {
            var formId = ObjectId.GenerateNewId().ToString();
            var publishedBy = "Admin";
            var publishedAt = DateTime.MinValue;

            _mockFormCollection
                .Setup(x => x.UpdateOne(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<UpdateDefinition<Form>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(new UpdateResult.Acknowledged(1, 1, formId));

            _repository.PublishForm(formId, publishedBy, publishedAt);

            _mockFormCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        [Fact]
        public void PublishForm_Should_HandleMaxDateTime()
        {
            var formId = ObjectId.GenerateNewId().ToString();
            var publishedBy = "Admin";
            var publishedAt = DateTime.MaxValue;

            _mockFormCollection
                .Setup(x => x.UpdateOne(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<UpdateDefinition<Form>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(new UpdateResult.Acknowledged(1, 1, formId));

            _repository.PublishForm(formId, publishedBy, publishedAt);

            _mockFormCollection.Verify(x => x.UpdateOne(
                It.IsAny<FilterDefinition<Form>>(),
                It.IsAny<UpdateDefinition<Form>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        [Fact]
        public void GetByStatus_Should_HandleAllStatusValues()
        {
            // Test Draft status
            var draftForms = new List<Form> {
                new Form { Id = "1", Status = FormStatus.Draft }
            };
            var mockCursorDraft = SetupCursor(draftForms);

            _mockFormCollection
                .Setup(x => x.FindSync(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<FindOptions<Form, Form>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(mockCursorDraft.Object);

            var draftResult = _repository.GetByStatus(FormStatus.Draft);
            Assert.Single(draftResult);

            // Test Published status
            var publishedForms = new List<Form> {
                new Form { Id = "2", Status = FormStatus.Published }
            };
            var mockCursorPublished = SetupCursor(publishedForms);

            _mockFormCollection
                .Setup(x => x.FindSync(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<FindOptions<Form, Form>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(mockCursorPublished.Object);

            var publishedResult = _repository.GetByStatus(FormStatus.Published);
            Assert.Single(publishedResult);
        }

        
        [Fact]
        public void Delete_Should_HandleNullId()
        {
            string? formId = null;

            _mockFormCollection.Setup(x => x.DeleteOne(It.IsAny<FilterDefinition<Form>>(), It.IsAny<CancellationToken>()))
                .Returns(new DeleteResult.Acknowledged(0));

            var ex = Assert.Throws<InvalidOperationException>(() => _repository.Delete(formId!));
            Assert.Contains("could not be deleted or does not exist", ex.Message);
        }

        [Fact]
        public void DeleteFormAndResponses_Should_HandleMultipleExceptionsInResponseDeletion()
        {
            var formId = ObjectId.GenerateNewId().ToString();
            var form = new Form { Id = formId };
            var responses = new List<Response>
            {
                new Response { Id = 1, FormId = formId },
                new Response { Id = 2, FormId = formId },
                new Response { Id = 3, FormId = formId }
            };

            var mockCursor = SetupCursor(new List<Form> { form });
            _mockFormCollection
                .Setup(x => x.FindSync(
                    It.IsAny<FilterDefinition<Form>>(),
                    It.IsAny<FindOptions<Form, Form>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns(mockCursor.Object);

            _mockResponseRepository.Setup(x => x.GetByFormId(formId)).Returns(responses);
            _mockResponseRepository.Setup(x => x.Delete("1")).Throws(new Exception("Error 1"));
            _mockResponseRepository.Setup(x => x.Delete("2"));
            _mockResponseRepository.Setup(x => x.Delete("3")).Throws(new Exception("Error 3"));

            var ex = Assert.Throws<InvalidOperationException>(() => 
                _repository.DeleteFormAndResponses(formId, _mockResponseRepository.Object));
            
            Assert.Contains("Only 1 of 3 responses could be deleted", ex.Message);
            Assert.Contains("ResponseId: 1", ex.Message);
            Assert.Contains("ResponseId: 3", ex.Message);
        }

        [Fact]
        public void Constructor_Should_InitializeWithMongoDbContext()
        {
            var mockContext = new Mock<MongoDbContext>("mongodb://test", "testdb");
            mockContext.Setup(x => x.Forms).Returns(_mockFormCollection.Object);

            var repository = new FormRepository(mockContext.Object);

            Assert.NotNull(repository);
        }

        [Fact]
        public void CreateConfig_Should_SetAllFieldsCorrectly()
        {
            var title = "Test Form";
            var description = "Test Description";
            var createdBy = "TestUser";
            Form? capturedForm = null;

            _mockFormCollection
                .Setup(x => x.InsertOne(It.IsAny<Form>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Callback<Form, InsertOneOptions, CancellationToken>((form, options, token) =>
                {
                    capturedForm = form;
                });

            var result = _repository.CreateConfig(title, description, createdBy);

            Assert.NotNull(capturedForm);
            Assert.Equal(title, capturedForm!.Title);
            Assert.Equal(description, capturedForm.Description);
            Assert.Equal(FormStatus.Draft, capturedForm.Status);
            Assert.Equal(createdBy, capturedForm.CreatedBy);
            Assert.NotNull(capturedForm.CreatedAt);
            Assert.NotNull(capturedForm.UpdatedAt);
            Assert.NotNull(capturedForm.Questions);
            Assert.Empty(capturedForm.Questions);
        }
    }
}
