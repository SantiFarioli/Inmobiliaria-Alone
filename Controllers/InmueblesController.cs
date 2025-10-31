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

        [HttpGet("{id}")]
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
        [HttpPost]
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
                //  Obtener el ID del propietario desde el token JWT
                var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                //  Buscar el propietario en la base de datos
                var propietario = await _context.Propietarios.FirstOrDefaultAsync(p => p.IdPropietario == idProp);
                if (propietario == null)
                    return Unauthorized("No se encontró el propietario asociado al token.");

                // Validar datos del inmueble
                var body = JsonSerializer.Deserialize<Inmueble>(inmueble);
                if (body == null)
                    return BadRequest("Datos de inmueble inválidos.");

                if (imagen == null || imagen.Length == 0)
                    return BadRequest("Debe enviar una imagen válida.");

                //  Guardar imagen en carpeta /uploads/inmuebles
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inmuebles");
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imagen.FileName);
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imagen.CopyToAsync(stream);
                }

                // Completar datos del inmueble
                body.Foto = "/uploads/inmuebles/" + fileName;
                body.IdPropietario = propietario.IdPropietario;
                if (string.IsNullOrWhiteSpace(body.Estado))
                    body.Estado = "No disponible"; // valor por defecto

                // Guardar inmueble en DB
                _context.Inmuebles.Add(body);
                await _context.SaveChangesAsync();

                return Ok(body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en CargarInmueble: {ex.Message}");
                return StatusCode(500, "Error interno al crear el inmueble.");
            }
        }

    

    }
}
