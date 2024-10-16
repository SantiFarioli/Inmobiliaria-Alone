using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Inmobiliaria_Alone.Models;
    public class Propietario
    {
        [Key]
        public int IdPropietario { get; set; }
        public string Dni { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Nueva propiedad
        public ICollection<Inmueble> Inmuebles { get; set; } = new List<Inmueble>();
    }

