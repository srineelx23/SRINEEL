using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VIMS.Application.DTOs
{
    public class AuthResultDTO
    {
        public string token { get; set; }
        public string name { get; set; }
        public string Role { get; set; }
        public bool IsSecurityQuestionSet { get; set; }
    }
}

