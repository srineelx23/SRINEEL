using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Application.Interfaces.Services
{
    public interface IInvoiceService
    {
        byte[] GenerateInvoicePdf(int paymentId);
        byte[] GenerateClaimSettlementPdf(int claimId);
    }
}
