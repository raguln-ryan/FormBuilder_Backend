using Xunit;
using Moq;
using FormBuilder.API.DataAccess.Implementations;
using FormBuilder.API.Models;
using FormBuilder.API.Configurations;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FormBuilder.API.Tests.DataAccess
{
    public class QuestionRepositoryTests
    {
        private readonly Mock<IMongoCollection<Question>> _mockCollection;
        private readonly Mock<MongoDbContext> _mockContext;
        private readonly QuestionRepository _repository;

        public QuestionRepositoryTests()
        {
            _mockCollection = new Mock<IMongoCollection<Question>>();
            _mockContext = new Mock<MongoDbContext>();
            _mockContext.Setup(c => c.Questions).Returns(_mockCollection.Object);
            _repository = new QuestionRepository(_mockContext.Object);
        }

        [Fact]
        public void Add_CallsInsertOne()
        {
            // Arrange
            var question = new Question { QuestionId = "q1", QuestionText = "Test" };

            // Act
            _repository.Add(question);

            // Assert
            _mockCollection.Verify(x => x.InsertOne(It.IsAny<Question>(), null, default(CancellationToken)), Times.Once);
        }

        [Fact]
        public void GetById_ReturnsQuestion()
        {
            // Arrange
            var question = new Question { QuestionId = "q1", QuestionText = "Test" };
            var mockFindFluent = new Mock<IFindFluent<Question, Question>>();
            
            mockFindFluent.Setup(x => x.FirstOrDefault(It.IsAny<CancellationToken>()))
                .Returns(question);
            
            _mockCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<Question>>(), null))
                .Returns(mockFindFluent.Object);

            // Act
            var result = _repository.GetById("q1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("q1", result.QuestionId);
        }

        [Fact]
        public void GetAll_ReturnsAllQuestions()
        {
            // Arrange
            var questions = new List<Question>
            {
                new Question { QuestionId = "q1" },
                new Question { QuestionId = "q2" }
            };
            var mockFindFluent = new Mock<IFindFluent<Question, Question>>();
            
            mockFindFluent.Setup(x => x.ToList(It.IsAny<CancellationToken>()))
                .Returns(questions);
            
            _mockCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<Question>>(), null))
                .Returns(mockFindFluent.Object);

            // Act
            var result = _repository.GetAll();

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void Update_CallsReplaceOne()
        {
            // Arrange
            var question = new Question { QuestionId = "q1", QuestionText = "Updated" };
            var replaceResult = new ReplaceOneResult.Acknowledged(1, 1, null);
            
            _mockCollection.Setup(x => x.ReplaceOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.IsAny<Question>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(replaceResult);

            // Act
            _repository.Update(question);

            // Assert
            _mockCollection.Verify(x => x.ReplaceOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.IsAny<Question>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Delete_CallsDeleteOne()
        {
            // Arrange
            var deleteResult = new DeleteResult.Acknowledged(1);
            _mockCollection.Setup(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.IsAny<CancellationToken>()))
                .Returns(deleteResult);

            // Act
            _repository.Delete("q1");

            // Assert
            _mockCollection.Verify(x => x.DeleteOne(
                It.IsAny<FilterDefinition<Question>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}