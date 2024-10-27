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
using MimeKit;
using MailKit.Net.Smtp;

namespace Inmobiliaria_Alone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PropietariosController : ControllerBase
    {
        private readonly MyDbContext _context;
        private readonly IConfiguration _config;

        public PropietariosController(MyDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Propietario>>> GetPropietarios()
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
        public async Task<ActionResult<Propietario>> PostPropietario([FromBody] Propietario propietario)
        {
            propietario.Password = HashPassword(propietario.Password);
            _context.Propietarios.Add(propietario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPropietario), new { id = propietario.IdPropietario }, propietario);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPropietario(int id, Propietario propietario)
        {
            if (id != propietario.IdPropietario)
            {
                return BadRequest();
            }

            var existingPropietario = await _context.Propietarios.AsNoTracking().FirstOrDefaultAsync(p => p.IdPropietario == id);
            if (existingPropietario == null)
            {
                return NotFound();
            }

            propietario.Password = HashPassword(propietario.Password);
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
            var propietario = await _context.Propietarios.FirstOrDefaultAsync(x => x.Email == loginView.Usuario);
            if (propietario == null || loginView.Clave == null || !VerifyPassword(loginView.Clave, propietario.Password))
            {
                return BadRequest("Nombre de usuario o clave incorrecta");
            }

            var token = GenerateJwtToken(propietario);
            return Ok(new { token });
        }

        [HttpGet("perfil")]
        [Authorize]
        public async Task<ActionResult<Propietario>> GetMyDetails()
        {
            var email = User.FindFirst(ClaimTypes.Name)?.Value;
            if (email == null)
            {
                return Unauthorized();
            }

            var propietario = await _context.Propietarios.FirstOrDefaultAsync(p => p.Email == email);
            if (propietario == null)
            {
                return NotFound();
            }

            return propietario;
        }

        [HttpPost("solicitar-recuperacion-contrasena")]
        [AllowAnonymous]
        public async Task<IActionResult> SolicitarRecuperacionContrasena([FromForm] string email)
        {
            var propietario = await _context.Propietarios.FirstOrDefaultAsync(x => x.Email == email);
            if (propietario == null)
            {
                return NotFound("No hay un usuario asociado con este correo electrónico.");
            }

            var token = GenerarTokenRestablecimientoContrasena(propietario);
            var enlaceRestablecimiento = Url.Action("RestablecerContrasena", "Propietarios", new { token = token, email = email }, Request.Scheme);

            var mensaje = $"Por favor, restablezca su contraseña usando este enlace: <a href='{enlaceRestablecimiento}'>Restablecer Contraseña</a>";
            EnviarCorreo(propietario.Email, "Restablecimiento de Contraseña", mensaje);

            return Ok("El enlace para restablecer la contraseña ha sido enviado a su correo electrónico.");
        }

        [HttpPost("restablecer-contrasena")]
        [AllowAnonymous]
        public async Task<IActionResult> RestablecerContrasena([FromForm] string token, [FromForm] string email, [FromForm] string nuevaContrasena)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["TokenAuthentication:SecretKey"]);
            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = int.Parse(jwtToken.Claims.First(x => x.Type == "UserId").Value);

                var propietario = await _context.Propietarios.FirstOrDefaultAsync(x => x.IdPropietario == userId && x.Email == email);
                if (propietario == null)
                {
                    return BadRequest("Token o correo electrónico inválido.");
                }

                propietario.Password = HashPassword(nuevaContrasena);
                await _context.SaveChangesAsync();

                return Ok("La contraseña ha sido restablecida exitosamente.");
            }
            catch
            {
                return BadRequest("Token inválido.");
            }
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
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_config["TokenAuthentication:SecretKey"] ?? throw new ArgumentNullException("TokenAuthentication:SecretKey")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, propietario.Email),
                new Claim("FullName", propietario.Nombre + " " + propietario.Apellido),
                new Claim(ClaimTypes.Role, "Propietario"),
                new Claim("Dni", propietario.Dni),
                new Claim("Telefono", propietario.Telefono),
                new Claim("FotoPerfil", propietario.FotoPerfil) // Nueva propiedad en el token
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

        private string GenerarTokenRestablecimientoContrasena(Propietario propietario)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["TokenAuthentication:SecretKey"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, propietario.Email),
                    new Claim("UserId", propietario.IdPropietario.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private void EnviarCorreo(string correoDestino, string asunto, string mensaje)
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("Inmobiliaria Alone", "no-reply@inmobiliaria-alone.com"));
            emailMessage.To.Add(new MailboxAddress("", correoDestino));
            emailMessage.Subject = asunto;

            var bodyBuilder = new BodyBuilder { HtmlBody = mensaje };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.tu-proveedor-de-correo.com", 587, false);
                client.Authenticate("tu-correo@example.com", "tu-contraseña-de-correo");
                client.Send(emailMessage);
                client.Disconnect(true);
            }
        }
    }

    public class LoginView
    {
        public string? Usuario { get; set; }
        public string? Clave { get; set; }
    }
}