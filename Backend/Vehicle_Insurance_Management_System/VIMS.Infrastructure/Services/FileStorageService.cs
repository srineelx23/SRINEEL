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
            
            // Build folder structure e.g., uploads/user_1/policydocuments/invoice
            var userFolder = Path.Combine(basePath, $"{baseType}_{identifier}");
            var docsFolder = Path.Combine(userFolder, documentType);
            
            Directory.CreateDirectory(docsFolder);

            var fileName = Guid.NewGuid() + "_" + file.FileName;
            var fullPath = Path.Combine(docsFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine("uploads", $"{baseType}_{identifier}", documentType, fileName).Replace("\\", "/");
        }
    }
}
