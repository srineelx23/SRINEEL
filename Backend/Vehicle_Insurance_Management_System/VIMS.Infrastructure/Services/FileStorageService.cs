using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Services;

namespace VIMS.Infrastructure.Services
{
    public class FileStorageService : IFileStorageService
    {
        public async Task<string> SaveFileAsync(IFormFile file, string baseType, string identifier, string documentType)
        {
            if (file == null || file.Length == 0) return null;

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            
            // identifier can contain sub-paths now, like "user_1/transfer_policies/transfer_10"
            var targetFolder = Path.Combine(basePath, $"{baseType}_{identifier}", documentType);
            
            Directory.CreateDirectory(targetFolder);

            var fileName = Guid.NewGuid() + "_" + file.FileName;
            var fullPath = Path.Combine(targetFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine("uploads", $"{baseType}_{identifier}", documentType, fileName).Replace("\\", "/");
        }

        public async Task<string> CopyFileAsync(string existingRelativePath, string baseType, string identifier, string documentType)
        {
            if (string.IsNullOrEmpty(existingRelativePath)) return null;

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var sourcePath = Path.Combine(basePath, existingRelativePath);

            if (!File.Exists(sourcePath)) return null;

            var uploadsPath = Path.Combine(basePath, "uploads");
            var targetFolder = Path.Combine(uploadsPath, $"{baseType}_{identifier}", documentType);
            
            Directory.CreateDirectory(targetFolder);

            var fileName = Guid.NewGuid() + "_" + Path.GetFileName(existingRelativePath);
            var fullPath = Path.Combine(targetFolder, fileName);

            File.Copy(sourcePath, fullPath, true);

            return Path.Combine("uploads", $"{baseType}_{identifier}", documentType, fileName).Replace("\\", "/");
        }

        public async Task DeleteFileAsync(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public async Task DeleteDirectoryAsync(string baseType, string identifier)
        {
            var folderName = $"{baseType}_{identifier}";
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folderName);
            
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }

        public async Task MoveDirectoryAsync(string sourceBaseType, string sourceIdentifier, string targetBaseType, string targetIdentifier)
        {
            var sourceFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", $"{sourceBaseType}_{sourceIdentifier}");
            var targetFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", $"{targetBaseType}_{targetIdentifier}");

            if (Directory.Exists(sourceFolder))
            {
                if (Directory.Exists(targetFolder))
                {
                    // If target already exists, we might need to merge or delete.
                    // For now, let's just delete target if it exists to avoid conflicts, or handle uniquely.
                    Directory.Delete(targetFolder, true);
                }
                
                var parentDir = Directory.GetParent(targetFolder).FullName;
                if (!Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                Directory.Move(sourceFolder, targetFolder);
            }
        }
    }
}
