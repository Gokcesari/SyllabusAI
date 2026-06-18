using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SyllabusAI.Data;
using SyllabusAI.Services;
using SyllabusAI.Service.Helpers;

var builder = WebApplication.CreateBuilder(args);

var secretsFile = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "config", "secrets", "secrets.Local.json"));
if (File.Exists(secretsFile))
    builder.Configuration.AddJsonFile(secretsFile, optional: true, reloadOnChange: true);

// ConnectionStrings:DefaultConnection -> SQL Server (appsettings / appsettings.Development / config/secrets).
var defaultCs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be set.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(defaultCs);
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IWeeklyFeedbackService, WeeklyFeedbackService>();
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
builder.Services.AddSingleton<SyllabusCategoryMapper>();
builder.Services.AddSingleton<QuestionCategoryHintService>();
builder.Services.AddSingleton<SyllabusRagRetriever>();
builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddControllers();

// JWT (rapor: 401 Unauthorized, 403 Forbidden - RBAC)
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key must be set in appsettings.");
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
        Description = "Sign in with POST /api/Auth/login first; paste the returned accessToken here (with or without the Bearer prefix)."
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

// In Development, skip HTTPS redirect to avoid "Failed to determine the https port" when using HTTP only.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// When UseDemoAuth is false, run migrations / seed / schema patch.
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
            logger.LogWarning(ex, "EF Migrate failed (old DB / migration mismatch). SQL Server schema patch will be attempted.");
        }

        try
        {
            await DataSeeder.SeedAsync(db, app.Configuration);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Seed atlandÄ±.");
        }

        try
        {
            var rag = scope.ServiceProvider.GetRequiredService<ISyllabusRagIndexService>();
            var courseIds = await db.Courses
                .Where(c => c.SyllabusContent != null && c.SyllabusContent != "")
                .Select(c => new { c.Id, c.SyllabusContent })
                .ToListAsync();
            foreach (var c in courseIds)
            {
                var stale = !await db.SyllabusChunks.AnyAsync(ch => ch.CourseId == c.Id)
                    || await db.SyllabusChunks.AnyAsync(ch => ch.CourseId == c.Id && !ch.Text.StartsWith("[Section:"));
                if (!stale) continue;
                await rag.ReindexCourseAsync(c.Id, c.SyllabusContent!);
                logger.LogInformation("Reindexed syllabus chunks for course {CourseId}", c.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dev syllabus reindex skipped.");
        }
    }

    try
    {
        await SqlServerSchemaPatch.EnsureSyllabusSupportTablesAsync(db);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "SQL Server syllabus schema patch skipped.");
    }
}
else if (useDemoAuth)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Demo auth: login without DB seed; password from appsettings DemoPassword.");
}

app.Run();

