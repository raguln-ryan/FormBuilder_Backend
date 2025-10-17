using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FormBuilder.API.Configurations;
using FormBuilder.API.DataAccess.Implementations;
using FormBuilder.API.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace FormBuilder.API.Tests.DataAccess.Implementations
{
    public class QuestionRepositoryTests
    {
        private readonly Mock<IMongoCollection<Question>> _mockCollection;
        private readonly Mock<MongoDbContext> _mockContext;
        private readonly QuestionRepository _repository;

        public QuestionRepositoryTests()
        {
            _mockCollection = new Mock<IMongoCollection<Question>>();
            _mockContext = new Mock<MongoDbContext>("mongodb://test", "testdb");
            _mockContext.Setup(x => x.Questions).Returns(_mockCollection.Object);
            _repository = new QuestionRepository(_mockContext.Object);
        }

        /// <summary>
        /// Helper to create a mock IAsyncCursor&lt;Question&gt; that yields a list of items.
        /// </summary>
        private static Mock<IAsyncCursor<Question>> CreateCursor(List<Question> items)
        {
            var cursor = new Mock<IAsyncCursor<Question>>();
            cursor.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
                  .Returns(true)
                  .Returns(false);
            cursor.Setup(x => x.Current).Returns(items);
            return cursor;
        }

        /// <summary>
        /// Helper to create a mock IFindFluent&lt;Question, Question&gt; for list-returning scenarios.
        /// </summary>
        private static Mock<IFindFluent<Question, Question>> CreateFindFluent(List<Question> questions)
        {
            var mockCursor = CreateCursor(questions);
            var fluent = new Mock<IFindFluent<Question, Question>>();
            // When ToCursor is called, return the cursor
            fluent.Setup(f => f.ToCursor(It.IsAny<CancellationToken>())).Returns(mockCursor.Object);
            // When ToList is called, return the list
            fluent.Setup(f => f.ToList(It.IsAny<CancellationToken>())).Returns(questions);
            return fluent;
        }

        /// <summary>
        /// Helper to create a mock IFindFluent&lt;Question, Question&gt; for single-item or FirstOrDefault behavior.
        /// </summary>
        private static Mock<IFindFluent<Question, Question>> CreateFindFluentFirst(Question result)
        {
            var fluent = new Mock<IFindFluent<Question, Question>>();
            fluent.Setup(f => f.FirstOrDefault(It.IsAny<CancellationToken>())).Returns(result);
            return fluent;
        }

       
        [Fact]
        public void Add_ShouldInsertQuestion_WhenQuestionIsValid()
        {
            var question = new Question
            {
                QuestionId = "test-id-123",
                Type = "short_text",
                QuestionText = "What is your name?",
                Required = true,
                Order = 1
            };

            _mockCollection.Setup(x => x.InsertOne(
                It.IsAny<Question>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>())).Verifiable();

            _repository.Add(question);

            _mockCollection.Verify(x => x.InsertOne(
                It.Is<Question>(q => q.QuestionId == question.QuestionId),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Add_ShouldInsertQuestion_WithAllProperties()
        {
            var question = new Question
            {
                QuestionId = ObjectId.GenerateNewId().ToString(),
                Type = "choice",
                QuestionText = "Select an option",
                DescriptionEnabled = true,
                Description = "Choose one option",
                SingleChoice = true,
                MultipleChoice = false,
                Options = new List<Option>
                {
                    new Option { OptionId = "opt1", Value = "Option 1" },
                    new Option { OptionId = "opt2", Value = "Option 2" }
                },
                Format = null,
                Required = true,
                Order = 2,
                MaxLength = null,
                Enabled = true
            };

            _repository.Add(question);

            _mockCollection.Verify(x => x.InsertOne(
                It.Is<Question>(q =>
                    q.Options.Count == 2 &&
                    q.DescriptionEnabled == true &&
                    q.SingleChoice == true),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        
       
       
    
        [Fact]
        public void Delete_ShouldCallDeleteOne_EvenWhenQuestionDoesNotExist()
        {
            var questionId = "non-existent-question";
            var deleteResult = new DeleteResult.Acknowledged(0);
            _mockCollection.Setup(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.IsAny<CancellationToken>()
            )).Returns(deleteResult);

            _repository.Delete(questionId);

            _mockCollection.Verify(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Add_ShouldHandleQuestionWithNullOptions()
        {
            var question = new Question
            {
                QuestionId = "no-options-id",
                Type = "short_text",
                QuestionText = "Simple text question",
                Options = null,
                Order = 1
            };

            _repository.Add(question);

            _mockCollection.Verify(x => x.InsertOne(
                It.Is<Question>(q => q.Options == null),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Add_ShouldHandleQuestionWithEmptyQuestionId()
        {
            var question = new Question
            {
                QuestionId = "", // empty id should become generated
                Type = "long_text",
                QuestionText = "Describe something",
                Order = 1
            };

            _repository.Add(question);

            _mockCollection.Verify(x => x.InsertOne(
                It.Is<Question>(q => !string.IsNullOrEmpty(q.QuestionId)),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Update_ShouldHandleQuestionWithAllDefaultValues()
        {
            var question = new Question
            {
                QuestionId = "default-values-id",
                Type = "",
                QuestionText = "",
                DescriptionEnabled = false,
                Description = "",
                SingleChoice = false,
                MultipleChoice = false,
                Options = null,
                Format = null,
                Required = false,
                Order = 0,
                MaxLength = null,
                Enabled = true
            };

            var replaceResult = new ReplaceOneResult.Acknowledged(1, 1, null);
            _mockCollection.Setup(x => x.ReplaceOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.IsAny<Question>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()
            )).Returns(replaceResult);

            _repository.Update(question);

            _mockCollection.Verify(x => x.ReplaceOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.Is<Question>(q =>
                    q.QuestionId == question.QuestionId &&
                    q.Type == "" &&
                    q.Required == false),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
