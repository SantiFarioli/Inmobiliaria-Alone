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

        [HttpPost("solicitar-restablecimiento")]
        public async Task<IActionResult> SolicitarRestablecimiento([FromForm] string email)
        {
            var propietario = await _context.Propietarios.FirstOrDefaultAsync(p => p.Email == email);
            if (propietario == null)
            {
                return BadRequest("Correo electrónico no encontrado.");
            }

            // Generar token de restablecimiento
            var token = Guid.NewGuid().ToString();

            // Guardar el token y su fecha de expiración
            propietario.ResetToken = token;
            propietario.ResetTokenExpiry = DateTime.UtcNow.AddHours(1); // Token válido por 1 hora
            await _context.SaveChangesAsync();

            // Enviar correo electrónico con el enlace de restablecimiento
            var resetLink = $"{Request.Scheme}://{Request.Host}/api/Propietarios/{propietario.IdPropietario}/restablecer-contrasena?token={token}";
            await EnviarCorreoAsync(email, "Restablecimiento de contraseña", $"Haga clic en el siguiente enlace para restablecer su contraseña: {resetLink}");

            return Ok("Se ha enviado un enlace de restablecimiento de contraseña a su correo electrónico.");
        }

        private async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpo)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress("Nombre del remitente", _config["SMTP_User"]));
            mensaje.To.Add(new MailboxAddress("Nombre del destinatario", destinatario));
            mensaje.Subject = asunto;
            mensaje.Body = new TextPart("plain") { Text = cuerpo };

            using var cliente = new SmtpClient();

            if (int.TryParse(_config["SMTP_Port"], out int smtpPort))
            {
                await cliente.ConnectAsync(_config["SMTP_Host"], smtpPort, true);
            }
            else
            {
                throw new ArgumentException("Invalid SMTP port configuration");
            }

            await cliente.AuthenticateAsync(_config["SMTP_User"], _config["SMTP_Pass"]);
            await cliente.SendAsync(mensaje);
            await cliente.DisconnectAsync(true);
        }

        [HttpGet("{id}/restablecer-contrasena")]
        public IActionResult MostrarFormularioRestablecimiento(int id, [FromQuery] string token)
        {
            // Verificar si el token es válido y no ha expirado
            var propietario = _context.Propietarios.FirstOrDefault(p => p.IdPropietario == id && p.ResetToken == token && p.ResetTokenExpiry > DateTime.UtcNow);
            if (propietario == null)
            {
                return BadRequest("Token de restablecimiento inválido o expirado.");
            }

            return Ok(new
            {
                Message = "Formulario de restablecimiento de contraseña",
                Token = token,
                Propietario = new
                {
                    Id = propietario.IdPropietario,
                    Email = propietario.Email,
                    Nombre = propietario.Nombre,
                    Apellido = propietario.Apellido
                }
            });  
        }

        [HttpPost("{id}/restablecer-contrasena")]
        public async Task<IActionResult> RestablecerContraseña(int id, [FromBody] RestablecerContrasenaRequest request)
        {
            var propietario = await _context.Propietarios.FindAsync(id);
            if (propietario == null)
            {
                return NotFound("Propietario no encontrado.");
            }

            // Verificar el token de restablecimiento
            if (!VerificarTokenDeRestablecimiento(propietario, request.Token))
            {
                return BadRequest("Token de restablecimiento inválido o expirado.");
            }

            // Actualizar la contraseña
            propietario.Password = HashPassword(request.NuevaContrasena); 
            await _context.SaveChangesAsync();

            return Ok("Contraseña restablecida con éxito.");
        }

        private bool VerificarTokenDeRestablecimiento(Propietario propietario, string token)
        {
            // Verificar si el token y su fecha de expiración existen
            if (propietario.ResetToken == null || propietario.ResetTokenExpiry == null)
            {
                return false;
            }

            // Comprobar si el token coincide y si no ha expirado
            return propietario.ResetToken == token && propietario.ResetTokenExpiry > DateTime.UtcNow;
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
                new Claim("FotoPerfil", propietario.FotoPerfil)
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

    public class RestablecerContrasenaRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NuevaContrasena { get; set; } = string.Empty;
    }

    public class LoginView
    {
        public string? Usuario { get; set; }
        public string? Clave { get; set; }
    }
}