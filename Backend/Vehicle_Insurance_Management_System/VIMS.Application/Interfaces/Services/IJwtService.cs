using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Application.Interfaces.Services
{
    public interface IJwtService
    {
        string GenerateToken(User customer);
    }
}
