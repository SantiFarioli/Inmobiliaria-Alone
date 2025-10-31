using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Inmobiliaria_Alone.Data;

var builder = WebApplication.CreateBuilder(args);

//  Cargar variables de entorno desde .env y el sistema

Env.Load();
builder.Configuration.AddEnvironmentVariables();

// Log de verificación (solo para desarrollo)
Console.WriteLine(" SMTP_Host: " + Environment.GetEnvironmentVariable("SMTP_Host"));
Console.WriteLine(" TokenAuthentication_SecretKey: " + Environment.GetEnvironmentVariable("TokenAuthentication_SecretKey"));
Console.WriteLine(" TokenAuthentication_Issuer: " + Environment.GetEnvironmentVariable("TokenAuthentication_Issuer"));
Console.WriteLine(" TokenAuthentication_Audience: " + Environment.GetEnvironmentVariable("TokenAuthentication_Audience"));

//  Configurar conexión a la base de datos

builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 21))
    )
);

//  Configurar JWT (validación completa)

var secretKey =
    Environment.GetEnvironmentVariable("TokenAuthentication_SecretKey") ??
    Environment.GetEnvironmentVariable("TokenAuthentication__SecretKey");

var issuer =
    Environment.GetEnvironmentVariable("TokenAuthentication_Issuer") ??
    Environment.GetEnvironmentVariable("TokenAuthentication__Issuer");

var audience =
    Environment.GetEnvironmentVariable("TokenAuthentication_Audience") ??
    Environment.GetEnvironmentVariable("TokenAuthentication__Audience");

if (string.IsNullOrEmpty(secretKey))
    throw new InvalidOperationException("❌ TokenAuthentication_SecretKey no está configurada en .env");

var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // 🔧 útil en desarrollo local
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,

        ValidIssuer = issuer,
        ValidAudience = audience,
        ClockSkew = TimeSpan.Zero // evita el retraso por diferencia de tiempo
    };
});

//  Habilitar CORS

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

//  Configurar JSON para evitar ciclos de referencia

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Inmobiliaria Alone API",
        Version = "v1",
        Description = "API REST para gestión de inmuebles y propietarios"
    });
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inmobiliaria Alone API v1");
        c.RoutePrefix = string.Empty;
    });
}

// Middleware

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
