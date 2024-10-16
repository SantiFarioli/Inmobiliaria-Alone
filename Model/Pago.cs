using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inmobiliaria_Alone.Models;

    public class Pago
    {
        [Key]
        public int IdPago { get; set; }

        [ForeignKey("Contrato")]
        public int IdContrato { get; set; }
        public Contrato? Contrato { get; set; }

        public int NumeroPago { get; set; }
        public DateTime FechaPago { get; set; }
        public decimal Importe { get; set; }
    }
