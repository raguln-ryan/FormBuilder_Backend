using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FormBuilder.API.Common.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FormBuilder.API.Tests.Common.Middleware
{
    public class ExceptionMiddlewareTests
    {
        private readonly ExceptionMiddleware _middleware;

        public ExceptionMiddlewareTests()
        {
            RequestDelegate next = (HttpContext hc) => throw new Exception("Test exception message");
            _middleware = new ExceptionMiddleware(next);
        }

        [Fact]
        public async Task InvokeAsync_ShouldCatchException_AndReturnInternalServerError()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await _middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            
            // Read response body
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var response = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
            
            Assert.Equal("Test exception message", response.message);
        }

        [Fact]
        public async Task InvokeAsync_ShouldPassThrough_WhenNoException()
        {
            // Arrange
            RequestDelegate next = (HttpContext hc) =>
            {
                hc.Response.StatusCode = 200;
                return Task.CompletedTask;
            };
            var middleware = new ExceptionMiddleware(next);
            var context = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_ShouldHandleDifferentExceptionTypes()
        {
            // Arrange
            RequestDelegate next = (HttpContext hc) => throw new InvalidOperationException("Invalid operation");
            var middleware = new ExceptionMiddleware(next);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var response = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
            
            Assert.Equal("Invalid operation", response.message);
        }

        private class ErrorResponse
        {
            public string message { get; set; }
        }
    }
}