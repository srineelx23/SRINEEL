using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using VIMS.Application.DTOs;
using System.Linq;

namespace VIMS.Infrastructure.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPricingService _pricingService;
        private readonly IClaimsRepository _claimsRepository;
        private readonly IPolicyTransferRepository _transferRepository;
        private readonly IPolicyRepository _policyRepository;

        // Modern UI Colors
        private const string PrimaryColor = "#1A237E"; // Deep Indigo
        private const string AccentColor = "#00ACC1";  // Cyan
        private const string SuccessColor = "#2E7D32"; // Green
        private const string WarningColor = "#F57C00"; // Orange
        private const string ErrorColor = "#C62828";   // Red
        private const string MutedColor = "#757575";   // Grey

        public InvoiceService(
            IPaymentRepository paymentRepository, 
            IPricingService pricingService, 
            IClaimsRepository claimsRepository,
            IPolicyTransferRepository transferRepository,
            IPolicyRepository policyRepository)
        {
            _paymentRepository = paymentRepository;
            _pricingService = pricingService;
            _claimsRepository = claimsRepository;
            _transferRepository = transferRepository;
            _policyRepository = policyRepository;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        #region Helper Styles
        private void ComposeHeader(IContainer container, string title, string? subTitle = null, string? reference = null)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("VIMS").FontSize(28).SemiBold().FontColor(PrimaryColor).LetterSpacing(0.05f);
                    col.Item().Text("VEHICLE INSURANCE MANAGEMENT").FontSize(9).SemiBold().FontColor(MutedColor);
                    if (!string.IsNullOrEmpty(subTitle))
                        col.Item().PaddingTop(10).Text(subTitle).FontSize(14).SemiBold().FontColor(AccentColor);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(title).FontSize(24).SemiBold().FontColor(MutedColor);
                    if (!string.IsNullOrEmpty(reference))
                        col.Item().Text(reference).FontSize(10).FontColor(PrimaryColor);
                    col.Item().Text(DateTime.Now.ToString("dd MMM yyyy")).FontSize(10).FontColor(MutedColor);
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Column(col =>
            {
                col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                col.Item().PaddingTop(5).Text(x =>
                {
                    x.Span("Page ").FontSize(9).FontColor(MutedColor);
                    x.CurrentPageNumber().FontSize(9).FontColor(MutedColor);
                    x.Span(" of ").FontSize(9).FontColor(MutedColor);
                    x.TotalPages().FontSize(9).FontColor(MutedColor);
                });
                col.Item().Text("This is an electronically generated document. No signature is required.").FontSize(8).Italic().FontColor(Colors.Grey.Medium);
            });
        }

        private void ComposeTableStyle(IContainer container)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
        }

        private IContainer CellStyle(IContainer container)
        {
            return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(8).BorderBottom(1).BorderColor(PrimaryColor);
        }

        private IContainer ItemCellStyle(IContainer container)
        {
            return container.PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
        }
        #endregion

        public byte[] GenerateInvoicePdf(int paymentId)
        {
            var payment = _paymentRepository.GetByIdWithDetailsAsync(paymentId).GetAwaiter().GetResult();
            if (payment == null) return Array.Empty<byte>();

            // If this is a Transfer Fee, generate the Transfer Certificate instead
            bool isTransfer = string.Equals(payment.TransactionReference, "Transfer Fee", StringComparison.OrdinalIgnoreCase) || 
                              string.Equals(payment.TransactionReference, "Transfer Fees", StringComparison.OrdinalIgnoreCase);

            if (isTransfer)
            {
                // Try finding the transfer via NewVehicleApplicationId
                var vehicleAppId = payment.Policy?.Vehicle?.VehicleApplicationId;
                if (vehicleAppId.HasValue)
                {
                    var transfers = _transferRepository.GetByNewVehicleApplicationIdAsync(vehicleAppId.Value).GetAwaiter().GetResult();
                    var transfer = transfers.FirstOrDefault();
                    if (transfer != null)
                    {
                        return GenerateTransferReportPdf(transfer.PolicyTransferId);
                    }
                }

                // Fallback: If for some reason we can't find it via AppId, try finding by Recipient Customer + Amount
                // Since this is a specialized service, we try to be helpful
                var allTransfers = _transferRepository.GetAllAsync().GetAwaiter().GetResult();
                var fallbackTransfer = allTransfers.FirstOrDefault(t => t.RecipientCustomerId == payment.Policy.CustomerId && t.Status == VIMS.Domain.Enums.PolicyTransferStatus.Completed);
                if (fallbackTransfer != null)
                {
                    return GenerateTransferReportPdf(fallbackTransfer.PolicyTransferId);
                }
            }

            var policy = payment.Policy;
            var plan = policy.Plan;
            var vehicle = policy.Vehicle;
            var customer = policy.Customer;

            var pricingDto = new CalculateQuoteDTO
            {
                InvoiceAmount = policy.InvoiceAmount,
                ManufactureYear = vehicle.Year,
                FuelType = vehicle.FuelType,
                VehicleType = vehicle.VehicleType,
                KilometersDriven = policy.InitialKilometersDriven,
                PolicyYears = policy.SelectedYears,
                PlanId = policy.PlanId
            };

            var breakdown = _pricingService.CalculateAnnualPremium(pricingDto, plan, policy.IsRenewed);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                    page.Header().Element(c => ComposeHeader(c, "INVOICE", plan.PlanName, $"#INV-{payment.PaymentId}"));

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // Customer & Vehicle Info Grid
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("BILL TO").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(2).Text(customer.FullName).FontSize(12).SemiBold();
                                c.Item().Text(customer.Email).FontSize(10).FontColor(MutedColor);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("VEHICLE DETAILS").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(2).Text($"{vehicle.Make} {vehicle.Model}").FontSize(11).SemiBold();
                                c.Item().Text($"Reg: {vehicle.RegistrationNumber} ({vehicle.Year})").FontSize(10).FontColor(MutedColor);
                            });
                        });

                        col.Item().PaddingTop(30).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.RelativeColumn();
                                columns.ConstantColumn(120);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("NO.");
                                header.Cell().Element(CellStyle).Text("DESCRIPTION");
                                header.Cell().Element(CellStyle).AlignRight().Text("AMOUNT (INR)");
                            });

                            int rowNum = 1;
                            void AddRow(string desc, decimal amt, bool isNegative = false)
                            {
                                table.Cell().Element(ItemCellStyle).Text(rowNum++.ToString());
                                table.Cell().Element(ItemCellStyle).Text(desc);
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{(isNegative ? "-" : "")}{amt:N2}");
                            }

                            if (breakdown.TPComponent > 0) AddRow("Third Party Liability Coverage", breakdown.TPComponent);
                            if (breakdown.ODComponent > 0) AddRow("Own Damage Coverage", breakdown.ODComponent);
                            if (breakdown.RiskLoadingAmount > 0) AddRow("Risk & Add-on Loadings", breakdown.RiskLoadingAmount);
                            if (breakdown.DiscountAmount > 0) AddRow("Promotional / Loyalty Discount", breakdown.DiscountAmount, true);
                            if (breakdown.TaxAmount > 0) AddRow("GST", breakdown.TaxAmount);
                        });

                        col.Item().PaddingTop(20).AlignRight().Background(Colors.Grey.Lighten4).Padding(10).Row(row =>
                        {
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("TOTAL AMOUNT PAID").FontSize(10).SemiBold().FontColor(MutedColor);
                                c.Item().Text($"INR {payment.Amount:N2}").FontSize(18).SemiBold().FontColor(PrimaryColor);
                                c.Item().Text("Transaction Success").FontSize(9).SemiBold().FontColor(SuccessColor);
                            });
                        });

                        col.Item().PaddingTop(40).Column(c =>
                        {
                            c.Item().Text("POLICY SUMMARY").FontSize(11).SemiBold().FontColor(PrimaryColor);
                            c.Item().PaddingTop(5).LineHorizontal(0.5f).LineColor(Colors.Indigo.Lighten4);
                            c.Item().PaddingTop(10).Row(r =>
                            {
                                r.RelativeItem().Text(t => { t.Span("Policy IDV: ").SemiBold(); t.Span($"INR {policy.IDV:N2}"); });
                                r.RelativeItem().Text(t => { t.Span("Duration: ").SemiBold(); t.Span($"{policy.SelectedYears} Year(s)"); });
                            });
                            c.Item().PaddingTop(5).Row(r =>
                            {
                                r.RelativeItem().Text(t => { t.Span("Start Date: ").SemiBold(); t.Span(policy.StartDate.ToString("dd MMM yyyy")); });
                                r.RelativeItem().Text(t => { t.Span("End Date: ").SemiBold(); t.Span(policy.EndDate.ToString("dd MMM yyyy")); });
                            });
                        });
                    });

                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateClaimSettlementPdf(int claimId)
        {
            var claim = _claimsRepository.GetByIdAsync(claimId).GetAwaiter().GetResult();
            if (claim == null || string.IsNullOrEmpty(claim.SettlementBreakdownJson)) return Array.Empty<byte>();

            ClaimBreakdownDTO? breakdown = null;
            try { breakdown = JsonSerializer.Deserialize<ClaimBreakdownDTO>(claim.SettlementBreakdownJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { return Array.Empty<byte>(); }

            if (breakdown == null) return Array.Empty<byte>();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                    page.Header().Element(c => ComposeHeader(c, "SETTLEMENT REPORT", "Claim Assessment", $"REF: {claim.ClaimNumber}"));

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("INSURED PARTY").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(2).Text(claim.Customer?.FullName ?? "N/A").FontSize(12).SemiBold();
                                c.Item().Text($"Policy: {claim.Policy?.PolicyNumber ?? "N/A"}").FontSize(10).FontColor(MutedColor);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("LOSS ASSESSMENT").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(2).Text(claim.claimType.ToString()).FontSize(11).SemiBold();
                                c.Item().Text($"Status: {claim.DecisionType ?? "Approved"}").FontSize(10).FontColor(SuccessColor).SemiBold();
                            });
                        });

                        col.Item().PaddingTop(30).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.ConstantColumn(120);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("ASSESSMENT ITEM");
                                header.Cell().Element(CellStyle).AlignRight().Text("AMOUNT (INR)");
                            });

                            foreach (var item in breakdown.Items)
                            {
                                var color = item.Status == "error" ? ErrorColor : (item.Status == "success" ? SuccessColor : "#000000");
                                table.Cell().Element(ItemCellStyle).Column(c => {
                                    c.Item().Text(item.Label).SemiBold();
                                    if (!string.IsNullOrEmpty(item.Note)) c.Item().Text(item.Note).FontSize(8).Italic().FontColor(MutedColor);
                                });
                                table.Cell().Element(ItemCellStyle).AlignRight().Text($"{item.Value:N2}").FontColor(color);
                            }
                        });

                        col.Item().PaddingTop(20).AlignRight().Background(Colors.Indigo.Lighten5).Padding(10).Column(c =>
                        {
                            c.Item().Text("TOTAL APPROVED PAYOUT").FontSize(10).SemiBold().FontColor(PrimaryColor);
                            c.Item().Text($"INR {breakdown.FinalPayout:N2}").FontSize(20).SemiBold().FontColor(PrimaryColor);
                        });

                        col.Item().PaddingTop(40).Column(c =>
                        {
                            c.Item().Text("DISCLAIMER & TERMS").FontSize(11).SemiBold().FontColor(PrimaryColor);
                            c.Item().PaddingTop(5).LineHorizontal(0.5f).LineColor(Colors.Indigo.Lighten4);
                            c.Item().PaddingTop(10).Text("• This settlement is calculated based on the policy terms, applicable depreciation, and compulsory deductibles.").FontSize(8).FontColor(MutedColor);
                            c.Item().Text("• IDV represents the maximum liability of the insurer at the time of claim.").FontSize(8).FontColor(MutedColor);
                        });
                    });

                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateTransferReportPdf(int transferId)
        {
            var transfer = _transferRepository.GetByIdAsync(transferId).GetAwaiter().GetResult();
            if (transfer == null) return Array.Empty<byte>();

            var policy = transfer.Policy;
            var sender = transfer.SenderCustomer;
            var recipient = transfer.RecipientCustomer;
            var vehicle = policy.Vehicle;
            var plan = policy.Plan;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                    page.Header().Element(c => ComposeHeader(c, "TRANSFER CERTIFICATE", "Insurance Ownership Transfer", $"#TRF-{transfer.PolicyTransferId}"));

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // Summary Card
                        col.Item().Border(1).BorderColor(PrimaryColor).Background(Colors.Indigo.Lighten5).Padding(15).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("TRANSFER STATUS").FontSize(9).SemiBold().FontColor(PrimaryColor);
                                c.Item().Text(transfer.Status.ToString()).FontSize(14).SemiBold().FontColor(SuccessColor);
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("POLICY NO").FontSize(9).SemiBold().FontColor(PrimaryColor);
                                c.Item().Text(policy.PolicyNumber).FontSize(14).SemiBold().FontColor(PrimaryColor);
                            });
                        });

                        // Participants
                        col.Item().PaddingTop(30).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("ORIGINAL OWNER (SENDER)").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(5).Text(sender.FullName).FontSize(11).SemiBold();
                                c.Item().Text(sender.Email).FontSize(9).FontColor(MutedColor);
                            });

                            row.ConstantColumn(40).AlignCenter().PaddingTop(10).Text("→").FontSize(20).FontColor(AccentColor);

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("NEW OWNER (RECIPIENT)").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(5).Text(recipient.FullName).FontSize(11).SemiBold();
                                c.Item().Text(recipient.Email).FontSize(9).FontColor(MutedColor);
                            });
                        });

                        // Vehicle Assets
                        col.Item().PaddingTop(40).Column(c =>
                        {
                            c.Item().Text("TRANSFERRED ASSETS").FontSize(11).SemiBold().FontColor(PrimaryColor);
                            c.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);
                            
                            c.Item().PaddingTop(15).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                void AddDetail(string label, string value)
                                {
                                    table.Cell().PaddingBottom(10).Column(inner =>
                                    {
                                        inner.Item().Text(label).FontSize(8).SemiBold().FontColor(MutedColor);
                                        inner.Item().Text(value).FontSize(10).SemiBold();
                                    });
                                }

                                AddDetail("Vehicle Brand/Model", $"{vehicle.Make} {vehicle.Model}");
                                AddDetail("Registration Number", vehicle.RegistrationNumber);
                                AddDetail("Manufacturing Year", vehicle.Year.ToString());
                                AddDetail("Fuel Type", vehicle.FuelType ?? "N/A");
                                AddDetail("Insurance Plan", plan.PlanName);
                                AddDetail("Insured Declared Value", $"INR {policy.IDV:N2}");
                                AddDetail("Coverage End Date", policy.EndDate.ToString("dd MMM yyyy"));
                                AddDetail("Policy Status", "TRANSFERRED (ACTIVE)");
                            });
                        });

                        // Financial Note
                        col.Item().PaddingTop(30).Background(Colors.Grey.Lighten4).Padding(10).Text(t =>
                        {
                            t.Span("Note: ").SemiBold().FontColor(PrimaryColor);
                            t.Span("This certificate confirms the legal transfer of policy rights and vehicle insurance obligations from the sender to the recipient. The accumulated premium and coverage benefits remain active under the new ownership.").FontSize(9);
                        });
                    });

                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GeneratePolicyContractPdf(int policyId)
        {
            var policy = _policyRepository.GetByIdAsync(policyId).GetAwaiter().GetResult();
            if (policy == null) return Array.Empty<byte>();

            var customer = policy.Customer;
            var vehicle = policy.Vehicle;
            var plan = policy.Plan;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                    page.Header().Element(c => ComposeHeader(c, "POLICY CONTRACT", plan.PlanName, $"#POL-{policy.PolicyNumber}"));

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // Main Certificate Box
                        col.Item().Border(1).BorderColor(PrimaryColor).Background(Colors.Indigo.Lighten5).Padding(15).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("POLICY STATUS").FontSize(9).SemiBold().FontColor(PrimaryColor);
                                c.Item().Text(policy.Status.ToString().ToUpper()).FontSize(14).SemiBold().FontColor(SuccessColor);
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text("VALID UNTIL").FontSize(9).SemiBold().FontColor(PrimaryColor);
                                c.Item().Text(policy.EndDate.ToString("dd MMM yyyy")).FontSize(14).SemiBold().FontColor(PrimaryColor);
                            });
                        });

                        // Participants
                        col.Item().PaddingTop(30).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("INSURED PERSON DETAILS").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(5).Text(customer.FullName).FontSize(11).SemiBold();
                                c.Item().Text(customer.Email).FontSize(9).FontColor(MutedColor);
                                c.Item().PaddingTop(10).Text("COVERAGE PERIOD").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().Text($"{policy.StartDate:dd MMM yyyy} to {policy.EndDate:dd MMM yyyy}").FontSize(10).SemiBold();
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("INSURED VEHICLE").FontSize(9).SemiBold().FontColor(MutedColor);
                                c.Item().PaddingTop(5).Text($"{vehicle.Make} {vehicle.Model}").FontSize(11).SemiBold();
                                c.Item().Text($"Reg: {vehicle.RegistrationNumber}").FontSize(10).SemiBold();
                                c.Item().Text($"Year: {vehicle.Year}").FontSize(9).FontColor(MutedColor);
                                c.Item().Text($"Fuel: {vehicle.FuelType}").FontSize(9).FontColor(MutedColor);
                            });
                        });

                        // Coverage Table
                        col.Item().PaddingTop(40).Column(c =>
                        {
                            c.Item().Text("COVERAGE & BENEFITS").FontSize(11).SemiBold().FontColor(PrimaryColor);
                            c.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);
                            
                            c.Item().PaddingTop(15).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                void AddDetail(string label, string value, bool isActive = true)
                                {
                                    table.Cell().PaddingBottom(10).Column(inner =>
                                    {
                                        inner.Item().Text(label).FontSize(8).SemiBold().FontColor(MutedColor);
                                        inner.Item().Row(r => {
                                            r.RelativeItem().Text(value).FontSize(10).SemiBold().FontColor(isActive ? Colors.Black : Colors.Grey.Medium);
                                            if (isActive) r.ConstantColumn(15).PaddingTop(2).Text("✔").FontSize(8).FontColor(SuccessColor);
                                        });
                                    });
                                }

                                AddDetail("Plan Name", plan.PlanName);
                                AddDetail("Plan Type", plan.PolicyType);
                                AddDetail("Insured Declared Value (IDV)", $"INR {policy.IDV:N2}");
                                AddDetail("Policy Duration", $"{policy.SelectedYears} Year(s)");
                                
                                // Specific features
                                AddDetail("Third Party Liability", plan.CoversThirdParty ? "Included" : "Not Covered", plan.CoversThirdParty);
                                AddDetail("Own Damage Coverage", plan.CoversOwnDamage ? "Included" : "Not Covered", plan.CoversOwnDamage);
                                AddDetail("Theft Protection", plan.CoversTheft ? "Included" : "Not Covered", plan.CoversTheft);
                                AddDetail("Zero Depreciation", plan.ZeroDepreciationAvailable ? "Available" : "Not Included", plan.ZeroDepreciationAvailable);
                                AddDetail("Roadside Assistance", plan.RoadsideAssistanceAvailable ? "Available" : "Not Included", plan.RoadsideAssistanceAvailable);
                                AddDetail("Deductible Amount", $"INR {plan.DeductibleAmount:N2}");
                            });
                        });

                        // Legal Note
                        col.Item().PaddingTop(30).Background(Colors.Grey.Lighten4).Padding(10).Column(inner =>
                        {
                            inner.Item().Text("TERMS AND CONDITIONS SUMMARY").FontSize(9).SemiBold().FontColor(PrimaryColor);
                            inner.Item().PaddingTop(5).Text("This policy contract signifies a legal agreement between the insurer and the insured. The coverage is subject to the conditions mentioned in the full policy handbook. Maintenance of accurate vehicle records and timely premium payments are required to keep the policy active. (Note: This document is free of GST charges as per current insurance regulations).").FontSize(8).FontColor(MutedColor).LineHeight(1.4f);
                        });
                    });

                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }
    }
}
