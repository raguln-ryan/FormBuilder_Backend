using Xunit;
using FormBuilder.API.DataAccess.Implementations;
using FormBuilder.API.Models;
using FormBuilder.API.Configurations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FormBuilder.API.Tests.DataAccess
{
    public class FileAttachmentRepositoryTests : IDisposable
    {
        private readonly MySqlDbContext _context;
        private readonly FileAttachmentRepository _repository;

        public FileAttachmentRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<MySqlDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new MySqlDbContext(options);
            _repository = new FileAttachmentRepository(_context);
        }

        [Fact]
        public void Add_ValidFileAttachment_AddsSuccessfully()
        {
            // Arrange
            var fileAttachment = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "test.pdf",
                FileType = "application/pdf",
                FileSize = 1024,
                Base64Content = "base64content",
                UploadedAt = DateTime.UtcNow
            };

            // Act
            _repository.Add(fileAttachment);
            _repository.SaveChanges();

            // Assert
            var saved = _context.FileAttachments.FirstOrDefault();
            Assert.NotNull(saved);
            Assert.Equal("test.pdf", saved.FileName);
        }

        [Fact]
        public void AddRange_MultipleFiles_AddsAllSuccessfully()
        {
            // Arrange
            var files = new List<FileAttachment>
            {
                new FileAttachment
                {
                    ResponseId = 1,
                    QuestionId = "q1",
                    FileName = "file1.pdf",
                    FileType = "application/pdf",
                    FileSize = 1024,
                    Base64Content = "content1"
                },
                new FileAttachment
                {
                    ResponseId = 1,
                    QuestionId = "q2",
                    FileName = "file2.pdf",
                    FileType = "application/pdf",
                    FileSize = 2048,
                    Base64Content = "content2"
                }
            };

            // Act
            _repository.AddRange(files);
            _repository.SaveChanges();

            // Assert
            var saved = _context.FileAttachments.ToList();
            Assert.Equal(2, saved.Count);
        }

        [Fact]
        public void GetById_ExistingFile_ReturnsFile()
        {
            // Arrange
            var file = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "test.pdf",
                FileType = "application/pdf",
                FileSize = 1024,
                Base64Content = "base64content"
            };
            _context.FileAttachments.Add(file);
            _context.SaveChanges();

            // Act
            var result = _repository.GetById(file.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test.pdf", result.FileName);
        }

        [Fact]
        public void GetByResponseAndQuestion_ReturnsCorrectFile()
        {
            // Arrange
            var file = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "test.pdf",
                FileType = "application/pdf",
                FileSize = 1024,
                Base64Content = "base64content"
            };
            _context.FileAttachments.Add(file);
            _context.SaveChanges();

            // Act
            var result = _repository.GetByResponseAndQuestion(1, "q1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test.pdf", result.FileName);
        }

        [Fact]
        public void GetByResponseId_ReturnsAllFilesForResponse()
        {
            // Arrange
            var file1 = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "file1.pdf",
                FileType = "application/pdf",
                FileSize = 1024,
                Base64Content = "content1"
            };
            var file2 = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q2",
                FileName = "file2.pdf",
                FileType = "application/pdf",
                FileSize = 2048,
                Base64Content = "content2"
            };
            _context.FileAttachments.AddRange(file1, file2);
            _context.SaveChanges();

            // Act
            var result = _repository.GetByResponseId(1);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, f => Assert.Equal(1, f.ResponseId));
        }

        [Fact]
        public void Delete_ExistingFile_DeletesSuccessfully()
        {
            // Arrange
            var file = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "test.pdf",
                FileType = "application/pdf",
                FileSize = 1024,
                Base64Content = "base64content"
            };
            _context.FileAttachments.Add(file);
            _context.SaveChanges();

            // Act
            _repository.Delete(file.Id);
            _repository.SaveChanges();

            // Assert
            var deleted = _context.FileAttachments.Find(file.Id);
            Assert.Null(deleted);
        }

        [Fact]
        public void DeleteByResponseId_DeletesAllFilesForResponse()
        {
            // Arrange
            var file1 = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "file1.pdf",
                FileType = "application/pdf",
                FileSize = 1024,
                Base64Content = "content1"
            };
            var file2 = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q2",
                FileName = "file2.pdf",
                FileType = "application/pdf",
                FileSize = 2048,
                Base64Content = "content2"
            };
            _context.FileAttachments.AddRange(file1, file2);
            _context.SaveChanges();

            // Act
            _repository.DeleteByResponseId(1);
            _repository.SaveChanges();

            // Assert
            var remaining = _context.FileAttachments.Where(f => f.ResponseId == 1).ToList();
            Assert.Empty(remaining);
        }

        [Fact]
        public void SaveChanges_ReturnsTrue_WhenChangesAreSaved()
        {
            // Arrange
            var file = new FileAttachment
            {
                ResponseId = 1,
                QuestionId = "q1",
                FileName = "test.pdf",
                FileType = "application/pdf",
                FileSize = 1024,
                Base64Content = "base64content"
            };
            _repository.Add(file);

            // Act
            var result = _repository.SaveChanges();

            // Assert
            Assert.True(result);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}