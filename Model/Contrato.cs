using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Inmobiliaria_Alone.Models;

public class Contrato
    {
        [Key]
        public int IdContrato { get; set; }

        [ForeignKey("Inmueble")]
        public int IdInmueble { get; set; }
        public Inmueble? Inmueble { get; set; }

        [ForeignKey("Inquilino")]
        public int IdInquilino { get; set; }
        public Inquilino? Inquilino { get; set; }

        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal MontoAlquiler { get; set; }
        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    }

