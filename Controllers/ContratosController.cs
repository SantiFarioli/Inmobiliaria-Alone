using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inmobiliaria_Alone.Data;
using Inmobiliaria_Alone.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;


namespace Inmobiliaria_Alone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContratosController : ControllerBase
    {
        private readonly MyDbContext _context;

        public ContratosController(MyDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Contrato>>> GetContratos()
        {
            return await _context.Contratos.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Contrato>> GetContrato(int id)
        {
            var contrato = await _context.Contratos.FindAsync(id);

            if (contrato == null)
            {
                return NotFound();
            }

            return contrato;
        }

        [HttpPost]
        public async Task<ActionResult<Contrato>> PostContrato(Contrato contrato)
        {
            _context.Contratos.Add(contrato);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetContrato), new { id = contrato.IdContrato }, contrato);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutContrato(int id, Contrato contrato)
        {
            if (id != contrato.IdContrato)
            {
                return BadRequest();
            }

            _context.Entry(contrato).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ContratoExists(id))
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
        public async Task<IActionResult> DeleteContrato(int id)
        {
            var contrato = await _context.Contratos.FindAsync(id);
            if (contrato == null)
            {
                return NotFound();
            }

            _context.Contratos.Remove(contrato);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("vigentes/mios")]
        public async Task<IActionResult> VigentesMios()
        {
            var propietarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var hoy = DateTime.Today;

            var contratos = await _context.Contratos
                .Include(c => c.Inquilino)
                .Include(c => c.Inmueble)
                .Where(c => c.Inmueble != null
                        && c.Inmueble.IdPropietario == propietarioId
                        && c.FechaInicio <= hoy
                        && c.FechaFin >= hoy)
                .ToListAsync();

            return Ok(contratos);
        }

        private bool ContratoExists(int id) =>
            _context.Contratos.Any(e => e.IdContrato == id);
        }
}
