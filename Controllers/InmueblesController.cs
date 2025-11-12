using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inmobiliaria_Alone.Models;
using Inmobiliaria_Alone.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace Inmobiliaria_Alone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InmueblesController : ControllerBase
    {
        private readonly MyDbContext _context;

        public InmueblesController(MyDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Inmueble>>> GetInmuebles()
        {
            return await _context.Inmuebles.ToListAsync();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Inmueble>> GetInmueble(int id)
        {
            var inmueble = await _context.Inmuebles.FindAsync(id);

            if (inmueble == null)
            {
                return NotFound();
            }

            return inmueble;
        }

        [Authorize]
        [HttpPost("crear")]
        public async Task<ActionResult<Inmueble>> PostInmueble([FromBody] Inmueble body)
        {
            var email = User.FindFirst(ClaimTypes.Name)?.Value;
            var p = await _context.Propietarios.FirstOrDefaultAsync(x => x.Email == email);
            if (p == null) return Unauthorized();

            body.IdPropietario = p.IdPropietario;
            if (string.IsNullOrWhiteSpace(body.Estado)) body.Estado = "Disponible";

            _context.Inmuebles.Add(body);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetInmueble), new { id = body.IdInmueble }, body);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutInmueble(int id, Inmueble inmueble)
        {
            if (id != inmueble.IdInmueble)
            {
                return BadRequest();
            }

            _context.Entry(inmueble).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InmuebleExists(id))
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
        public async Task<IActionResult> DeleteInmueble(int id)
        {
            var inmueble = await _context.Inmuebles.FindAsync(id);
            if (inmueble == null)
            {
                return NotFound();
            }

            _context.Inmuebles.Remove(inmueble);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool InmuebleExists(int id)
        {
            return _context.Inmuebles.Any(e => e.IdInmueble == id);
        }

        // PATCH api/Inmuebles/5/disponibilidad   body: true/false
        [HttpPatch("{idInmueble:int}/disponibilidad")]
        public async Task<IActionResult> CambiarDisponibilidad(int idInmueble, [FromBody] bool disponible)
        {
            var propietarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var inm = await _context.Inmuebles
                .FirstOrDefaultAsync(i => i.IdInmueble == idInmueble && i.IdPropietario == propietarioId);
            if (inm == null) return NotFound();

            inm.Estado = disponible ? "Disponible" : "Ocupado"; // o "No disponible"
            await _context.SaveChangesAsync();
            return Ok(inm);
        }

        [Authorize]
        [HttpPost("cargar")]
        public async Task<IActionResult> CargarInmueble([FromForm] IFormFile imagen, [FromForm] string inmueble)
        {
            try
            {
                var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var propietario = await _context.Propietarios.FirstOrDefaultAsync(p => p.IdPropietario == idProp);
                if (propietario == null)
                    return Unauthorized("No se encontró el propietario asociado al token.");

                if (string.IsNullOrWhiteSpace(inmueble))
                    return BadRequest("No se recibió el JSON del inmueble.");

                Inmueble body;
                try
                {
                    body = JsonSerializer.Deserialize<Inmueble>(inmueble, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })!;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializando inmueble: {ex.Message}");
                    return BadRequest("Formato JSON inválido.");
                }

                if (body == null)
                    return BadRequest("El cuerpo del inmueble está vacío.");

                if (imagen == null || imagen.Length == 0)
                    return BadRequest("Debe enviar una imagen válida.");

                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inmuebles");
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                var fileName = Guid.NewGuid() + Path.GetExtension(imagen.FileName);
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imagen.CopyToAsync(stream);
                }

                // URL pública absoluta
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var fotoUrl = $"{baseUrl}/uploads/inmuebles/{fileName}";

                body.IdPropietario = propietario.IdPropietario;
                body.Foto = fotoUrl;
                if (string.IsNullOrWhiteSpace(body.Estado))
                    body.Estado = "No disponible";

                _context.Inmuebles.Add(body);
                await _context.SaveChangesAsync();

                // Devolver inmueble ya con la URL absoluta
                return Ok(body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔥 Error en CargarInmueble: {ex.Message}");
                return StatusCode(500, "Error interno al crear el inmueble.");
            }
        }

        // GET api/inmuebles/mios
        [Authorize]
        [HttpGet("mios")]
        public async Task<ActionResult<IEnumerable<Inmueble>>> GetMisInmuebles()
        {
            try
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim == null)
                {
                    Console.WriteLine("⚠️ No se encontró el Claim NameIdentifier");
                    return Unauthorized("Token inválido o mal formado.");
                }

                var idProp = int.Parse(claim.Value);
                Console.WriteLine($"✅ Claim NameIdentifier detectado: {idProp}");

                var inmuebles = await _context.Inmuebles
                    .Where(i => i.IdPropietario == idProp)
                    .ToListAsync();

                Console.WriteLine($"✅ Se encontraron {inmuebles.Count} inmuebles del propietario {idProp}");
                return Ok(inmuebles);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔥 Error en GetMisInmuebles: {ex.Message}");
                return StatusCode(500, "Error interno al obtener los inmuebles del propietario.");
            }
        }

    }
}
