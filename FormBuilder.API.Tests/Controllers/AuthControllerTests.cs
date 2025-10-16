using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using FormBuilder.API.Controllers;
using FormBuilder.API.Business.Interfaces;
using FormBuilder.API.DTOs.Auth;
using FormBuilder.API.Common;

namespace FormBuilder.API.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthManager> _authManagerMock;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _authManagerMock = new Mock<IAuthManager>();
            _controller = new AuthController(_authManagerMock.Object);
        }

        [Fact]
        public void Register_ValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Password = "Password123",
                Role = Roles.Learner
            };
            var response = new AuthResponse
            {
                UserId = "1",
                Name = "Test User",
                Role = Roles.Learner,
                Token = "test-token"
            };
            _authManagerMock.Setup(x => x.Register(request))
                .Returns((Success: true, Message: "Registration successful", Data: response));

            // Act
            var result = _controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public void Register_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "Test User",
                Email = "existing@example.com",
                Password = "Password123",
                Role = Roles.Learner
            };
            _authManagerMock.Setup(x => x.Register(request))
                .Returns((Success: false, Message: "Email already exists", Data: null));

            // Act
            var result = _controller.Register(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Email already exists", badRequestResult.Value);
        }

        [Fact]
        public void Login_ValidCredentials_ReturnsOk()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123"
            };
            var response = new AuthResponse
            {
                UserId = "1",
                Name = "Test User",
                Role = Roles.Learner,
                Token = "test-token"
            };
            _authManagerMock.Setup(x => x.Login(request))
                .Returns((Success: true, Message: "Login successful", Data: response));

            // Act
            var result = _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public void Login_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            };
            _authManagerMock.Setup(x => x.Login(request))
                .Returns((false, "Invalid credentials", null));

            // Act
            var result = _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Invalid credentials", unauthorizedResult.Value);
        }

        [Fact]
        public void Login_UserNotFound_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "Password123"
            };
            _authManagerMock.Setup(x => x.Login(request))
                .Returns((false, "User not found", null));

            // Act
            var result = _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("User not found", unauthorizedResult.Value);
        }

        [Fact]
        public void Login_AdminCredentials_ReturnsOkWithAdminRole()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "admin@example.com",
                Password = "Admin@123"
            };
            var response = new AuthResponse
            {
                UserId = "0",
                Name = "Admin",
                Role = Roles.Admin,
                Token = "admin-token"
            };
            _authManagerMock.Setup(x => x.Login(request))
                .Returns((true, "Login successful", response));

            // Act
            var result = _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var authResponse = Assert.IsType<AuthResponse>(okResult.Value);
            Assert.Equal(Roles.Admin, authResponse.Role);
        }

        [Fact]
        public void Register_AdminRole_ReturnsOk()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "Admin User",
                Email = "admin2@example.com",
                Password = "AdminPass123",
                Role = Roles.Admin
            };
            var response = new AuthResponse
            {
                UserId = "2",
                Name = "Admin User",
                Role = Roles.Admin,
                Token = "admin-token"
            };
            _authManagerMock.Setup(x => x.Register(request))
                .Returns((true, "Registration successful", response));

            // Act
            var result = _controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var authResponse = Assert.IsType<AuthResponse>(okResult.Value);
            Assert.Equal(Roles.Admin, authResponse.Role);
        }

        [Fact]
        public void Register_EmptyName_ReturnsBadRequest()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Name = "",
                Email = "test@example.com",
                Password = "Password123",
                Role = "Learner"
            };
            _authManagerMock.Setup(x => x.Register(request))
                .Returns((false, "Name is required", null));

            // Act
            var result = _controller.Register(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Name is required", badRequestResult.Value);
        }

        [Fact]
        public void Login_EmptyEmail_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "",
                Password = "Password123"
            };
            _authManagerMock.Setup(x => x.Login(request))
                .Returns((false, "Email is required", null));

            // Act
            var result = _controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Email is required", unauthorizedResult.Value);
        }
    }
}