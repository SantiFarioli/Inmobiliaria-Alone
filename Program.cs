using System.Text;
using DotNetEnv; // Para cargar el archivo .env
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Inmobiliaria_Alone.Data;

var builder = WebApplication.CreateBuilder(args);

// Cargar variables del archivo .env
Env.Load(); // Asegúrate de que esta línea esté antes de cualquier acceso a las variables de entorno
builder.Configuration.AddEnvironmentVariables(); // Permitir acceso a las variables de entorno cargadas

Console.WriteLine("SMTP_Host desde Environment: " + Environment.GetEnvironmentVariable("SMTP_Host"));
Console.WriteLine("SMTP_Port desde Environment: " + Environment.GetEnvironmentVariable("SMTP_Port"));
Console.WriteLine("SMTP_User desde Environment: " + Environment.GetEnvironmentVariable("SMTP_User"));
Console.WriteLine("SMTP_Pass desde Environment: " + Environment.GetEnvironmentVariable("SMTP_Pass"));

// Imprimir para depuración (opcional, eliminar en producción)
Console.WriteLine("TokenAuthentication_SecretKey desde Environment: " + Environment.GetEnvironmentVariable("TokenAuthentication_SecretKey"));

// Configurar la cadena de conexión a la base de datos
builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 21))
    )
);

// Configurar JWT
var secretKey = Environment.GetEnvironmentVariable("TokenAuthentication_SecretKey"); // Usar variable de entorno
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("TokenAuthentication:SecretKey is not configured.");
}
var key = Encoding.ASCII.GetBytes(secretKey);
builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
        };
    });

// Habilitar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAllOrigins",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
    );
});

// Configurar JSON para manejar ciclos de referencia
builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});

// Construir la aplicación
var app = builder.Build();

// Configuración del entorno de desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });
}

// Configurar el middleware
app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins"); // Aplicar la política de CORS
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
