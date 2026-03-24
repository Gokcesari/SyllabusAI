using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SyllabusAI.Data;
using SyllabusAI.Services;

var builder = WebApplication.CreateBuilder(args);

// Veritabanı: GitHub'tan indirilen DB için ConnectionStrings:DefaultConnection kullanın
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAiService, AiService>();

builder.Services.AddControllers();

// JWT (rapor: 401 Unauthorized, 403 Forbidden - RBAC)
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key appsettings'te tanımlanmalı.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sadece HTTP kullanıldığında "Failed to determine the https port" uyarısını önlemek için
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Demo mod kapalıysa ve Development ise DB oluştur/seed (UseDemoAuth: true iken veritabanına bağlanmaz)
var useDemoAuth = app.Configuration.GetValue<bool>("UseDemoAuth");
if (app.Environment.IsDevelopment() && !useDemoAuth)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await DataSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Veritabanı bağlantısı veya oluşturma atlandı. Connection string ve DB'yi kontrol edin.");
    }
}
else if (useDemoAuth)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Demo mod aktif: Giriş veritabanı olmadan test edilebilir (şifre: appsettings'teki DemoPassword).");
}

app.Run();
