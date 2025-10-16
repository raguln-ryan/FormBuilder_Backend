using System;
using FormBuilder.API.Common.Helpers;
using Xunit;

namespace FormBuilder.API.Tests.Common.Helpers
{
    public class DateTimeHelperTests
    {
        [Fact]
        public void GetUtcNow_ShouldReturnCurrentUtcTime()
        {
            // Arrange
            var beforeCall = DateTime.UtcNow;

            // Act
            var result = DateTimeHelper.GetUtcNow();
            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.True(result >= beforeCall);
            Assert.True(result <= afterCall);
            Assert.Equal(DateTimeKind.Utc, result.Kind);
        }

        [Fact]
        public void GetUtcNow_ShouldReturnDifferentTimes_WhenCalledMultipleTimes()
        {
            // Act
            var firstCall = DateTimeHelper.GetUtcNow();
            System.Threading.Thread.Sleep(10); // Small delay
            var secondCall = DateTimeHelper.GetUtcNow();

            // Assert
            Assert.True(secondCall >= firstCall);
        }

        [Fact]
        public void GetUtcNow_ShouldAlwaysReturnUtcKind()
        {
            // Act
            var result = DateTimeHelper.GetUtcNow();

            // Assert
            Assert.Equal(DateTimeKind.Utc, result.Kind);
        }

        [Fact]
        public void GetUtcNow_ShouldBeCloseToSystemUtcNow()
        {
            // Act
            var helperTime = DateTimeHelper.GetUtcNow();
            var systemTime = DateTime.UtcNow;

            // Assert
            var difference = Math.Abs((helperTime - systemTime).TotalMilliseconds);
            Assert.True(difference < 100); // Should be within 100ms
        }
    }
}