using System.IdentityModel.Tokens.Jwt;
using System.Text;
using MeetingRoomBooking.Api.Middleware;
using MeetingRoomBooking.Data;
using MeetingRoomBooking.Api.Infrastructure;
using MeetingRoomBooking.Data.Entities;
using MeetingRoomBooking.Services.Implementations;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");
var jwtSettings = new JwtSettings
{
    Issuer = builder.Configuration["Jwt:Issuer"] ?? string.Empty,
    Audience = builder.Configuration["Jwt:Audience"] ?? string.Empty,
    Key = builder.Configuration["Jwt:Key"] ?? string.Empty
};
if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) || string.IsNullOrWhiteSpace(jwtSettings.Audience) ||
    Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32 || jwtSettings.Key.StartsWith("REPLACE_WITH_"))
{
    throw new InvalidOperationException("Set Jwt:Issuer, Jwt:Audience and a random Jwt:Key of at least 32 UTF-8 bytes in local appsettings.json.");
}

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var message = context.ModelState.Values.SelectMany(value => value.Errors)
        .Select(error => error.ErrorMessage).FirstOrDefault() ?? "Geçersiz istek.";
    return new BadRequestObjectResult(new { message, code = "VALIDATION_ERROR" });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            var sub = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var jti = principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var role = principal?.FindFirst("role")?.Value;
            if (!int.TryParse(sub, out var userId) || string.IsNullOrWhiteSpace(jti) || string.IsNullOrWhiteSpace(role))
            {
                context.Fail("Invalid token claims.");
                return;
            }

            var database = context.HttpContext.RequestServices.GetRequiredService<BookingDbContext>();
            var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId,
                context.HttpContext.RequestAborted);
            if (user is null || user.Role.ToString() != role ||
                await database.RevokedTokens.AnyAsync(x => x.JwtId == jti, context.HttpContext.RequestAborted))
            {
                context.Fail("Token was revoked or role changed.");
            }
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Giriş yapmanız gerekiyor veya oturumunuz sona erdi.", code = "UNAUTHORIZED" });
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Bu işlem için yetkiniz yok.", code = "FORBIDDEN" });
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddHostedService<DemoDataHostedService>();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOfficeService, OfficeService>();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
