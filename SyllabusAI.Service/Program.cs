using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SyllabusAI.Data;
using SyllabusAI.Services;
using SyllabusAI.Service.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Yerel geliştirme: SQLite dosya DB (Data/syllabus_local.db). SQL Server için connection string değiştirin.
var defaultCs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı olmalı.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(defaultCs);
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IRolesService, RolesService>();
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
builder.Services.AddAutoMapper(typeof(MappingProfile));

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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SyllabusAI API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Önce POST /api/Auth/login ile giriş yapın; dönen accessToken değerini buraya yapıştırın (Bearer yazmadan sadece token da olur)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

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
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EF Migrate tamamlanamadı (eski DB / geçmiş uyumsuz olabilir). SQL Server ise şema yaması denenecek.");
        }

        try
        {
            await DataSeeder.SeedAsync(db, app.Configuration);
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
