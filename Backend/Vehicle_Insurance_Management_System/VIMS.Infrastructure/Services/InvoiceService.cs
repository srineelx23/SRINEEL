using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using System;

namespace VIMS.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPricingService _pricingService;

        public InvoiceService(IPaymentRepository paymentRepository, IPricingService pricingService)
        {
            _paymentRepository = paymentRepository;
            _pricingService = pricingService;
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
    }
}
