using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Inmobiliaria_Alone.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Inmobiliaria_Alone.Models;
using Microsoft.IdentityModel.Tokens;

namespace Inmobiliaria_Alone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PropietariosController : ControllerBase
    {
        private readonly InmobiliariaContext _context;
        private readonly IConfiguration _config;

        public PropietariosController(InmobiliariaContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Models.Propietario>>> GetPropietarios()
        {
            return await _context.Propietarios.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Propietario>> GetPropietario(int id)
        {
            var propietario = await _context.Propietarios.FindAsync(id);

            if (propietario == null)
            {
                return NotFound();
            }

            return propietario;
        }

        [HttpPost]
        public async Task<ActionResult<Propietario>> PostPropietario(
            [FromBody] Propietario propietario
        )
        {
            propietario.Password = HashPassword(propietario.Password);
            _context.Propietarios.Add(propietario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetPropietario),
                new { id = propietario.IdPropietario },
                propietario
            );
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPropietario(int id, Propietario propietario)
        {
            if (id != propietario.IdPropietario)
            {
                return BadRequest();
            }

            _context.Entry(propietario).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PropietarioExists(id))
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
        public async Task<IActionResult> DeletePropietario(int id)
        {
            var propietario = await _context.Propietarios.FindAsync(id);
            if (propietario == null)
            {
                return NotFound();
            }

            _context.Propietarios.Remove(propietario);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PropietarioExists(int id)
        {
            return _context.Propietarios.Any(e => e.IdPropietario == id);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromForm] LoginView loginView)
        {
            var propietario = await _context.Propietarios.FirstOrDefaultAsync(x =>
                x.Email == loginView.Usuario
            );
            if (
                propietario == null
                || loginView.Clave == null
                || !VerifyPassword(loginView.Clave, propietario.Password)
            )
            {
                return BadRequest("Nombre de usuario o clave incorrecta");
            }

            var token = GenerateJwtToken(propietario);
            return Ok(new { token });
        }

        private string HashPassword(string password)
        {
            var saltConfig = _config["Salt"];
            if (string.IsNullOrEmpty(saltConfig))
            {
                throw new ArgumentNullException("Salt configuration is missing.");
            }
            byte[] salt = Encoding.ASCII.GetBytes(saltConfig);
            return Convert.ToBase64String(
                KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA1,
                    iterationCount: 1000,
                    numBytesRequested: 256 / 8
                )
            );
        }

        private bool VerifyPassword(string enteredPassword, string storedHash)
        {
            return HashPassword(enteredPassword) == storedHash;
        }

        private string GenerateJwtToken(Propietario propietario)
        {
            var key = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(
                    _config["TokenAuthentication:SecretKey"]
                        ?? throw new ArgumentNullException("TokenAuthentication:SecretKey")
                )
            );
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, propietario.Email),
                new Claim("FullName", propietario.Nombre + " " + propietario.Apellido),
                new Claim(ClaimTypes.Role, "Propietario"),
            };

            var token = new JwtSecurityToken(
                issuer: _config["TokenAuthentication:Issuer"],
                audience: _config["TokenAuthentication:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginView
    {
        public string? Usuario { get; set; }
        public string? Clave { get; set; }
    }
}
