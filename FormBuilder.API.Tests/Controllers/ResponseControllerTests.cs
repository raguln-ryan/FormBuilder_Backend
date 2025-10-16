using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using FormBuilder.API.Controllers;
using FormBuilder.API.Business.Interfaces;
using FormBuilder.API.DTOs.Form;
using FormBuilder.API.Common;
using FormBuilder.API.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System;

namespace FormBuilder.API.Tests.Controllers
{
    public class ResponseControllerTests
    {
        private readonly Mock<IResponseManager> _responseManagerMock;
        private readonly ResponseController _controller;

        public ResponseControllerTests()
        {
            _responseManagerMock = new Mock<IResponseManager>();
            _controller = new ResponseController(_responseManagerMock.Object);
            SetupUserClaims("TestUser", Roles.Learner);
        }

        private void SetupUserClaims(string name, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.NameIdentifier, "1")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Fact]
        public void GetPublishedForms_ReturnsOkWithForms()
        {
            // Arrange
            var forms = new List<FormLayoutResponseDto>
            {
                new FormLayoutResponseDto(),
                new FormLayoutResponseDto()
            };
            _responseManagerMock.Setup(x => x.GetPublishedForms())
                .Returns(forms);

            // Act
            var result = _controller.GetPublishedForms();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(forms, okResult.Value);
        }

        [Fact]
        public void SubmitResponse_ValidSubmission_ReturnsOk()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "1",
                Answers = new List<AnswerDto>()
            };
            var response = new Response { Id = 1 };
            _responseManagerMock.Setup(x => x.SubmitResponse(dto, It.IsAny<ClaimsPrincipal>()))
                .Returns((true, "Response submitted successfully", response));

            // Act
            var result = _controller.SubmitResponse(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic okValue = okResult.Value;
            Assert.True(okValue.success);
            Assert.Equal("Response submitted successfully", okValue.message);
            Assert.Equal(1, okValue.responseId);
        }

        [Fact]
        public void SubmitResponse_InvalidForm_ReturnsBadRequest()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "999",
                Answers = new List<AnswerDto>()
            };
            _responseManagerMock.Setup(x => x.SubmitResponse(dto, It.IsAny<ClaimsPrincipal>()))
                .Returns((false, "Form not found", null));

            // Act
            var result = _controller.SubmitResponse(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic badRequestValue = badRequestResult.Value;
            Assert.False(badRequestValue.success);
            Assert.Equal("Form not found", badRequestValue.message);
        }

        [Fact]
        public void GetResponsesByForm_ValidFormId_ReturnsOk()
        {
            // Arrange
            SetupUserClaims("Admin", Roles.Admin);
            var responses = new List<Response>
            {
                new Response { Id = 1, FormId = "1" },
                new Response { Id = 2, FormId = "1" }
            };
            _responseManagerMock.Setup(x => x.GetResponsesByForm("1"))
                .Returns(responses);

            // Act
            var result = _controller.GetResponsesByForm("1");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(responses, okResult.Value);
        }

        [Fact]
        public void GetResponseById_ExistingId_ReturnsOk()
        {
            // Arrange
            SetupUserClaims("Admin", Roles.Admin);
            var response = new Response { Id = 1, FormId = "1" };
            _responseManagerMock.Setup(x => x.GetResponseById("1"))
                .Returns((true, "Response found", response));

            // Act
            var result = _controller.GetResponseById("1");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public void GetResponseById_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            SetupUserClaims("Admin", Roles.Admin);
            _responseManagerMock.Setup(x => x.GetResponseById("999"))
                .Returns((false, "Response not found", null));

            // Act
            var result = _controller.GetResponseById("999");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Response not found", notFoundResult.Value);
        }

        [Fact]
        public void DownloadFile_ValidRequest_ReturnsFile()
        {
            // Arrange
            var fileAttachment = new FileAttachment
            {
                Id = 1,
                FileName = "test.pdf",
                FileType = "application/pdf",
                Base64Content = Convert.ToBase64String(new byte[] { 1, 2, 3 })
            };
            _responseManagerMock.Setup(x => x.GetFileAttachment(1, "q1"))
                .Returns((true, "File found", fileAttachment));

            // Act
            var result = _controller.DownloadFile("1", "q1");

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("test.pdf", fileResult.FileDownloadName);
            Assert.Equal("application/pdf", fileResult.ContentType);
        }

        [Fact]
        public void DownloadFile_InvalidResponseId_ReturnsBadRequest()
        {
            // Arrange & Act
            var result = _controller.DownloadFile("invalid", "q1");

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic badRequestValue = badRequestResult.Value;
            Assert.False(badRequestValue.success);
            Assert.Equal("Invalid response ID", badRequestValue.message);
        }

        [Fact]
        public void GetResponseWithDetails_ValidId_ReturnsOk()
        {
            // Arrange
            SetupUserClaims("Admin", Roles.Admin);
            var response = new Response { Id = 1, FormId = "1" };
            _responseManagerMock.Setup(x => x.GetResponseWithFiles(1))
                .Returns((true, "Response retrieved", response));

            // Act
            var result = _controller.GetResponseWithDetails("1");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public void GetResponseWithDetails_InvalidId_ReturnsBadRequest()
        {
            // Arrange
            SetupUserClaims("Admin", Roles.Admin);

            // Act
            var result = _controller.GetResponseWithDetails("invalid");

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic badRequestValue = badRequestResult.Value;
            Assert.False(badRequestValue.success);
            Assert.Equal("Invalid response ID", badRequestValue.message);
        }
    }
}