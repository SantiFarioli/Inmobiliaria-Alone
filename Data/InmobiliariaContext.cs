using Microsoft.EntityFrameworkCore;
using Inmobiliaria_Alone.Models;

namespace Inmobiliaria_Alone.Data
{
    public class InmobiliariaContext : DbContext
    {
        public InmobiliariaContext(DbContextOptions<InmobiliariaContext> options)
         : base(options)
        {

        }
        public DbSet<Propietario> Propietarios { get; set; }
        public DbSet<Inmueble> Inmuebles { get; set; }
        public DbSet<Inquilino> Inquilinos { get; set; }
        public DbSet<Contrato> Contratos { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de las relaciones
            modelBuilder
                .Entity<Inmueble>()
                .HasOne(i => i.Propietario)
                .WithMany(p => p.Inmuebles)
                .HasForeignKey(i => i.IdPropietario);

            modelBuilder
                .Entity<Contrato>()
                .HasOne(c => c.Inmueble)
                .WithMany(i => i.Contratos)
                .HasForeignKey(c => c.IdInmueble);

            modelBuilder
                .Entity<Contrato>()
                .HasOne(c => c.Inquilino)
                .WithMany(i => i.Contratos)
                .HasForeignKey(c => c.IdInquilino);

            modelBuilder
                .Entity<Pago>()
                .HasOne(p => p.Contrato)
                .WithMany(c => c.Pagos)
                .HasForeignKey(p => p.IdContrato);
        }
    }     
}
