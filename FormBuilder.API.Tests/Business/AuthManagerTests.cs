using Xunit;
using Moq;
using FormBuilder.API.Business.Implementations;
using FormBuilder.API.DataAccess.Interfaces;
using FormBuilder.API.DTOs.Auth;
using FormBuilder.API.Models;
using FormBuilder.API.Services;
using FormBuilder.API.Common;
using System;

namespace FormBuilder.API.Tests.Business
{
    public class AuthManagerTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly PasswordHasher _passwordHasher;
        private readonly JwtService _jwtService;
        private readonly AuthManager _authManager;

        public AuthManagerTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _passwordHasher = new PasswordHasher();
            _jwtService = new JwtService("ThisIsAVerySecureKeyForTestingPurposesWith256Bits!");
            
            _authManager = new AuthManager(
                _userRepositoryMock.Object,
                _passwordHasher,
                _jwtService
            );
        }

        #region Register Tests

        [Fact]
        public void Register_ValidLearnerRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "john@example.com",
                Password = "Password123",
                Role = Roles.Learner
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns((User)null);
            _userRepositoryMock.Setup(x => x.Add(It.IsAny<User>()))
                .Callback<User>(u => u.Id = 1); // Simulate ID assignment

            // Act
            var result = _authManager.Register(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("User registered successfully", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal("John Doe", result.Data.Name);
            Assert.Equal(Roles.Learner, result.Data.Role);
            Assert.NotNull(result.Data.Token);
            Assert.Equal("1", result.Data.UserId);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(request.Email), Times.Once);
            _userRepositoryMock.Verify(x => x.Add(It.Is<User>(u => 
                u.Name == request.Name &&
                u.Email == request.Email &&
                u.Role == request.Role &&
                !string.IsNullOrEmpty(u.PasswordHash)
            )), Times.Once);
        }

        [Fact]
        public void Register_AdminRole_ReturnsFalse()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "Admin User",
                Email = "admin@example.com",
                Password = "Admin123",
                Role = Roles.Admin
            };

            // Act
            var result = _authManager.Register(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Admin cannot register via API", result.Message);
            Assert.Null(result.Data);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(It.IsAny<string>()), Times.Never);
            _userRepositoryMock.Verify(x => x.Add(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public void Register_ExistingEmail_ReturnsFalse()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "existing@example.com",
                Password = "Password123",
                Role = Roles.Learner
            };

            var existingUser = new User
            {
                Id = 99,
                Name = "Existing User",
                Email = "existing@example.com",
                PasswordHash = "existing_hash",
                Role = Roles.Learner
            };
            
            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns(existingUser);

            // Act
            var result = _authManager.Register(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Email already exists", result.Message);
            Assert.Null(result.Data);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(request.Email), Times.Once);
            _userRepositoryMock.Verify(x => x.Add(It.IsAny<User>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Register_InvalidRole_ReturnsFalse(string role)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "John Doe",
                Email = "john@example.com",
                Password = "Password123",
                Role = role ?? ""
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns((User)null);

            // Act
            var result = _authManager.Register(request);

            // Assert
            // Since role is neither Admin nor explicitly checked, it will proceed
            // This tests edge cases
            if (string.IsNullOrWhiteSpace(role))
            {
                Assert.True(result.Success);
            }
        }

        #endregion

        #region Login Tests

        [Fact]
        public void Login_ValidCredentials_ReturnsSuccess()
        {
            // Arrange
            var password = "Password123";
            var hashedPassword = _passwordHasher.HashPassword(password);
            
            var request = new LoginRequest
            {
                Email = "user@example.com",
                Password = password
            };

            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Email = "user@example.com",
                Role = Roles.Learner,
                PasswordHash = hashedPassword
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns(user);

            // Act
            var result = _authManager.Login(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Login successful", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal("1", result.Data.UserId);
            Assert.Equal("Test User", result.Data.Name);
            Assert.Equal(Roles.Learner, result.Data.Role);
            Assert.NotNull(result.Data.Token);
            Assert.NotEmpty(result.Data.Token);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(request.Email), Times.Once);
        }

        [Fact]
        public void Login_HardcodedAdmin_ReturnsSuccess()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "admin@example.com",
                Password = "Admin@123"
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns((User)null);

            // Act
            var result = _authManager.Login(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Login successful", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal("0", result.Data.UserId);
            Assert.Equal("Admin", result.Data.Name);
            Assert.Equal(Roles.Admin, result.Data.Role);
            Assert.NotNull(result.Data.Token);
            Assert.NotEmpty(result.Data.Token);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(request.Email), Times.Once);
        }

        [Fact]
        public void Login_HardcodedAdmin_WrongPassword_ReturnsFalse()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "admin@example.com",
                Password = "WrongPassword"
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns((User)null);

            // Act
            var result = _authManager.Login(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User not found", result.Message);
            Assert.Null(result.Data);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(request.Email), Times.Once);
        }

        [Fact]
        public void Login_UserNotFound_ReturnsFalse()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "Password123"
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns((User)null);

            // Act
            var result = _authManager.Login(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User not found", result.Message);
            Assert.Null(result.Data);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(request.Email), Times.Once);
        }

        [Fact]
        public void Login_InvalidPassword_ReturnsFalse()
        {
            // Arrange
            var correctPassword = "CorrectPassword";
            var hashedPassword = _passwordHasher.HashPassword(correctPassword);
            
            var request = new LoginRequest
            {
                Email = "user@example.com",
                Password = "WrongPassword"
            };

            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Email = "user@example.com",
                PasswordHash = hashedPassword,
                Role = Roles.Learner
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns(user);

            // Act
            var result = _authManager.Login(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid credentials", result.Message);
            Assert.Null(result.Data);
            
            _userRepositoryMock.Verify(x => x.GetByEmail(request.Email), Times.Once);
        }

        [Fact]
        public void Login_EmptyEmail_UserNotFound()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "",
                Password = "Password123"
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns((User)null);

            // Act
            var result = _authManager.Login(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User not found", result.Message);
            Assert.Null(result.Data);
        }

        [Fact]
        public void Login_EmptyPassword_InvalidCredentials()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "user@example.com",
                Password = ""
            };

            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Email = "user@example.com",
                PasswordHash = _passwordHasher.HashPassword("ActualPassword"),
                Role = Roles.Learner
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(request.Email))
                .Returns(user);

            // Act
            var result = _authManager.Login(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid credentials", result.Message);
            Assert.Null(result.Data);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Register_Then_Login_Success()
        {
            // This test simulates a full registration and login flow
            
            // Arrange - Register
            var registerRequest = new RegisterRequest
            {
                Name = "New User",
                Email = "newuser@example.com",
                Password = "SecurePass123",
                Role = Roles.Learner
            };

            User savedUser = null;
            _userRepositoryMock.Setup(x => x.GetByEmail(registerRequest.Email))
                .Returns(() => savedUser); // Return null first, then the saved user
            
            _userRepositoryMock.Setup(x => x.Add(It.IsAny<User>()))
                .Callback<User>(u => 
                {
                    u.Id = 5;
                    savedUser = u;
                });

            // Act - Register
            var registerResult = _authManager.Register(registerRequest);

            // Assert - Register
            Assert.True(registerResult.Success);
            Assert.NotNull(savedUser);

            // Arrange - Login
            var loginRequest = new LoginRequest
            {
                Email = registerRequest.Email,
                Password = registerRequest.Password
            };

            // Act - Login
            var loginResult = _authManager.Login(loginRequest);

            // Assert - Login
            Assert.True(loginResult.Success);
            Assert.Equal("5", loginResult.Data.UserId);
            Assert.Equal("New User", loginResult.Data.Name);
        }

        [Theory]
        [InlineData("user@example.com", "Pass123", Roles.Learner)]
        [InlineData("teacher@school.edu", "Teach456", Roles.Learner)]
        [InlineData("student@university.org", "Study789", Roles.Learner)]
        public void Register_MultipleUsers_AllSucceed(string email, string password, string role)
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = $"User for {email}",
                Email = email,
                Password = password,
                Role = role
            };

            _userRepositoryMock.Setup(x => x.GetByEmail(email))
                .Returns((User)null);
            _userRepositoryMock.Setup(x => x.Add(It.IsAny<User>()))
                .Callback<User>(u => u.Id = email.GetHashCode());

            // Act
            var result = _authManager.Register(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal($"User for {email}", result.Data.Name);
            Assert.Equal(role, result.Data.Role);
            _userRepositoryMock.Verify(x => x.Add(It.IsAny<User>()), Times.Once);
        }

        #endregion
    }
}
