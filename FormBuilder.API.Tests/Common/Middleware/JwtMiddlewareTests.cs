using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FormBuilder.API.Common.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace FormBuilder.API.Tests.Common.Middleware
{
    public class JwtMiddlewareTests
    {
        private readonly JwtMiddleware _middleware;
        private readonly Mock<RequestDelegate> _nextMock;

        public JwtMiddlewareTests()
        {
            _nextMock = new Mock<RequestDelegate>();
            _middleware = new JwtMiddleware(_nextMock.Object);
        }

        [Fact]
        public async Task Invoke_ShouldCallNext_WhenNoAuthorizationHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();

            // Act
            await _middleware.Invoke(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
            Assert.Null(context.User.Identity.Name);
        }

        [Fact]
        public async Task Invoke_ShouldCallNext_WhenAuthorizationHeaderIsEmpty()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = "";

            // Act
            await _middleware.Invoke(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
        }

        [Fact]
        public async Task Invoke_ShouldSetUserClaims_WhenValidTokenProvided()
        {
            // Arrange
            var context = new DefaultHttpContext();
            
            // Create a simple JWT token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("your-256-bit-secret-key-for-testing-purposes-only!");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("sub", "testuser"),
                    new Claim("email", "test@example.com")
                }),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            context.Request.Headers["Authorization"] = $"Bearer {tokenString}";

            // Act
            await _middleware.Invoke(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
            Assert.NotNull(context.User);
            Assert.True(context.User.HasClaim(c => c.Type == "sub"));
            Assert.True(context.User.HasClaim(c => c.Type == "email"));
        }

        [Fact]
        public async Task Invoke_ShouldHandleInvalidToken_WithoutThrowing()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = "Bearer invalid-token";

            // Act
            await _middleware.Invoke(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
            // Should not throw exception, just continue
        }

        [Fact]
        public async Task Invoke_ShouldHandleTokenWithoutBearer_Correctly()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = "just-a-token-without-bearer";

            // Act
            await _middleware.Invoke(context);

            // Assert
            _nextMock.Verify(next => next(context), Times.Once);
        }
    }
}