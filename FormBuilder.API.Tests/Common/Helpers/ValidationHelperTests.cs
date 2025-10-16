using FormBuilder.API.Common.Helpers;
using Xunit;

namespace FormBuilder.API.Tests.Common.Helpers
{
    public class ValidationHelperTests
    {
        [Theory]
        [InlineData("test@example.com", true)]
        [InlineData("user.name@example.co.uk", true)]
        [InlineData("first.last@subdomain.example.com", true)]
        [InlineData("test+tag@example.com", true)]
        [InlineData("test_email@example-domain.com", true)]
        public void IsValidEmail_ShouldReturnTrue_ForValidEmails(string email, bool expected)
        {
            // Act
            var result = ValidationHelper.IsValidEmail(email);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData(" ", false)]
        [InlineData(null, false)]
        [InlineData("notanemail", false)]
        [InlineData("@example.com", false)]
        [InlineData("test@", false)]
        [InlineData("test @example.com", false)]
        [InlineData("test@example", false)]
        [InlineData("test@@example.com", false)]
        [InlineData("test.example.com", false)]
        public void IsValidEmail_ShouldReturnFalse_ForInvalidEmails(string email, bool expected)
        {
            // Act
            var result = ValidationHelper.IsValidEmail(email);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidEmail_ShouldHandleWhitespace()
        {
            // Arrange
            var emailWithSpaces = "  test@example.com  ";

            // Act
            var result = ValidationHelper.IsValidEmail(emailWithSpaces);

            // Assert
            Assert.False(result); // Should be false because of surrounding whitespace
        }

        [Fact]
        public void IsValidEmail_ShouldRejectMultipleAtSymbols()
        {
            // Arrange
            var email = "test@@example.com";

            // Act
            var result = ValidationHelper.IsValidEmail(email);

            // Assert
            Assert.False(result);
        }
    }
}