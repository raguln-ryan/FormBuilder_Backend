using Xunit;
using Moq;
using FormBuilder.API.DataAccess.Implementations;
using FormBuilder.API.Models;
using FormBuilder.API.Configurations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace FormBuilder.API.Tests.DataAccess
{
    public class UserRepositoryTests : IDisposable
    {
        private readonly MySqlDbContext _context;
        private readonly UserRepository _repository;

        public UserRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<MySqlDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new MySqlDbContext(options);
            _repository = new UserRepository(_context);
        }

        [Fact]
        public void Add_ValidUser_AddsSuccessfully()
        {
            // Arrange
            var user = new User
            {
                Name = "Test User",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                Role = "Learner"
            };

            // Act
            _repository.Add(user);

            // Assert
            var savedUser = _context.Users.FirstOrDefault(u => u.Email == "test@example.com");
            Assert.NotNull(savedUser);
            Assert.Equal("Test User", savedUser.Name);
        }

        [Fact]
        public void GetById_ExistingUser_ReturnsUser()
        {
            // Arrange
            var user = new User
            {
                Name = "Test User",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                Role = "Learner"
            };
            _context.Users.Add(user);
            _context.SaveChanges();

            // Act
            var result = _repository.GetById(user.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public void GetByEmail_ExistingEmail_ReturnsUser()
        {
            // Arrange
            var user = new User
            {
                Name = "Test User",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                Role = "Learner"
            };
            _context.Users.Add(user);
            _context.SaveChanges();

            // Act
            var result = _repository.GetByEmail("test@example.com");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test User", result.Name);
        }

        [Fact]
        public void Update_ExistingUser_UpdatesSuccessfully()
        {
            // Arrange
            var user = new User
            {
                Name = "Original Name",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                Role = "Learner"
            };
            _context.Users.Add(user);
            _context.SaveChanges();

            // Act
            user.Name = "Updated Name";
            _repository.Update(user);

            // Assert
            var updatedUser = _context.Users.Find(user.Id);
            Assert.Equal("Updated Name", updatedUser.Name);
        }

        [Fact]
        public void Delete_ExistingUser_DeletesSuccessfully()
        {
            // Arrange
            var user = new User
            {
                Name = "Test User",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                Role = "Learner"
            };
            _context.Users.Add(user);
            _context.SaveChanges();

            // Act
            _repository.Delete(user.Id);

            // Assert
            var deletedUser = _context.Users.Find(user.Id);
            Assert.Null(deletedUser);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}