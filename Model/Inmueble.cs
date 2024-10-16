using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inmobiliaria_Alone.Models;

    public class Inmueble
    {
        [Key]
        public int IdInmueble { get; set; }
        public string Direccion { get; set; } = string.Empty;
        public string Uso { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int Ambientes { get; set; }
        public decimal Precio { get; set; }
        public string Estado { get; set; } = string.Empty;

        [ForeignKey("Propietario")]
        public int IdPropietario { get; set; }
        public Propietario? Propietario { get; set; }
        public ICollection<Contrato> Contratos { get; set; } = new List<Contrato>();
    }

