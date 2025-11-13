using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inmobiliaria_Alone.Models;
using Inmobiliaria_Alone.Data;
using System.Security.Claims;

namespace Inmobiliaria_Alone.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PagosController : ControllerBase
    {
        private readonly MyDbContext _context;

        public PagosController(MyDbContext context)
        {
            _context = context;
        }

        // ✔ GET api/pagos/por-contrato/123

        [HttpGet("por-contrato/{idContrato:int}")]
        public async Task<IActionResult> PorContrato(int idContrato)
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Validar que el contrato sea del propietario
            var esMio = await _context.Contratos
                .Include(c => c.Inmueble)
                .AnyAsync(c =>
                    c.IdContrato == idContrato &&
                    c.Inmueble != null &&
                    c.Inmueble.IdPropietario == idProp);

            if (!esMio)
                return Forbid("Ese contrato no te pertenece.");

            var pagos = await _context.Pagos
                .Where(p => p.IdContrato == idContrato)
                .OrderByDescending(p => p.FechaPago)
                .ToListAsync();

            return Ok(pagos);
        }
    }
}
