using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inmobiliaria_Alone.Data;
using Inmobiliaria_Alone.Models;
using System.Security.Claims;

namespace Inmobiliaria_Alone.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InquilinosController : ControllerBase
    {
        private readonly MyDbContext _context;

        public InquilinosController(MyDbContext context)
        {
            _context = context;
        }

        //  GET api/inquilinos/mios
        //    Solo devuelve inquilinos de contratos del propietario
        [HttpGet("mios")]
        public async Task<ActionResult<IEnumerable<Inquilino>>> GetInquilinosMios()
        {
            var idProp = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var inquilinos = await _context.Contratos
                .Include(c => c.Inquilino)
                .Include(c => c.Inmueble)
                .Where(c => c.Inmueble != null &&
                            c.Inmueble.IdPropietario == idProp)
                .Select(c => c.Inquilino!)
                .Distinct()
                .ToListAsync();

            return Ok(inquilinos);
        }
    }
}
