using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using VIMS.Application.DTOs;

namespace VIMS.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPricingService _pricingService;
        private readonly IClaimsRepository _claimsRepository;

        public InvoiceService(IPaymentRepository paymentRepository, IPricingService pricingService, IClaimsRepository claimsRepository)
        {
            _paymentRepository = paymentRepository;
            _pricingService = pricingService;
            _claimsRepository = claimsRepository;
            // Set license for QuestPDF (Community version)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateInvoicePdf(int paymentId)
        {
            var payment = _paymentRepository.GetByIdWithDetailsAsync(paymentId).GetAwaiter().GetResult();

            if (payment == null) return Array.Empty<byte>();

            var invoiceNumber = $"INV-{payment.PaymentId}-{DateTime.UtcNow.Ticks}";
            var invoiceDate = DateTime.UtcNow;
            var customerName = payment.Policy.Customer.FullName;
            var policyNumber = payment.Policy.PolicyNumber;
            var vehicleNumber = payment.Policy.Vehicle.RegistrationNumber;
            var totalPaidAmount = payment.Amount;

            var policy = payment.Policy;
            var plan = policy.Plan;
            var vehicle = policy.Vehicle;

            // Re-calculate the exact breakdown using PricingService
            bool isRenewal = policy.IsRenewed;

            var pricingDto = new VIMS.Application.DTOs.CalculateQuoteDTO
            {
                InvoiceAmount = policy.InvoiceAmount,
                ManufactureYear = vehicle.Year,
                FuelType = vehicle.FuelType,
                VehicleType = vehicle.VehicleType,
                KilometersDriven = policy.InitialKilometersDriven,
                PolicyYears = policy.SelectedYears,
                PlanId = policy.PlanId
            };

            var breakdown = _pricingService.CalculateAnnualPremium(pricingDto, plan, isRenewal);

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
                                c.Item().Text($"Vehicle: {vehicle.Make} {vehicle.Model} ({vehicle.Year})");
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

                        col.Item().PaddingTop(0.5f, Unit.Centimetre).Text($"Insurance Plan: {plan.PlanName} ({plan.PolicyType})").SemiBold();

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn();
                                columns.ConstantColumn(120);
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

                            int rowNum = 1;

                            // 1. TP Component
                            if (breakdown.TPComponent > 0)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Third Party (TP) Liability Premium");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{breakdown.TPComponent:N2}");
                            }

                            // 2. OD Component
                            if (breakdown.ODComponent > 0)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Own Damage (OD) Premium");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{breakdown.ODComponent:N2}");
                            }

                            // 3. Risk Loading
                            if (breakdown.RiskLoadingAmount > 0)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Risk / Coverage Loading");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{breakdown.RiskLoadingAmount:N2}");
                            }

                            // 4. Discounts (Negative)
                            if (breakdown.DiscountAmount > 0)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Applied Discounts (Commitment/Loyalty)");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"- {breakdown.DiscountAmount:N2}");
                            }

                            // 5. Tax / GST
                            if (breakdown.TaxAmount > 0)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text("Goods and Services Tax (GST 18%)");
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{breakdown.TaxAmount:N2}");
                            }

                            static IContainer ItemCellStyle(IContainer container)
                            {
                                return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            }
                        });

                        col.Item().AlignRight().PaddingTop(10).Text(x =>
                        {
                            x.Span("Net Payable Amount: ").FontSize(12).SemiBold();
                            x.Span($"INR {totalPaidAmount:N2}").FontSize(12).SemiBold().FontColor(Colors.Blue.Medium);
                        });

                        col.Item().PaddingTop(2, Unit.Centimetre).Column(c => {
                            c.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(5).Text("Payment Summary").SemiBold();
                            c.Item().PaddingTop(5).Text($"Payment Method: {payment.PaymentMethod}");
                            c.Item().Text($"Transaction Reference: {payment.TransactionReference ?? "N/A"}");
                            c.Item().Text($"Payment Date: {payment.PaymentDate:dd-MMM-yyyy HH:mm}");
                            c.Item().Text($"Policy IDV: INR {policy.IDV:N2}");
                        });

                        col.Item().PaddingTop(1, Unit.Centimetre).AlignCenter().Text("Thank you for choosing VIMS Insurance! This is a system-generated invoice.").Italic().FontSize(9);
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

        public byte[] GenerateClaimSettlementPdf(int claimId)
        {
            var claim = _claimsRepository.GetByIdAsync(claimId).GetAwaiter().GetResult();
            if (claim == null || string.IsNullOrEmpty(claim.SettlementBreakdownJson)) return Array.Empty<byte>();

            var breakdown = JsonSerializer.Deserialize<ClaimBreakdownDTO>(claim.SettlementBreakdownJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (breakdown == null) return Array.Empty<byte>();

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
                            col.Item().Text("Settlement Assessment Report").FontSize(12).SemiBold().FontColor(Colors.Grey.Medium);
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("OFFICIAL SETTLEMENT").FontSize(16).SemiBold().FontColor(Colors.Green.Medium);
                            col.Item().Text($"Claim REF: {claim.ClaimNumber}").FontSize(10);
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Insured Individual:").SemiBold();
                                c.Item().Text(claim.Customer?.FullName ?? "N/A");
                                c.Item().Text($"Policy No: {claim.Policy?.PolicyNumber ?? "N/A"}");
if (claim.Policy?.Vehicle != null)
{
                                c.Item().Text($"Vehicle: {claim.Policy.Vehicle.Make} {claim.Policy.Vehicle.Model} ({claim.Policy.Vehicle.RegistrationNumber})");
}
                            });

                            row.RelativeItem().AlignRight().Column(c =>
                                                    {
                                                        c.Item().Text("Assessment Date:").SemiBold();
                                                        c.Item().Text(DateTime.UtcNow.ToString("dd-MMM-yyyy"));
                                                        c.Item().Text("Status:").SemiBold();
                                                        c.Item().Text("FINALIZED").FontColor(Colors.Green.Medium).SemiBold();
                                                    });
                        });

                        col.Item().PaddingTop(1, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().PaddingTop(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.ConstantColumn(120);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Assessment Item Description");
                                header.Cell().Element(CellStyle).AlignRight().Text("Amount (INR)");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                }
                            });

                            foreach (var item in breakdown.Items)
                            {
                                table.Cell().Element(ItemCellStyle).Column(c => {
                                    c.Item().Text(item.Label).SemiBold();
                                    if (!string.IsNullOrEmpty(item.Note))
                                        c.Item().Text(item.Note).FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                                });

                                var color = item.Status == "error" ? Colors.Red.Medium : (item.Status == "success" ? Colors.Green.Medium : Colors.Black);
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{item.Value:N2}").FontColor(color);
                            }

                            static IContainer ItemCellStyle(IContainer container)
                            {
                                return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            }
                        });

                        col.Item().AlignRight().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(10).Text(x =>
                        {
                            x.Span("Total Payout Approved: ").FontSize(14).SemiBold();
                            x.Span($"INR {breakdown.FinalPayout:N2}").FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);
                        });

                        // Key Terms Section
                        col.Item().PaddingTop(40).Column(c =>
                        {
                            c.Item().Text("Settlement Key Terms").FontSize(12).SemiBold().FontColor(Colors.Blue.Medium).Underline();
                            
                            c.Item().PaddingTop(10).Row(row => {
                                row.RelativeItem().Column(inner => {
                                    inner.Item().Text("• IDV (Insured Declared Value)").SemiBold();
                                    inner.Item().Text("The maximum sum assured which is provided in case of total loss.").FontSize(8);
                                });
                                row.RelativeItem().PaddingLeft(10).Column(inner => {
                                    inner.Item().Text("• Compulsory Deductible").SemiBold();
                                    inner.Item().Text("A fixed amount the insured pays towards each claim.").FontSize(8);
                                });
                            });

                            c.Item().PaddingTop(10).Row(row => {
                                row.RelativeItem().Column(inner => {
                                    inner.Item().Text("• Depreciation").SemiBold();
                                    inner.Item().Text("Reduction in value of vehicle parts over time.").FontSize(8);
                                });
                                row.RelativeItem().PaddingLeft(10).Column(inner => {
                                    inner.Item().Text("• Max Coverage Cap").SemiBold();
                                    inner.Item().Text("The upper limit of coverage defined by your plan.").FontSize(8);
                                });
                            });
                        });

                        col.Item().PaddingTop(2, Unit.Centimetre).AlignCenter().Text("© 2026 VIMS Insurance - System Generated Report").FontSize(8).Italic();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
