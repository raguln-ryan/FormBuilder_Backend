using Xunit;
using FormBuilder.API.DataAccess.Implementations;
using FormBuilder.API.Models;
using FormBuilder.API.Configurations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FormBuilder.API.Tests.DataAccess
{
    public class ResponseRepositoryTests : IDisposable
    {
        private readonly MySqlDbContext _context;
        private readonly ResponseRepository _repository;

        public ResponseRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<MySqlDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new MySqlDbContext(options);
            _repository = new ResponseRepository(_context);
        }

        [Fact]
        public void Add_ValidResponse_AddsSuccessfully()
        {
            // Arrange
            var response = new Response
            {
                FormId = "form1",
                UserId = 1,
                SubmittedAt = DateTime.UtcNow,
                Details = new List<ResponseDetail>
                {
                    new ResponseDetail { QuestionId = "q1", Answer = "Answer 1" }
                }
            };

            // Act
            _repository.Add(response);

            // Assert
            var savedResponse = _context.Responses.FirstOrDefault();
            Assert.NotNull(savedResponse);
            Assert.Equal("form1", savedResponse.FormId);
            Assert.Equal(1, savedResponse.UserId);
        }

        [Fact]
        public void Add_ResponseWithNullDetails_AddsSuccessfully()
        {
            // Arrange
            var response = new Response
            {
                FormId = "form2",
                UserId = 2,
                SubmittedAt = DateTime.UtcNow,
                Details = null
            };

            // Act
            _repository.Add(response);

            // Assert
            var savedResponse = _context.Responses.FirstOrDefault();
            Assert.NotNull(savedResponse);
            Assert.Equal("form2", savedResponse.FormId);
            Assert.Null(savedResponse.Details);
        }

        [Fact]
        public void Add_ResponseWithEmptyDetails_AddsSuccessfully()
        {
            // Arrange
            var response = new Response
            {
                FormId = "form3",
                UserId = 3,
                SubmittedAt = DateTime.UtcNow,
                Details = new List<ResponseDetail>()
            };

            // Act
            _repository.Add(response);

            // Assert
            var savedResponse = _context.Responses.FirstOrDefault();
            Assert.NotNull(savedResponse);
            Assert.Empty(savedResponse.Details);
        }

        [Fact]
        public void GetById_ExistingResponse_ReturnsResponse()
        {
            // Arrange
            var response = new Response
            {
                FormId = "form1",
                UserId = 1,
                SubmittedAt = DateTime.UtcNow
            };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            var result = _repository.GetById(response.Id.ToString());

            // Assert
            Assert.NotNull(result);
            Assert.Equal("form1", result.FormId);
        }

        [Fact]
        public void GetById_NonExistingResponse_ReturnsNull()
        {
            // Act
            var result = _repository.GetById("999");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetById_InvalidIdFormat_ReturnsNull()
        {
            // Act
            var result = _repository.GetById("invalid-id");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetById_NullId_ReturnsNull()
        {
            // Act
            var result = _repository.GetById(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetById_EmptyStringId_ReturnsNull()
        {
            // Act
            var result = _repository.GetById("");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetByFormId_ReturnsResponsesForForm()
        {
            // Arrange
            var response1 = new Response { FormId = "form1", UserId = 1 };
            var response2 = new Response { FormId = "form1", UserId = 2 };
            var response3 = new Response { FormId = "form2", UserId = 3 };
            
            _context.Responses.AddRange(response1, response2, response3);
            _context.SaveChanges();

            // Act
            var result = _repository.GetByFormId("form1");

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, r => Assert.Equal("form1", r.FormId));
        }

        [Fact]
        public void GetByFormId_NoResponsesForForm_ReturnsEmptyList()
        {
            // Arrange
            var response = new Response { FormId = "form1", UserId = 1 };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            var result = _repository.GetByFormId("form2");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetByFormId_NullFormId_ReturnsEmptyList()
        {
            // Act
            var result = _repository.GetByFormId(null);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetByUserId_ReturnsResponsesForUser()
        {
            // Arrange
            var response1 = new Response { FormId = "form1", UserId = 1, SubmittedAt = DateTime.UtcNow };
            var response2 = new Response { FormId = "form2", UserId = 1, SubmittedAt = DateTime.UtcNow.AddHours(-1) };
            var response3 = new Response { FormId = "form3", UserId = 2, SubmittedAt = DateTime.UtcNow };
            
            _context.Responses.AddRange(response1, response2, response3);
            _context.SaveChanges();

            // Act
            var result = _repository.GetByUserId(1);

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, r => Assert.Equal(1, r.UserId));
        }

        [Fact]
        public void GetByUserId_NoResponsesForUser_ReturnsEmptyList()
        {
            // Arrange
            var response = new Response { FormId = "form1", UserId = 1 };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            var result = _repository.GetByUserId(999);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetByUserId_ZeroUserId_ReturnsEmptyList()
        {
            // Act
            var result = _repository.GetByUserId(0);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetByUserId_NegativeUserId_ReturnsEmptyList()
        {
            // Act
            var result = _repository.GetByUserId(-1);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GetAll_ReturnsAllResponses()
        {
            // Arrange
            var response1 = new Response { FormId = "form1", UserId = 1 };
            var response2 = new Response { FormId = "form2", UserId = 2 };
            
            _context.Responses.AddRange(response1, response2);
            _context.SaveChanges();

            // Act
            var result = _repository.GetAll();

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void GetAll_NoResponses_ReturnsEmptyList()
        {
            // Act
            var result = _repository.GetAll();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Update_ExistingResponse_UpdatesSuccessfully()
        {
            // Arrange
            var response = new Response
            {
                FormId = "form1",
                UserId = 1,
                SubmittedAt = DateTime.UtcNow
            };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            response.SubmittedAt = DateTime.UtcNow.AddHours(1);
            _repository.Update(response);

            // Assert
            var updatedResponse = _context.Responses.Find(response.Id);
            Assert.NotNull(updatedResponse);
        }

        
        
        [Fact]
        public void Delete_NonExistingResponse_NoException()
        {
            // Act & Assert - Should not throw
            _repository.Delete("999");
        }

        [Fact]
        public void Delete_NullId_NoException()
        {
            // Act & Assert - Should not throw
            _repository.Delete(null);
        }

        [Fact]
        public void Delete_InvalidIdFormat_NoException()
        {
            // Act & Assert - Should not throw
            _repository.Delete("invalid-id");
        }

        [Fact]
        public void DeleteAllByFormId_DeletesAllResponsesForForm()
        {
            // Arrange
            var response1 = new Response { FormId = "form1", UserId = 1 };
            var response2 = new Response { FormId = "form1", UserId = 2 };
            var response3 = new Response { FormId = "form2", UserId = 3 };
            
            _context.Responses.AddRange(response1, response2, response3);
            _context.SaveChanges();

            // Act
            var deletedCount = _repository.DeleteAllByFormId("form1");

            // Assert
            Assert.Equal(2, deletedCount);
            var remainingResponses = _context.Responses.ToList();
            Assert.Single(remainingResponses);
            Assert.Equal("form2", remainingResponses[0].FormId);
        }

        [Fact]
        public void DeleteAllByFormId_NoResponsesForForm_ReturnsZero()
        {
            // Arrange
            var response = new Response { FormId = "form1", UserId = 1 };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            var deletedCount = _repository.DeleteAllByFormId("form2");

            // Assert
            Assert.Equal(0, deletedCount);
            Assert.Single(_context.Responses);
        }

        [Fact]
        public void DeleteAllByFormId_NullFormId_ReturnsZero()
        {
            // Arrange
            var response = new Response { FormId = "form1", UserId = 1 };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            var deletedCount = _repository.DeleteAllByFormId(null);

            // Assert
            Assert.Equal(0, deletedCount);
        }

        [Fact]
        public void DeleteAllByFormId_EmptyFormId_ReturnsZero()
        {
            // Arrange
            var response = new Response { FormId = "form1", UserId = 1 };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            var deletedCount = _repository.DeleteAllByFormId("");

            // Assert
            Assert.Equal(0, deletedCount);
        }

        [Fact]
        public void Add_MultipleResponses_AddsAllSuccessfully()
        {
            // Arrange
            var response1 = new Response
            {
                FormId = "form1",
                UserId = 1,
                SubmittedAt = DateTime.UtcNow
            };
            var response2 = new Response
            {
                FormId = "form2",
                UserId = 2,
                SubmittedAt = DateTime.UtcNow
            };

            // Act
            _repository.Add(response1);
            _repository.Add(response2);

            // Assert
            var savedResponses = _context.Responses.ToList();
            Assert.Equal(2, savedResponses.Count);
        }

     
        [Fact]
        public void ResponseWithComplexDetails_HandledCorrectly()
        {
            // Arrange
            var response = new Response
            {
                FormId = "form1",
                UserId = 1,
                SubmittedAt = DateTime.UtcNow,
                Details = new List<ResponseDetail>
                {
                    new ResponseDetail { QuestionId = "q1", Answer = "Answer 1" },
                    new ResponseDetail { QuestionId = "q2", Answer = "Answer 2" },
                    new ResponseDetail { QuestionId = "q3", Answer = "Answer 3" }
                }
            };

            // Act
            _repository.Add(response);

            // Assert
            var saved = _repository.GetById(response.Id.ToString());
            Assert.NotNull(saved);
            Assert.Equal(3, saved.Details.Count);
        }

        [Fact]
        public void GetById_WithIncludedDetails_ReturnsFullResponse()
        {
            // Arrange
            var response = new Response
            {
                FormId = "form1",
                UserId = 1,
                SubmittedAt = DateTime.UtcNow,
                Details = new List<ResponseDetail>
                {
                    new ResponseDetail { QuestionId = "q1", Answer = "Answer 1" }
                }
            };
            _context.Responses.Add(response);
            _context.SaveChanges();

            // Act
            var result = _repository.GetById(response.Id.ToString());

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Details);
            Assert.Single(result.Details);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}