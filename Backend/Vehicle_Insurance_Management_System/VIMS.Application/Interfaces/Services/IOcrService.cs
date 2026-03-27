using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using VIMS.Application.DTOs;

namespace VIMS.Application.Interfaces.Services
{
    public interface IOcrService
    {
        Task<OcrExtractionResultDTO> ExtractVehicleDetailsAsync(IFormFile rcDocument, IFormFile invoiceDocument);
        Task<string> ExtractTextAsync(string filePath);
    }
}
