using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VIMS.Application.Interfaces.Services;
using VIMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using VIMS.Domain.Enums;
using System;
using System.Linq;

namespace VIMS.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly VehicleInsuranceContext _context;

        public InvoiceService(VehicleInsuranceContext context)
        {
            _context = context;
            // Set license for QuestPDF (Community version)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInvoicePdf(int paymentId)
        {
            var payment = _context.Payments
                .Include(p => p.Policy)
                    .ThenInclude(po => po.Customer)
                .Include(p => p.Policy)
                    .ThenInclude(po => po.Vehicle)
                .Include(p => p.Policy)
                    .ThenInclude(po => po.Plan)
                .FirstOrDefault(p => p.PaymentId == paymentId);

            if (payment == null) return Array.Empty<byte>();

            var invoiceNumber = $"INV-{payment.PaymentId}-{DateTime.UtcNow.Ticks}";
            var invoiceDate = DateTime.UtcNow;
            var customerName = payment.Policy.Customer.FullName;
            var policyNumber = payment.Policy.PolicyNumber;
            var vehicleNumber = payment.Policy.Vehicle.RegistrationNumber;
            var totalAmount = payment.Amount;
            
            // Premium calculation logic for display (Format: TP/OD Split)
            var planName = payment.Policy.Plan.PlanName;
            var basePremium = payment.Policy.PremiumAmount; 

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Verdana"));


                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("VIMS INSURANCE").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text("Vehicle Insurance Management System").FontSize(9).Italic();
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("TAX INVOICE").FontSize(24).SemiBold().FontColor(Colors.Grey.Medium);
                            col.Item().Text($"{invoiceNumber}").FontSize(10);
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Billed To:").SemiBold();
                                c.Item().Text(customerName);
                                c.Item().Text($"Policy No: {policyNumber}");
                                c.Item().Text($"Vehicle Reg No: {vehicleNumber}");
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("Invoice Date:").SemiBold();
                                c.Item().Text(invoiceDate.ToString("dd-MMM-yyyy"));
                                c.Item().Text("Status:").SemiBold();
                                c.Item().Text("PAID").FontColor(Colors.Green.Medium).SemiBold();
                            });
                        });

                        col.Item().PaddingTop(1, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().PaddingTop(0.5f, Unit.Centimetre).Text($"Insurance Plan: {planName}").SemiBold();

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.RelativeColumn();
                                columns.ConstantColumn(100);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("#");
                                header.Cell().Element(CellStyle).Text("Description");
                                header.Cell().Element(CellStyle).AlignRight().Text("Amount (INR)");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                }
                            });

                            // Displaying the premium breakdown as the chosen format
                            // We split it into TP and OD components based on coverage flags
                            var plan = payment.Policy.Plan;
                            bool hasTP = plan.CoversThirdParty;
                            bool hasOD = plan.CoversOwnDamage;

                            int rowNum = 1;

                            if (hasTP && hasOD)
                            {
                                var tpPart = Math.Round(basePremium * 0.40m, 2);
                                var odPart = basePremium - tpPart;

                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Third Party (TP) Component");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{tpPart:N2}");

                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Own Damage (OD) Component");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{odPart:N2}");
                            }
                            else if (hasTP)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Third Party (TP) Component");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{basePremium:N2}");
                            }
                            else if (hasOD)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Own Damage (OD) Component");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{basePremium:N2}");
                            }
                            else
                            {
                                // Fallback for other plan types or if flags are missing
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text($"{planName} Premium");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{basePremium:N2}");
                            }

                            static IContainer ItemCellStyle(IContainer container)
                            {
                                return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            }

                        });

                        col.Item().AlignRight().PaddingTop(10).Text(x =>
                        {
                            x.Span("Total Premium: ").FontSize(12).SemiBold();
                            x.Span($"INR {totalAmount:N2}").FontSize(12).SemiBold().FontColor(Colors.Blue.Medium);
                        });

                        col.Item().PaddingTop(2, Unit.Centimetre).Column(c => {
                            c.Item().Text("Transaction Details").SemiBold().Underline();
                            c.Item().Text($"Payment Method: {payment.PaymentMethod}");
                            c.Item().Text($"Transaction ID: {payment.TransactionReference ?? "N/A"}");
                            c.Item().Text($"Payment Date: {payment.PaymentDate:dd-MMM-yyyy HH:mm}");
                        });

                        col.Item().PaddingTop(1, Unit.Centimetre).AlignCenter().Text("Thank you for choosing VIMS Insurance!").Italic().FontSize(9);
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
