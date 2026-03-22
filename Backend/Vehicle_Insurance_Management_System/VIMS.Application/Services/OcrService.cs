using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using VIMS.Application.DTOs;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Exceptions;

namespace VIMS.Application.Services
{
    public class OcrService : IOcrService
    {
        public OcrService(IConfiguration configuration) { }

        public async Task<OcrExtractionResultDTO> ExtractVehicleDetailsAsync(IFormFile rcDocument, IFormFile invoiceDocument)
        {
            // No try-catch — let GlobalExceptionHandler handle errors.
            if (rcDocument == null || invoiceDocument == null)
                throw new BadRequestException("Both RC and Invoice documents are required.");

            string rcText = await ExtractTextFromPdfAsync(rcDocument);
            string invoiceText = await ExtractTextFromPdfAsync(invoiceDocument);

            Console.WriteLine("=== RC TEXT ===");
            Console.WriteLine(rcText);
            Console.WriteLine("=== INVOICE TEXT ===");
            Console.WriteLine(invoiceText);

            var result = new OcrExtractionResultDTO();

            // 1. Registration Number — Indian format: XX 00 XX 0000 or XX 0X XX 0000
            // State(2 letters) + RTO(1-3 alphanumeric e.g. 05, 8C) + Series(1-3 letters) + Number(1-4 digits)
            var regMatch = Regex.Match(rcText, @"Registration\s*Number\s*[:\s]*([A-Z]{2}\s?[A-Z0-9]{1,3}\s?[A-Z]{1,3}\s?\d{1,4})", RegexOptions.IgnoreCase);
            result.RegistrationNumber = regMatch.Success
                ? Regex.Replace(regMatch.Groups[1].Value, @"\s+", "").Trim()
                : "";

            // 2. Make & Model — look for "Make & Model" label
            var makeModelMatch = Regex.Match(rcText, @"Make\s*[&and]*\s*Model\s*[:\s]+([^\r\n]+)", RegexOptions.IgnoreCase);
            if (makeModelMatch.Success)
            {
                var parts = makeModelMatch.Groups[1].Value.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                result.Make  = parts.Length > 0 ? parts[0].Trim() : "";
                result.Model = parts.Length > 1 ? parts[1].Trim() : "";
            }

            // 3. Year of Manufacture — preserve negatives
            var yearMatch = Regex.Match(rcText, @"Year\s*of\s*Manufacture\s*[:\s]*(-?\d{4})", RegexOptions.IgnoreCase);
            if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out int year))
                result.Year = year;

            // 4. Fuel Type
            var fuelMatch = Regex.Match(rcText, @"Fuel\s*Type\s*[:\s]*([^\r\n]+)", RegexOptions.IgnoreCase);
            if (fuelMatch.Success)
            {
                var fuel = fuelMatch.Groups[1].Value.Trim().ToLower();
                if (fuel.Contains("petrol"))   result.FuelType = "Petrol";
                else if (fuel.Contains("diesel"))  result.FuelType = "Diesel";
                else if (fuel.Contains("electric") || fuel.Contains("ev")) result.FuelType = "EV";
                else if (fuel.Contains("cng"))     result.FuelType = "CNG";
                else result.FuelType = fuel;
            }

            // 5. Vehicle Type (Private/Commercial)
            result.VehicleType = rcText.Contains("Commercial", StringComparison.OrdinalIgnoreCase)
                ? "Commercial" : "Private";

            // 5b. Vehicle Class — normalize to match plan's ApplicableVehicleType
            // RC contains "Vehicle Usage / Class" e.g.:
            //   Private (LMV - Motor Car)          → Car
            //   Private (LMV - Electric Motor Car)  → EVCar
            //   Two Wheeler - Motorcycle/Scooter     → TwoWheeler
            //   Three Wheeler - Auto Rickshaw         → ThreeWheeler
            //   HMV - Truck / Bus                    → HeavyVehicle
            var usageMatch = Regex.Match(rcText, @"Vehicle\s*(?:Usage|Class|Usage\s*/\s*Class)\s*[:\s]+([^\r\n]+)", RegexOptions.IgnoreCase);
            var usageText = usageMatch.Success ? usageMatch.Groups[1].Value.ToLower() : rcText.ToLower();
            // Also check Vehicle Type field if Usage/Class not found
            var vehicleTypeLineMatch = Regex.Match(rcText, @"Vehicle\s*Type\s*[:\s]+([^\r\n]+)", RegexOptions.IgnoreCase);
            var vehicleTypeLine = vehicleTypeLineMatch.Success ? vehicleTypeLineMatch.Groups[1].Value.ToLower() : "";
            var combinedText = usageText + " " + vehicleTypeLine;

            bool isElectric = result.FuelType == "EV";
            if (combinedText.Contains("two") || combinedText.Contains("2-wheel") || combinedText.Contains("motorcycle") || combinedText.Contains("scooter") || combinedText.Contains("moped"))
                result.VehicleClass = isElectric ? "EVTwoWheeler" : "TwoWheeler";
            else if (combinedText.Contains("three") || combinedText.Contains("3-wheel") || combinedText.Contains("auto rickshaw") || combinedText.Contains("e-rickshaw") || combinedText.Contains("rickshaw"))
                result.VehicleClass = isElectric ? "EVThreeWheeler" : "ThreeWheeler";
            else if (combinedText.Contains("hmv") || combinedText.Contains("heavy") || combinedText.Contains("truck") || combinedText.Contains("bus") || combinedText.Contains("lgv") || combinedText.Contains("mgv"))
                result.VehicleClass = "HeavyVehicle";
            else
                // Default: LMV Motor Car (most common)
                result.VehicleClass = isElectric ? "EVCar" : "Car";

            Console.WriteLine($"[OcrService] VehicleClass resolved: {result.VehicleClass}  (usageText={usageText.Trim()})");


            // 6. Ex-Showroom Price
            // The PDF table places "Ex-Showroom Price" on one line,
            // and the numbers (1 | 8703 | 1 | 14,89,000 | 14,89,000) on the NEXT line.
            // Strategy: match "Ex-Showroom Price" then ALSO grab the next line,
            // find all numbers with 5+ chars (e.g. "14,89,000") and take the last (= Amount column).
            var exShowroomMatch = Regex.Match(invoiceText,
                @"Ex[\s\-]*Showroom\s*Price[^\r\n]*[\r\n]+([^\r\n]+)",
                RegexOptions.IgnoreCase);

            if (exShowroomMatch.Success)
            {
                // The next line after "Ex-Showroom Price"
                var nextLine = exShowroomMatch.Groups[1].Value;
                Console.WriteLine($"[OcrService] Ex-Showroom next line: {nextLine}");

                // Find all numbers (with optional commas) that are large enough to be a price
                // "-?[\d,]+" with at least one comma OR 5+ digits = price-sized number
                var priceMatches = Regex.Matches(nextLine, @"-?(?:\d{1,2},)?\d{2},\d{3}|-?\d{5,}");
                if (priceMatches.Count > 0)
                {
                    // Last match = Amount column (rightmost in the table: 14,89,000)
                    var lastNum = priceMatches[priceMatches.Count - 1].Value.Replace(",", "");
                    Console.WriteLine($"[OcrService] Extracted amount string: {lastNum}");
                    if (decimal.TryParse(lastNum, out decimal amt))
                        result.InvoiceAmount = amt;
                }
            }

            // Fallback: scan entire invoice for "Ex-Showroom" near a big number
            if (result.InvoiceAmount == 0)
            {
                // Look up to 3 lines after "Ex-Showroom Price"
                var fallbackMatch = Regex.Match(invoiceText,
                    @"Ex[\s\-]*Showroom\s*Price(?:[^\r\n]*[\r\n]){1,3}",
                    RegexOptions.IgnoreCase);
                if (fallbackMatch.Success)
                {
                    var block = fallbackMatch.Value;
                    var nums = Regex.Matches(block, @"-?(?:\d{1,2},)?\d{2},\d{3}|-?\d{5,}");
                    if (nums.Count > 0)
                    {
                        var lastNum = nums[nums.Count - 1].Value.Replace(",", "");
                        if (decimal.TryParse(lastNum, out decimal amt))
                            result.InvoiceAmount = amt;
                    }
                }
            }

            Console.WriteLine($"=== EXTRACTED: Reg={result.RegistrationNumber}, Make={result.Make}, Model={result.Model}, Year={result.Year}, Fuel={result.FuelType}, Type={result.VehicleType}, Amount={result.InvoiceAmount} ===");

            if (string.IsNullOrWhiteSpace(result.RegistrationNumber) && result.Year == 0 && result.InvoiceAmount == 0)
                throw new BadRequestException("Could not extract details from documents. Please ensure the PDFs are clear and valid.");

            return result;
        }

        private async Task<string> ExtractTextFromPdfAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            var sb = new StringBuilder();
            using var doc = PdfDocument.Open(fileBytes);
            foreach (var page in doc.GetPages())
            {
                // Reconstruct text preserving line breaks based on Y position
                var words = page.GetWords().ToList();
                if (!words.Any()) continue;

                double lastY = -9999;
                foreach (var word in words)
                {
                    var wordY = Math.Round(word.BoundingBox.Bottom, 0);
                    if (Math.Abs(wordY - lastY) > 3)
                        sb.AppendLine(); // new line when Y changes significantly
                    else
                        sb.Append(' ');

                    sb.Append(word.Text);
                    lastY = wordY;
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}

