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

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}