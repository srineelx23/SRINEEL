using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Domain.Enums
{
    public enum PolicyStatus
    {
        Draft=0,
        PendingPayment=1,
        Active=2,
        Claimed=3,
        Expired=4,
        Cancelled=5
    }
}
