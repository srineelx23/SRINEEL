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
            var usageText = usageMatch.Success ? usageMatch.Groups[1].Value.Trim() : string.Empty;
            var vehicleTypeLineMatch = Regex.Match(rcText, @"Vehicle\s*Type\s*[:\s]+([^\r\n]+)", RegexOptions.IgnoreCase);
            var vehicleTypeLine = vehicleTypeLineMatch.Success ? vehicleTypeLineMatch.Groups[1].Value.Trim() : string.Empty;
            var vehicleDescriptorMatch = Regex.Match(rcText, @"(?im)^\s*Vehicle\s*[:\-]\s*([^\r\n]+)$");
            var vehicleDescriptor = vehicleDescriptorMatch.Success ? vehicleDescriptorMatch.Groups[1].Value.Trim() : string.Empty;
            var combinedText = string.Join(" ", new[] { usageText, vehicleTypeLine, vehicleDescriptor }.Where(s => !string.IsNullOrWhiteSpace(s)));

            bool isElectric = result.FuelType == "EV"
                || Regex.IsMatch(combinedText, @"\b(electric|ev)\b", RegexOptions.IgnoreCase);

            if (Regex.IsMatch(combinedText, @"\b(?:2|two)\s*[- ]?wheeler\b|\bmotorcycle\b|\bscooter\b|\bmoped\b", RegexOptions.IgnoreCase))
                result.VehicleClass = isElectric ? "EVTwoWheeler" : "TwoWheeler";
            else if (Regex.IsMatch(combinedText, @"\b(?:3|three)\s*[- ]?wheeler\b|\bauto\s*rickshaw\b|\be[- ]?rickshaw\b|\brickshaw\b", RegexOptions.IgnoreCase))
                result.VehicleClass = isElectric ? "EVThreeWheeler" : "ThreeWheeler";
            else if (Regex.IsMatch(combinedText, @"\b(hmv|heavy|truck|bus|lgv|mgv)\b", RegexOptions.IgnoreCase))
                result.VehicleClass = "HeavyVehicle";
            else if (Regex.IsMatch(combinedText, @"\b(?:4|four)\s*[- ]?wheeler\b|\b(lmv|car|motor\s*car)\b", RegexOptions.IgnoreCase))
                result.VehicleClass = isElectric ? "EVCar" : "Car";
            else
                // Default: LMV Motor Car (most common)
                result.VehicleClass = isElectric ? "EVCar" : "Car";

            Console.WriteLine($"[OcrService] VehicleClass resolved: {result.VehicleClass}  (classificationText={combinedText})");


            // 6. Ex-Showroom Price
            // Prefer the amount on the same line as "Ex-Showroom Price".
            // If OCR splits table cells into the next line, use the immediate following lines.
            var exShowroomLineMatch = Regex.Match(
                invoiceText,
                @"(?im)^.*Ex[\s\-]*Showroom\s*Price.*$",
                RegexOptions.IgnoreCase);

            if (exShowroomLineMatch.Success)
            {
                var sameLineAmount = ExtractAmountFromLine(exShowroomLineMatch.Value);
                if (sameLineAmount.HasValue)
                {
                    result.InvoiceAmount = sameLineAmount.Value;
                }
                else
                {
                    var tailText = invoiceText.Substring(exShowroomLineMatch.Index);
                    var lines = tailText
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                        .Skip(1)
                        .Take(2)
                        .ToList();

                    foreach (var line in lines)
                    {
                        var amount = ExtractAmountFromLine(line);
                        if (amount.HasValue)
                        {
                            result.InvoiceAmount = amount.Value;
                            break;
                        }
                    }
                }
            }

            Console.WriteLine($"=== EXTRACTED: Reg={result.RegistrationNumber}, Make={result.Make}, Model={result.Model}, Year={result.Year}, Fuel={result.FuelType}, Type={result.VehicleType}, Amount={result.InvoiceAmount} ===");

            if (string.IsNullOrWhiteSpace(result.RegistrationNumber) && result.Year == 0 && result.InvoiceAmount == 0)
                throw new BadRequestException("Could not extract details from documents. Please ensure the PDFs are clear and valid.");

            return result;
        }

        private static void ValidateRcAndInvoiceConsistency(string rcText, string invoiceText, OcrExtractionResultDTO extracted)
        {
            var rcEngine = ExtractEngineNumber(rcText);
            var invoiceEngine = ExtractEngineNumber(invoiceText);
            var rcChassis = ExtractChassisNumber(rcText);
            var invoiceChassis = ExtractChassisNumber(invoiceText);

            // Vehicle identity must match across both documents.
            if (string.IsNullOrWhiteSpace(rcEngine) || string.IsNullOrWhiteSpace(invoiceEngine))
            {
                throw new BadRequestException("Unable to validate document consistency: Engine number must be clearly present in both RC and Invoice.");
            }

            if (!string.Equals(NormalizeAlphaNumeric(rcEngine), NormalizeAlphaNumeric(invoiceEngine), StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException($"Document mismatch: RC engine number '{rcEngine}' does not match Invoice engine number '{invoiceEngine}'.");
            }

            if (string.IsNullOrWhiteSpace(rcChassis) || string.IsNullOrWhiteSpace(invoiceChassis))
            {
                throw new BadRequestException("Unable to validate document consistency: Chassis number must be clearly present in both RC and Invoice.");
            }

            if (!string.Equals(NormalizeAlphaNumeric(rcChassis), NormalizeAlphaNumeric(invoiceChassis), StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException($"Document mismatch: RC chassis number '{rcChassis}' does not match Invoice chassis number '{invoiceChassis}'.");
            }

            var rcMakeModel = NormalizeVehicleText($"{extracted.Make} {extracted.Model}");
            var invoiceMakeModel = NormalizeVehicleText(ExtractInvoiceMakeModel(invoiceText));
            if (!string.IsNullOrWhiteSpace(rcMakeModel) && !string.IsNullOrWhiteSpace(invoiceMakeModel)
                && !rcMakeModel.Contains(invoiceMakeModel, StringComparison.OrdinalIgnoreCase)
                && !invoiceMakeModel.Contains(rcMakeModel, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Document mismatch: Vehicle make/model in RC does not match Invoice.");
            }
        }

        private static string ExtractEngineNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var labeled = Regex.Match(text,
                @"(?:Engine\s*(?:/\s*Motor)?\s*(?:No\.?|Number)?|Engine\s*(?:No\.?|Number)|Engine\s*/\s*Motor\s*Number)\s*(?:\([^\)]*\))?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-]{5,})",
                RegexOptions.IgnoreCase);
            if (labeled.Success)
            {
                return labeled.Groups[1].Value.Trim();
            }

            // Fallback: strict line-level engine label extraction.
            var lineFallback = Regex.Match(text,
                @"(?im)^\s*Engine\s*(?:/\s*Motor)?\s*(?:No\.?|Number)?\s*(?:\([^\)]*\))?\s*[:\-]?\s*([A-Z0-9][A-Z0-9\-]{5,})\s*$");
            return lineFallback.Success ? lineFallback.Groups[1].Value.Trim() : string.Empty;
        }

        private static string ExtractChassisNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var labeled = Regex.Match(text,
                @"(?:Chassis\s*(?:No\.?|Number)?|CH\.?\s*NO\.?)\s*(?:\([^\)]*\))?\s*[:\-]?\s*([A-Z0-9\-]{8,})",
                RegexOptions.IgnoreCase);
            if (labeled.Success)
            {
                return labeled.Groups[1].Value.Trim();
            }

            // Fallback: common VIN format.
            var vin = Regex.Match(text, @"\b([A-HJ-NPR-Z0-9]{17})\b", RegexOptions.IgnoreCase);
            return vin.Success ? vin.Groups[1].Value.Trim() : string.Empty;
        }

        private static string ExtractInvoiceMakeModel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var match = Regex.Match(text,
                @"(?:Model\s*Name|Vehicle\s*Model|Model|Variant)\s*[:\-]?\s*([A-Za-z0-9\-/ ]{2,})",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static decimal? ExtractAmountFromLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var matches = Regex.Matches(line, @"-?(?:\d{1,3}(?:,\d{2,3})+|\d{5,})");
            if (matches.Count == 0)
            {
                return null;
            }

            var parsed = matches
                .Select(m => m.Value.Replace(",", ""))
                .Select(v => decimal.TryParse(v, out var amt) ? amt : 0m)
                .Where(amt => amt >= 10000m)
                .ToList();

            if (parsed.Count == 0)
            {
                return null;
            }

            return parsed.Max();
        }

        private static string NormalizeAlphaNumeric(string value)
        {
            return Regex.Replace((value ?? string.Empty).ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
        }

        private static string NormalizeVehicleText(string value)
        {
            return Regex.Replace((value ?? string.Empty).ToUpperInvariant(), @"\s+", " ").Trim();
        }

        public async Task<string> ExtractTextAsync(string filePath)
        {
            try
            {
                var fullPath = ResolveStoredFilePath(filePath);
                if (!File.Exists(fullPath)) return string.Empty;

                var fileBytes = await File.ReadAllBytesAsync(fullPath);
                return await ExtractTextFromBytesAsync(fileBytes);
            }
            catch { return string.Empty; }
        }

        private static string ResolveStoredFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            // If absolute path was stored, use it directly.
            if (Path.IsPathRooted(filePath))
            {
                return filePath;
            }

            var normalized = filePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var currentDirectory = Directory.GetCurrentDirectory();

            // Typical storage location used by FileStorageService.
            var fromWwwroot = Path.Combine(currentDirectory, "wwwroot", normalized);
            if (File.Exists(fromWwwroot))
            {
                return fromWwwroot;
            }

            // Fallback for cases where the relative path already contains "wwwroot".
            return Path.Combine(currentDirectory, normalized);
        }

        private async Task<string> ExtractTextFromBytesAsync(byte[] fileBytes)
        {
            var sb = new StringBuilder();
            using var doc = PdfDocument.Open(fileBytes);
            foreach (var page in doc.GetPages())
            {
                var words = page.GetWords().ToList();
                if (!words.Any()) continue;

                double lastY = -9999;
                foreach (var word in words)
                {
                    var wordY = Math.Round(word.BoundingBox.Bottom, 0);
                    if (Math.Abs(wordY - lastY) > 3)
                        sb.AppendLine();
                    else
                        sb.Append(' ');

                    sb.Append(word.Text);
                    lastY = wordY;
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private async Task<string> ExtractTextFromPdfAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            return await ExtractTextFromBytesAsync(fileBytes);
        }
    }
}

