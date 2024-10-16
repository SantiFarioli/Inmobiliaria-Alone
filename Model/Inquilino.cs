using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Inmobiliaria_Alone.Models;
    public class Inquilino
    {
        [Key]
        public int IdInquilino { get; set; }
        public string Dni { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string LugarTrabajo { get; set; } = string.Empty;
        public string NombreGarante { get; set; } = string.Empty;
        public string DniGarante { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public ICollection<Contrato> Contratos { get; set; } = new List<Contrato>();
    }

