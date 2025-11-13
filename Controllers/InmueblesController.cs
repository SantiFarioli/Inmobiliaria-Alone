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
    public class InmueblesController : ControllerBase
    {
        private readonly MyDbContext _context;

        public InmueblesController(MyDbContext context)
        {
            _context = context;
        }

        //   GET api/inmuebles/5 (solo si te pertenece)

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Inmueble>> GetInmueble(int id)
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var inmueble = await _context.Inmuebles
                .FirstOrDefaultAsync(i => i.IdInmueble == id && i.IdPropietario == idProp);

            return inmueble == null
                ? NotFound("No existe o no te pertenece.")
                : Ok(inmueble);
        }

        //   POST api/inmuebles/crear (crear inmueble SIN ID de propietario)
        [HttpPost("crear")]
        public async Task<ActionResult<Inmueble>> PostInmueble([FromBody] Inmueble body)
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            body.IdPropietario = idProp;
            if (string.IsNullOrWhiteSpace(body.Estado))
                body.Estado = "Disponible";

            _context.Inmuebles.Add(body);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInmueble), new { id = body.IdInmueble }, body);
        }

        //   PATCH api/inmuebles/5/disponibilidad  (habilitar/deshabilitar)
        [HttpPatch("{idInmueble:int}/disponibilidad")]
        public async Task<IActionResult> CambiarDisponibilidad(int idInmueble, [FromBody] bool disponible)
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var inmueble = await _context.Inmuebles
                .FirstOrDefaultAsync(i =>
                    i.IdInmueble == idInmueble &&
                    i.IdPropietario == idProp);

            if (inmueble == null)
                return NotFound("No existe o no te pertenece.");

            inmueble.Estado = disponible ? "Disponible" : "No disponible";

            await _context.SaveChangesAsync();
            return Ok(inmueble);
        }

        //   GET api/inmuebles/mios  (lista solo del propietario)
        [HttpGet("mios")]
        public async Task<ActionResult<IEnumerable<Inmueble>>> GetMisInmuebles()
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var inmuebles = await _context.Inmuebles
                .Where(i => i.IdPropietario == idProp)
                .ToListAsync();

            return Ok(inmuebles);
        }
    }
}
