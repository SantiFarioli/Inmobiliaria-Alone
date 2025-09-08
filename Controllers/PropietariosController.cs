using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inmobiliaria_Alone.Data;
using Inmobiliaria_Alone.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MimeKit;

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

        // -------------------- CRUD BÁSICO --------------------

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Propietario>>> GetPropietarios()
        {
            return await _context.Propietarios.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Propietario>> GetPropietario(int id)
        {
            var propietario = await _context.Propietarios.FindAsync(id);
            if (propietario == null) return NotFound();
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
        public async Task<IActionResult> PutPropietario(int id, [FromBody] Propietario propietario)
        {
            if (id != propietario.IdPropietario) return BadRequest();

            var existing = await _context.Propietarios.AsNoTracking()
                                .FirstOrDefaultAsync(p => p.IdPropietario == id);
            if (existing == null) return NotFound();

            // Si viene Password, la re-hasheamos; si no, conservamos la anterior
            if (string.IsNullOrWhiteSpace(propietario.Password))
                propietario.Password = existing.Password;
            else
                propietario.Password = HashPassword(propietario.Password);

            _context.Entry(propietario).State = EntityState.Modified;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!PropietarioExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePropietario(int id)
        {
            var propietario = await _context.Propietarios.FindAsync(id);
            if (propietario == null) return NotFound();

            _context.Propietarios.Remove(propietario);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool PropietarioExists(int id) =>
            _context.Propietarios.Any(e => e.IdPropietario == id);

        // -------------------- AUTH --------------------

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromForm] LoginView loginView)
        {
            var propietario = await _context.Propietarios
                                .FirstOrDefaultAsync(x => x.Email == loginView.Usuario);

            if (propietario == null || string.IsNullOrEmpty(loginView.Clave) ||
                !VerifyPassword(loginView.Clave, propietario.Password))
                return BadRequest("Nombre de usuario o clave incorrecta");

            var token = GenerateJwtToken(propietario);
            return Ok(new { token });
        }

        [HttpGet("perfil")]
        [Authorize]
        public async Task<ActionResult<Propietario>> GetMyDetails()
        {
            var email = User.FindFirst(ClaimTypes.Name)?.Value;
            if (email == null) return Unauthorized();

            var propietario = await _context.Propietarios.FirstOrDefaultAsync(p => p.Email == email);
            if (propietario == null) return NotFound();

            return propietario;
        }

        // -------------------- RECUPERAR / RESTABLECER --------------------

        [HttpPost("solicitar-restablecimiento")]
        [AllowAnonymous]
        public async Task<IActionResult> SolicitarRestablecimiento([FromForm] string email)
        {
            var propietario = await _context.Propietarios.FirstOrDefaultAsync(p => p.Email == email);
            if (propietario == null) return BadRequest("Correo electrónico no encontrado.");

            var token = Guid.NewGuid().ToString();
            propietario.ResetToken = token;
            propietario.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            var resetLink =
                $"{Request.Scheme}://{Request.Host}/api/Propietarios/{propietario.IdPropietario}/restablecer-contrasena?token={token}";

            try
            {
                await EnviarCorreoAsync(email, "Restablecimiento de contraseña",
                    $"Haga clic en el siguiente enlace para restablecer su contraseña: {resetLink}");
                return Ok("Se ha enviado un enlace de restablecimiento de contraseña a su correo electrónico.");
            }
            catch
            {
                // Revertir token si falla el envío
                propietario.ResetToken = null;
                propietario.ResetTokenExpiry = null;
                await _context.SaveChangesAsync();
                return StatusCode(500, "No se pudo enviar el correo. Verifica la configuración SMTP.");
            }
        }

        [HttpGet("{id}/restablecer-contrasena")]
        [AllowAnonymous]
        public IActionResult MostrarFormularioRestablecimiento(int id, [FromQuery] string token)
        {
            var propietario = _context.Propietarios
                .FirstOrDefault(p => p.IdPropietario == id &&
                                     p.ResetToken == token &&
                                     p.ResetTokenExpiry > DateTime.UtcNow);

            if (propietario == null)
                return BadRequest("Token de restablecimiento inválido o expirado.");

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
        [AllowAnonymous]
        public async Task<IActionResult> RestablecerContraseña(int id, [FromBody] RestablecerContrasenaRequest request)
        {
            var propietario = await _context.Propietarios.FindAsync(id);
            if (propietario == null) return NotFound("Propietario no encontrado.");

            if (!VerificarTokenDeRestablecimiento(propietario, request.Token))
                return BadRequest("Token de restablecimiento inválido o expirado.");

            propietario.Password = HashPassword(request.NuevaContrasena);
            // opcional: limpiar token post-uso
            propietario.ResetToken = null;
            propietario.ResetTokenExpiry = null;

            await _context.SaveChangesAsync();
            return Ok("Contraseña restablecida con éxito.");
        }

        // -------------------- HELPERS --------------------

        private bool VerificarTokenDeRestablecimiento(Propietario propietario, string token)
        {
            if (propietario.ResetToken == null || propietario.ResetTokenExpiry == null) return false;
            return propietario.ResetToken == token && propietario.ResetTokenExpiry > DateTime.UtcNow;
        }

        private string HashPassword(string password)
        {
            var saltConfig = _config["Salt"] ?? Environment.GetEnvironmentVariable("Salt");
            if (string.IsNullOrEmpty(saltConfig))
                throw new ArgumentNullException("Salt configuration is missing.");

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

        private bool VerifyPassword(string enteredPassword, string storedHash) =>
            HashPassword(enteredPassword) == storedHash;

        private string GenerateJwtToken(Propietario propietario)
        {
            // Lee de appsettings o de variables de entorno (soporta TokenAuthentication__SecretKey)
            var secret = _config["TokenAuthentication:SecretKey"]
                         ?? Environment.GetEnvironmentVariable("TokenAuthentication_SecretKey")
                         ?? throw new ArgumentNullException("TokenAuthentication:SecretKey");

            var issuer = _config["TokenAuthentication:Issuer"]
                         ?? Environment.GetEnvironmentVariable("TokenAuthentication_Issuer");

            var audience = _config["TokenAuthentication:Audience"]
                         ?? Environment.GetEnvironmentVariable("TokenAuthentication_Audience");

            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, propietario.Email),
                new Claim("FullName", $"{propietario.Nombre} {propietario.Apellido}"),
                new Claim(ClaimTypes.Role, "Propietario"),
                new Claim("Dni", propietario.Dni ?? string.Empty),
                new Claim("Telefono", propietario.Telefono ?? string.Empty),
                new Claim("FotoPerfil", propietario.FotoPerfil ?? string.Empty)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpo)
        {
            // Lee SMTP de appsettings o .env
            var host = _config["SMTP_Host"] ?? Environment.GetEnvironmentVariable("SMTP_Host") ?? "smtp.gmail.com";
            var portStr = _config["SMTP_Port"] ?? Environment.GetEnvironmentVariable("SMTP_Port") ?? "587";
            var user = _config["SMTP_User"] ?? Environment.GetEnvironmentVariable("SMTP_User");
            var pass = _config["SMTP_Pass"] ?? Environment.GetEnvironmentVariable("SMTP_Pass");

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                throw new InvalidOperationException("SMTP user/password not configured.");

            var portOk = int.TryParse(portStr, out var port) ? port : 587;

            var mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(user));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;
            mensaje.Body = new TextPart("plain") { Text = cuerpo };

            using var cliente = new SmtpClient();
            var secure = portOk == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            await cliente.ConnectAsync(host, portOk, secure);
            cliente.AuthenticationMechanisms.Remove("XOAUTH2"); // forzar user/pass
            await cliente.AuthenticateAsync(user, pass);
            await cliente.SendAsync(mensaje);
            await cliente.DisconnectAsync(true);
        }
    }

    // -------------------- DTOs --------------------

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
