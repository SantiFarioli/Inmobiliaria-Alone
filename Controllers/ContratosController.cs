using System.Security.Claims;
using Inmobiliaria_Alone.Data;
using Inmobiliaria_Alone.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inmobiliaria_Alone.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ContratosController : ControllerBase
    {
        private readonly MyDbContext _context;

        public ContratosController(MyDbContext context)
        {
            _context = context;
        }

        //   GET api/Contratos/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetContrato(int id)
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var contrato = await _context.Contratos
                .Include(c => c.Inquilino)
                .Include(c => c.Inmueble)
                .FirstOrDefaultAsync(c =>
                    c.IdContrato == id &&
                    c.Inmueble != null &&
                    c.Inmueble.IdPropietario == idProp
                );

            if (contrato == null)
                return NotFound("No existe o no te pertenece.");

            return Ok(contrato);
        }

        //   GET api/Contratos/vigentes/mios
        //  Lista solo los contratos vigentes del propietario autenticado
        [HttpGet("vigentes/mios")]
        public async Task<IActionResult> VigentesMios()
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var hoy = DateTime.Today;

            var contratos = await _context.Contratos
                .Include(c => c.Inquilino)
                .Include(c => c.Inmueble)
                .Where(c => c.Inmueble != null &&
                            c.Inmueble.IdPropietario == idProp &&
                            c.FechaInicio <= hoy &&
                            c.FechaFin >= hoy)
                .ToListAsync();

            return Ok(contratos);
        }
    }
}
