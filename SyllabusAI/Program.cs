using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SyllabusAI.Data;
using SyllabusAI.Services;

var builder = WebApplication.CreateBuilder(args);

// Yerel geliştirme: SQLite dosya DB (Data/syllabus_local.db). SQL Server için connection string değiştirin.
var defaultCs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı olmalı.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (IsSqliteConnectionString(defaultCs))
        options.UseSqlite(defaultCs);
    else
        options.UseSqlServer(defaultCs);
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
builder.Services.AddSingleton<ISyllabusFileTextExtractor, SyllabusFileTextExtractor>();
builder.Services.AddHttpClient(nameof(OpenAiSyllabusClient), (sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = (cfg["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/');
    if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        baseUrl += "/v1";
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddSingleton<IOpenAiSyllabusClient, OpenAiSyllabusClient>();
builder.Services.AddScoped<ISyllabusRagIndexService, SyllabusRagIndexService>();

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

// Demo mod kapalıysa DB (UseDemoAuth: true iken veritabanına bağlanmaz)
var useDemoAuth = app.Configuration.GetValue<bool>("UseDemoAuth");
if (!useDemoAuth)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (app.Environment.IsDevelopment())
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data"));
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EF Migrate tamamlanamadı (eski DB / geçmiş uyumsuz olabilir). SQL Server ise şema yaması denenecek.");
        }

        try
        {
            await DataSeeder.SeedAsync(db);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Seed atlandı.");
        }
    }

    try
    {
        await SqlServerSchemaPatch.EnsureSyllabusSupportTablesAsync(db);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "SQL Server syllabus tablo yaması atlandı.");
    }
}
else if (useDemoAuth)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Demo mod aktif: Giriş veritabanı olmadan test edilebilir (şifre: appsettings'teki DemoPassword).");
}

app.Run();

static bool IsSqliteConnectionString(string cs) =>
    cs.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
    || cs.TrimStart().StartsWith("Filename=", StringComparison.OrdinalIgnoreCase);
