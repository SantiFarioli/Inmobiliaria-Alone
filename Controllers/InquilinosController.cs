using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inmobiliaria_Alone.Models;
using Inmobiliaria_Alone.Data;

namespace Inmobiliaria_Alone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InquilinosController : ControllerBase
    {
        private readonly InmobiliariaContext _context;

        public InquilinosController(InmobiliariaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Inquilino>>> GetInquilinos()
        {
            return await _context.Inquilinos.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Inquilino>> GetInquilino(int id)
        {
            var inquilino = await _context.Inquilinos.FindAsync(id);

            if (inquilino == null)
            {
                return NotFound();
            }

            return inquilino;
        }

        [HttpPost]
        public async Task<ActionResult<Inquilino>> PostInquilino(Inquilino inquilino)
        {
            _context.Inquilinos.Add(inquilino);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetInquilino),
                new { id = inquilino.IdInquilino },
                inquilino
            );
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutInquilino(int id, Inquilino inquilino)
        {
            if (id != inquilino.IdInquilino)
            {
                return BadRequest();
            }

            _context.Entry(inquilino).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InquilinoExists(id))
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
        public async Task<IActionResult> DeleteInquilino(int id)
        {
            var inquilino = await _context.Inquilinos.FindAsync(id);
            if (inquilino == null)
            {
                return NotFound();
            }

            _context.Inquilinos.Remove(inquilino);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InquilinoExists(int id)
        {
            return _context.Inquilinos.Any(e => e.IdInquilino == id);
        }
    }
}
