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

        [HttpPost("recuperacion-contrasenia")]
        [AllowAnonymous]
        public async Task<IActionResult> SolicitarRecuperacionContrasena([FromForm] string email)
        {
            var propietario = await _context.Propietarios.FirstOrDefaultAsync(x => x.Email == email);
            if (propietario == null)
            {
                return Ok("Si el correo está registrado, se enviará un enlace de recuperación.");
            }

            var token = GenerarTokenRestablecimientoContrasena(propietario);
            var propietarioEmail = propietario.Email;
            var enlaceRestablecimiento = $"{Request.Scheme}://{Request.Host}/api/Propietarios/restablecer-contrasenia?token={token}&email={propietarioEmail}";

            var mensaje = $"Por favor, restablezca su contraseña usando este enlace: <a href='{enlaceRestablecimiento}'>Restablecer Contraseña</a>";
            EnviarCorreo(propietario.Email, "Restablecimiento de Contraseña", mensaje);

            return Ok("Si el correo está registrado, se enviará un enlace de recuperación.");
        }

        [HttpPost("restablecer-contrasenia")]
        [AllowAnonymous]
        public async Task<IActionResult> RestablecerContrasena([FromQuery] string token, [FromQuery] string email, [FromForm] string nuevaContrasena)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(nuevaContrasena))
            {
                return BadRequest("Parámetros inválidos.");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = _config["TokenAuthentication:SecretKey"] ?? throw new ArgumentNullException("TokenAuthentication:SecretKey");

            Console.WriteLine("SecretKey en RestablecerContrasena: " + secretKey);

            var key = Encoding.ASCII.GetBytes(secretKey);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero,
                    ValidateLifetime = true
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
            catch (SecurityTokenExpiredException)
            {
                return BadRequest("El token ha expirado.");
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                return BadRequest("El token tiene una firma inválida.");
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

        private string GenerarTokenRestablecimientoContrasena(Propietario propietario)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = _config["TokenAuthentication:SecretKey"] ?? throw new ArgumentNullException("TokenAuthentication:SecretKey");
            var key = Encoding.ASCII.GetBytes(secretKey);
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
            if (string.IsNullOrEmpty(correoDestino))
            {
                throw new ArgumentNullException(nameof(correoDestino), "El correo de destino no puede ser nulo o vacío.");
            }

            var emailMessage = new MimeMessage();

            // Obtener el correo del remitente desde la configuración y verificarlo
            var smtpUser = _config["SMTP:User"];
            if (string.IsNullOrEmpty(smtpUser))
            {
                throw new ArgumentNullException("SMTP:User", "El correo del remitente (SMTP:User) no está configurado.");
            }

            // configura remitente y destinatario
            emailMessage.From.Add(new MailboxAddress("Inmobiliaria Alone", smtpUser));
            emailMessage.To.Add(new MailboxAddress("", correoDestino));
            emailMessage.Subject = asunto;

            var bodyBuilder = new BodyBuilder { HtmlBody = mensaje };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                try
                {
                    var smtpHost = _config["SMTP:Host"];
                    var smtpPortStr = _config["SMTP:Port"];
                    var smtpPass = _config["SMTP:Pass"];

                    if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpPortStr) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
                    {
                        throw new InvalidOperationException("Las variables de entorno SMTP no están configuradas correctamente.");
                    }

                    if (!int.TryParse(smtpPortStr, out int smtpPort))
                    {
                        throw new InvalidOperationException("El valor de SMTP:Port no es un número válido.");
                    }

                    switch (smtpPort)
                    {
                        case 465:
                            client.Connect(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.SslOnConnect);
                            break;
                        case 587:
                            client.Connect(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                            break;
                        default:
                            throw new InvalidOperationException("El puerto SMTP no es válido. Use 465 para SSL o 587 para TLS.");
                    }

                    client.Authenticate(smtpUser, smtpPass);
                    client.Send(emailMessage);
                    client.Disconnect(true);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Error al enviar el correo electrónico.", ex);
                }
            }
        }
    }

    public class LoginView
    {
        public string? Usuario { get; set; }
        public string? Clave { get; set; }
    }
}