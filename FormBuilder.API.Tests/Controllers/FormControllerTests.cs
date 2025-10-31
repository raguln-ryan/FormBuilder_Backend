using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using FormBuilder.API.Controllers;
using FormBuilder.API.Business.Interfaces;
using FormBuilder.API.DTOs.Form;
using FormBuilder.API.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace FormBuilder.API.Tests.Controllers
{
    public class FormControllerTests
    {
        private readonly Mock<IFormManager> _formManagerMock;
        private readonly FormController _controller;

        public FormControllerTests()
        {
            _formManagerMock = new Mock<IFormManager>();
            _controller = new FormController(_formManagerMock.Object);
            SetupUserClaims("Admin", Roles.Admin);
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
        public void CreateFormConfig_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var dto = new FormConfigRequestDto();
            _formManagerMock.Setup(x => x.CreateFormConfig(dto, "Admin"))
                .Returns((false, "Invalid form data", null));

            // Act
            var result = _controller.CreateFormConfig(dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid form data", badRequestResult.Value);
        }

        [Fact]
        public void UpdateFormConfig_ValidRequest_ReturnsOk()
        {
            // Arrange
            var dto = new FormConfigRequestDto();
            var response = new FormConfigResponseDto();
            _formManagerMock.Setup(x => x.UpdateFormConfig("1", dto))
                .Returns((true, "Form updated successfully", response));

            // Act
            var result = _controller.UpdateFormConfig("1", dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public void UpdateFormLayout_ValidRequest_ReturnsOk()
        {
            // Arrange
            var dto = new FormLayoutRequestDto();
            var response = new FormLayoutResponseDto();
            _formManagerMock.Setup(x => x.UpdateFormLayout("1", dto, "Admin"))
                .Returns((true, "Layout updated successfully", response));

            // Act
            var result = _controller.UpdateFormLayout("1", dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(response, okResult.Value);
        }

        [Fact]
        public void UpdateFormLayout_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            var dto = new FormLayoutRequestDto();
            _formManagerMock.Setup(x => x.UpdateFormLayout("1", dto, "Admin"))
                .Returns((false, "Invalid layout data", null));

            // Act
            var result = _controller.UpdateFormLayout("1", dto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid layout data", badRequestResult.Value);
        }

        [Fact]
        public void DeleteForm_NonExistingForm_ReturnsBadRequest()
        {
            // Arrange
            _formManagerMock.Setup(x => x.DeleteForm("999"))
                .Returns((Success: false, Message: "Form not found"));

            // Act
            var result = _controller.DeleteForm("999");

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Form not found", badRequestResult.Value);
        }

        [Fact]
        public void GetAllForms_ReturnsOkWithForms()
        {
            // Arrange
            var paginatedResponse = new
            {
                data = new List<FormLayoutResponseDto>
                {
                    new FormLayoutResponseDto(),
                    new FormLayoutResponseDto()
                },
                pagination = new
                {
                    offset = 0,
                    limit = 10,
                    total = 2
                }
            };
            
            _formManagerMock.Setup(x => x.GetAllForms(It.IsAny<ClaimsPrincipal>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((true, "Forms retrieved successfully", (object)paginatedResponse));

            // Act
            var result = _controller.GetAllForms();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(paginatedResponse, okResult.Value);
        }

        [Fact]
        public void GetFormById_ExistingForm_ReturnsOk()
        {
            // Arrange
            var form = new FormLayoutResponseDto();
            _formManagerMock.Setup(x => x.GetFormById("1", It.IsAny<ClaimsPrincipal>()))
                .Returns((Success: true, Message: "Form found", Data: form));

            // Act
            var result = _controller.GetFormById("1");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(form, okResult.Value);
        }

        [Fact]
        public void GetFormById_NonExistingForm_ReturnsNotFound()
        {
            // Arrange
            _formManagerMock.Setup(x => x.GetFormById("999", It.IsAny<ClaimsPrincipal>()))
                .Returns((Success: false, Message: "Form not found", Data: null));

            // Act
            var result = _controller.GetFormById("999");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Form not found", notFoundResult.Value);
        }

 
        [Fact]
        public void PublishForm_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            _formManagerMock.Setup(x => x.PublishForm("999", "Admin"))
                .Returns((Success: false, Message: "Form not found"));

            // Act
            var result = _controller.PublishForm("999");

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Form not found", badRequestResult.Value);
        }
    }
}