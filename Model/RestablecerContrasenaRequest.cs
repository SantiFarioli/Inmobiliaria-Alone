using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inmobiliaria_Alone.Models
{
    public class RestablecerContrasenaRequest
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NuevaContrasena { get; set; } = string.Empty;
    }
}
