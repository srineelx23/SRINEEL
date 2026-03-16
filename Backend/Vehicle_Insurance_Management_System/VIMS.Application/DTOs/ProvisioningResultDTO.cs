using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.DTOs
{
    public class ProvisioningResultDTO
    {
        public User User { get; set; }
        public string WebhookResponse { get; set; }
    }
}
