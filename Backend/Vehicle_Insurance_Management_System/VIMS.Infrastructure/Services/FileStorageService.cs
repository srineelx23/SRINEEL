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
    }
}
