using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using FormBuilder.API.Business.Interfaces;
using FormBuilder.API.Common;
using FormBuilder.API.Controllers;
using FormBuilder.API.DTOs.Form;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Newtonsoft.Json;

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

            // Setup default user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, Roles.Learner)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region GetPublishedForms Tests

        [Fact]
        public void GetPublishedForms_ReturnsOkWithForms()
        {
            // Arrange
            var forms = new List<FormLayoutResponseDto>
            {
                new FormLayoutResponseDto
                {
                    FormId = "form1",
                    Title = "Test Form",
                    Description = "Test Description",
                    Status = FormStatusDto.Published,
                    Questions = new List<QuestionDto>()
                }
            };

            _responseManagerMock.Setup(x => x.GetPublishedForms())
                .Returns(forms);

            // Act
            var result = _controller.GetPublishedForms();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(forms, okResult.Value);
        }

        [Fact]
        public void GetPublishedForms_EmptyList_ReturnsOkWithEmptyList()
        {
            // Arrange
            _responseManagerMock.Setup(x => x.GetPublishedForms())
                .Returns(new List<FormLayoutResponseDto>());

            // Act
            var result = _controller.GetPublishedForms();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedForms = Assert.IsType<List<FormLayoutResponseDto>>(okResult.Value);
            Assert.Empty(returnedForms);
        }

        #endregion

        #region SubmitResponse Tests

        [Fact]
        public void SubmitResponse_Success_ReturnsOkWithResponseId()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>
                {
                    new AnswerDto { QuestionId = "q1", Answer = "Test Answer" }
                }
            };

            var response = new Response 
            { 
                Id = 123,
                FormId = "form1",
                UserId = 1
            };

            _responseManagerMock.Setup(x => x.SubmitResponse(dto, It.IsAny<ClaimsPrincipal>()))
                .Returns((true, "Response submitted successfully", response));

            // Act
            var result = _controller.SubmitResponse(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonConvert.SerializeObject(okResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.True((bool)value.success);
            Assert.Equal("Response submitted successfully", (string)value.message);
            Assert.Equal(123, (int)value.responseId);
        }

        [Fact]
        public void SubmitResponse_Failure_ReturnsBadRequest()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1"
            };

            _responseManagerMock.Setup(x => x.SubmitResponse(dto, It.IsAny<ClaimsPrincipal>()))
                .Returns((false, "Validation failed", null));

            // Act
            var result = _controller.SubmitResponse(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.False((bool)value.success);
            Assert.Equal("Validation failed", (string)value.message);
        }

       
        [Fact]
        public void SubmitResponse_WithFileUploads_Success()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                FileUploads = new List<FileUploadDto>
                {
                    new FileUploadDto
                    {
                        QuestionId = "q1",
                        FileName = "test.pdf",
                        FileType = "application/pdf",
                        FileSize = 1024,
                        Base64Content = "base64string"
                    }
                }
            };

            var response = new Response { Id = 456 };

            _responseManagerMock.Setup(x => x.SubmitResponse(dto, It.IsAny<ClaimsPrincipal>()))
                .Returns((true, "Response with files submitted", response));

            // Act
            var result = _controller.SubmitResponse(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonConvert.SerializeObject(okResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.Equal(456, (int)value.responseId);
        }

        #endregion

        #region GetResponsesByForm Tests

        

        [Fact]
        public void GetResponseById_Success_ReturnsOkWithData()
        {
            // Arrange
            var responseId = "123";
            var response = new Response 
            { 
                Id = 123,
                FormId = "form1"
            };

            _responseManagerMock.Setup(x => x.GetResponseById(responseId))
                .Returns((true, "Success", response));

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseById(responseId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public void GetResponseById_NotFound_ReturnsNotFound()
        {
            // Arrange
            var responseId = "999";
            _responseManagerMock.Setup(x => x.GetResponseById(responseId))
                .Returns((false, "Response not found", null));

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseById(responseId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Response not found", notFoundResult.Value);
        }

        #endregion

        #region DownloadFile Tests

        [Fact]
        public void DownloadFile_ValidId_ReturnsFile()
        {
            // Arrange
            var responseId = "123";
            var questionId = "q1";
            var fileAttachment = new FileAttachment
            {
                FileName = "test.pdf",
                FileType = "application/pdf",
                Base64Content = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })
            };

            _responseManagerMock.Setup(x => x.GetFileAttachment(123, questionId))
                .Returns((true, "Success", fileAttachment));

            // Act
            var result = _controller.DownloadFile(responseId, questionId);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("test.pdf", fileResult.FileDownloadName);
            Assert.Equal("application/pdf", fileResult.ContentType);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, fileResult.FileContents);
        }

        [Fact]
        public void DownloadFile_InvalidResponseId_ReturnsBadRequest()
        {
            // Arrange
            var responseId = "abc"; // Invalid integer
            var questionId = "q1";

            // Act
            var result = _controller.DownloadFile(responseId, questionId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.False((bool)value.success);
            Assert.Equal("Invalid response ID", (string)value.message);
        }

        [Fact]
        public void DownloadFile_FileNotFound_ReturnsNotFound()
        {
            // Arrange
            var responseId = "123";
            var questionId = "q1";

            _responseManagerMock.Setup(x => x.GetFileAttachment(123, questionId))
                .Returns((false, "File not found", null));

            // Act
            var result = _controller.DownloadFile(responseId, questionId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var json = JsonConvert.SerializeObject(notFoundResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.False((bool)value.success);
            Assert.Equal("File not found", (string)value.message);
        }

        [Fact]
        public void DownloadFile_EmptyResponseId_ReturnsBadRequest()
        {
            // Arrange
            var responseId = "";
            var questionId = "q1";

            // Act
            var result = _controller.DownloadFile(responseId, questionId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.Equal("Invalid response ID", (string)value.message);
        }

        [Fact]
        public void DownloadFile_NullResponseId_ReturnsBadRequest()
        {
            // Arrange
            string responseId = null;
            var questionId = "q1";

            // Act
            var result = _controller.DownloadFile(responseId, questionId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.Equal("Invalid response ID", (string)value.message);
        }

        #endregion

        #region GetResponseWithDetails Tests

        [Fact]
        public void GetResponseWithDetails_ValidId_ReturnsOkWithData()
        {
            // Arrange
            var responseId = "456";
            var responseData = new
            {
                Response = new Response { Id = 456 },
                FileAttachments = new List<object>()
            };

            _responseManagerMock.Setup(x => x.GetResponseWithFiles(456))
                .Returns((true, "Success", responseData));

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseWithDetails(responseId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(responseData, okResult.Value);
        }

        [Fact]
        public void GetResponseWithDetails_InvalidResponseId_ReturnsBadRequest()
        {
            // Arrange
            var responseId = "xyz";

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseWithDetails(responseId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.False((bool)value.success);
            Assert.Equal("Invalid response ID", (string)value.message);
        }

        [Fact]
        public void GetResponseWithDetails_NotFound_ReturnsNotFound()
        {
            // Arrange
            var responseId = "789";

            _responseManagerMock.Setup(x => x.GetResponseWithFiles(789))
                .Returns((false, "Response not found", null));

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseWithDetails(responseId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var json = JsonConvert.SerializeObject(notFoundResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.False((bool)value.success);
            Assert.Equal("Response not found", (string)value.message);
        }

        [Fact]
        public void GetResponseWithDetails_EmptyResponseId_ReturnsBadRequest()
        {
            // Arrange
            var responseId = "";

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseWithDetails(responseId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.Equal("Invalid response ID", (string)value.message);
        }

        [Fact]
        public void GetResponseWithDetails_WhitespaceResponseId_ReturnsBadRequest()
        {
            // Arrange
            var responseId = "  ";

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseWithDetails(responseId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonConvert.SerializeObject(badRequestResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.Equal("Invalid response ID", (string)value.message);
        }

        #endregion

        #region Edge Cases and Additional Coverage

        [Fact]
        public void SubmitResponse_WithEmptyAnswers_Success()
        {
            // Arrange
            var dto = new FormSubmissionDto
            {
                FormId = "form1",
                Answers = new List<AnswerDto>()
            };

            var response = new Response { Id = 999 };

            _responseManagerMock.Setup(x => x.SubmitResponse(dto, It.IsAny<ClaimsPrincipal>()))
                .Returns((true, "Success", response));

            // Act
            var result = _controller.SubmitResponse(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonConvert.SerializeObject(okResult.Value);
            var value = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.Equal(999, (int)value.responseId);
        }

        [Fact]
        public void GetResponsesByForm_SpecialCharactersInFormId_ReturnsOk()
        {
            // Arrange
            var formId = "form-123_test";
            var responses = new List<Response>();

            _responseManagerMock.Setup(x => x.GetResponsesByForm(formId))
                .Returns(responses);

            SetupAdminContext();

            // Act
            var result = _controller.GetResponsesByForm(formId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public void DownloadFile_LargeFile_ReturnsFile()
        {
            // Arrange
            var responseId = "100";
            var questionId = "q1";
            var largeContent = new byte[1024 * 1024]; // 1MB
            Random.Shared.NextBytes(largeContent);
            
            var fileAttachment = new FileAttachment
            {
                FileName = "large.pdf",
                FileType = "application/pdf",
                Base64Content = Convert.ToBase64String(largeContent)
            };

            _responseManagerMock.Setup(x => x.GetFileAttachment(100, questionId))
                .Returns((true, "Success", fileAttachment));

            // Act
            var result = _controller.DownloadFile(responseId, questionId);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal(largeContent, fileResult.FileContents);
        }

        [Fact]
        public void GetResponseWithDetails_MaxIntResponseId_Success()
        {
            // Arrange
            var responseId = int.MaxValue.ToString();
            var responseData = new { Response = new Response(), FileAttachments = new List<object>() };

            _responseManagerMock.Setup(x => x.GetResponseWithFiles(int.MaxValue))
                .Returns((true, "Success", responseData));

            SetupAdminContext();

            // Act
            var result = _controller.GetResponseWithDetails(responseId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public void DownloadFile_SpecialCharactersInQuestionId_Success()
        {
            // Arrange
            var responseId = "1";
            var questionId = "q-1_test.2";
            var fileAttachment = new FileAttachment
            {
                FileName = "test.pdf",
                FileType = "application/pdf",
                Base64Content = Convert.ToBase64String(new byte[] { 1, 2, 3 })
            };

            _responseManagerMock.Setup(x => x.GetFileAttachment(1, questionId))
                .Returns((true, "Success", fileAttachment));

            // Act
            var result = _controller.DownloadFile(responseId, questionId);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("test.pdf", fileResult.FileDownloadName);
        }

        #endregion

        #region Helper Methods

        private void SetupAdminContext()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, Roles.Admin)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #endregion
    }
}