using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inmobiliaria_Alone.Models;
using Inmobiliaria_Alone.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Inmobiliaria_Alone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PagosController : ControllerBase
    {
        private readonly MyDbContext _context;

        public PagosController(MyDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Pago>>> GetPagos()
        {
            return await _context.Pagos.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Pago>> GetPago(int id)
        {
            var pago = await _context.Pagos.FindAsync(id);

            if (pago == null)
            {
                return NotFound();
            }

            return pago;
        }

        [HttpPost]
        public async Task<ActionResult<Pago>> PostPago(Pago pago)
        {
            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPago), new { id = pago.IdPago }, pago);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPago(int id, Pago pago)
        {
            if (id != pago.IdPago)
            {
                return BadRequest();
            }

            _context.Entry(pago).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PagoExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePago(int id)
        {
            var pago = await _context.Pagos.FindAsync(id);
            if (pago == null)
            {
                return NotFound();
            }

            _context.Pagos.Remove(pago);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PagoExists(int id)
        {
            return _context.Pagos.Any(e => e.IdPago == id);
        }

        // GET api/Pagos/por-contrato/123
        [HttpGet("por-contrato/{idContrato:int}")]
        public async Task<IActionResult> PorContrato(int idContrato)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var propietarioId))
                return Unauthorized();

            var esMio = await _context.Contratos
                .Include(c => c.Inmueble)
                .AnyAsync(c => c.IdContrato == idContrato
                            && c.Inmueble != null
                            && c.Inmueble.IdPropietario == propietarioId);

            if (!esMio) return Forbid();

            var pagos = await _context.Pagos
                .Where(p => p.IdContrato == idContrato)
                .OrderByDescending(p => p.FechaPago)
                .ToListAsync();

            return Ok(pagos);
        }

    }
}
