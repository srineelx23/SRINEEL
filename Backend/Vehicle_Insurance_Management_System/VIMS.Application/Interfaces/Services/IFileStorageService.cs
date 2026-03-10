using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string baseType, string identifier, string documentType);
        Task<string> CopyFileAsync(string existingRelativePath, string baseType, string identifier, string documentType);
    }
}
