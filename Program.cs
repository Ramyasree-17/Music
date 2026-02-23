using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TunewaveAPIDB1.Common;
using TunewaveAPIDB1.Data;
using TunewaveAPIDB1.Repositories;
using TunewaveAPIDB1.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger generation (always register - serving is controlled later)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Tunewave API", Version = "v1" });

    // Ensure all APIs are included
    c.DocInclusionPredicate((docName, apiDesc) => true);

    // Order tags by section number for Swagger UI
    c.TagActionsBy(api =>
    {
        var controllerName = api.ActionDescriptor.RouteValues["controller"];

        // Map controllers to section numbers
        var sectionMap = new Dictionary<string, int>
        {
            { "Auth", 1 },
            { "Users", 2 },
            { "Enterprises", 3 },
            { "Labels", 4 },
            { "Artists", 5 },
            { "Releases", 6 },
            { "Tracks", 7 },
            { "Files", 8 },
            { "Qc", 9 },
            { "Delivery", 10 },
            { "Royalties", 11 },
            { "Wallet", 12 },
            { "Billing", 13 },
            { "Notifications", 14 },
            { "Support", 15 },
            { "Search", 16 },
            { "Admin", 17 },
            { "Jobs", 18 },
            { "Settings", 19 },
            { "Audit", 20 },
            { "Branding", 21 },
            { "Health", 22 },
            { "Mail", 23 },
            { "Teams", 24 },
            { "Whatsapp", 25 },
            { "ZohoInvoiceWebhook", 26 }
        };

        if (controllerName != null && sectionMap.TryGetValue(controllerName, out var sectionNum))
        {
            var sectionNames = new Dictionary<int, string>
            {
                { 1, "Authentication & Session" },
                { 2, "User & Identity" },
                { 3, "Enterprises" },
                { 4, "Labels" },
                { 5, "Artists" },
                { 6, "Releases" },
                { 7, "Tracks" },
                { 8, "Files" },
                { 9, "QC (Quality Control)" },
                { 10, "Delivery" },
                { 11, "Royalties" },
                { 12, "Wallet & Ledger" },
                { 13, "Billing & Subscriptions" },
                { 14, "Notifications" },
                { 15, "Support" },
                { 16, "Search" },
                { 17, "Admin / SuperAdmin" },
                { 18, "Jobs" },
                { 19, "Settings" },
                { 20, "Audit" },
                { 21, "Branding" },
                { 22, "Health" },
                { 23, "Mail" },
                { 24, "Teams" },
                { 25, "Whatsapp" },
                { 26, "ZohoInvoiceWebhook" }
            };

            return new[] { sectionNames[sectionNum] };
        }

        // Fallback: Try to get GroupName from ApiExplorerSettings
        var groupName = api.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Mvc.ApiExplorerSettingsAttribute>()
            .FirstOrDefault()?.GroupName;

        if (!string.IsNullOrEmpty(groupName))
        {
            return new[] { groupName };
        }

        return new[] { controllerName ?? "Other" };
    });

    // Order endpoints within each section
    c.OrderActionsBy(apiDesc =>
    {
        var controllerName = apiDesc.ActionDescriptor.RouteValues["controller"];
        var actionName = apiDesc.ActionDescriptor.RouteValues["action"];
        var sectionMap = new Dictionary<string, int>
        {
            { "Auth", 1 }, { "Users", 2 }, { "Enterprises", 3 }, { "Labels", 4 },
            { "Artists", 5 }, { "Releases", 6 }, { "Tracks", 7 }, { "Files", 8 },
            { "Qc", 9 }, { "Delivery", 10 }, { "Royalties", 11 }, { "Wallet", 12 },
            { "Billing", 13 }, { "Notifications", 14 }, { "Support", 15 }, { "Search", 16 },
            { "Admin", 17 }, { "Jobs", 18 }, { "Settings", 19 }, { "Audit", 20 },
            { "Branding", 21 }, { "Health", 22 }, { "Mail", 23 }, { "Teams", 24 },
            { "Whatsapp", 25 }, { "ZohoInvoiceWebhook", 26 }
        };

        if (controllerName != null && sectionMap.TryGetValue(controllerName, out var sectionNum))
        {
            return $"{sectionNum:D2}_{apiDesc.RelativePath}";
        }
        return apiDesc.RelativePath;
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT token: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });

    // Configure file upload support for multipart/form-data
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

// Entity Framework Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Hangfire Configuration
var hangfireConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer();

// Custom services & repositories
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<ResetTokenService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<BrevoService>();
builder.Services.AddSingleton<CdnService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<GoogleDriveService>();
builder.Services.AddSingleton<OneDriveService>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupWorker>();
builder.Services.AddHostedService<RecurringInvoiceWorker>();
builder.Services.AddHostedService<OverdueInvoiceCheckerService>();
builder.Services.AddHostedService<AutoSuspendHostedService>();
builder.Services.AddSingleton<ZohoBooksService>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
builder.Services.AddScoped<ILabelRepository, LabelRepository>();
builder.Services.AddScoped<IArtistRepository, ArtistRepository>();
builder.Services.AddScoped<IReleaseRepository, ReleaseRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();

// ---- JWT Authentication ----
var key = builder.Configuration["Jwt:Key"];
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(key))
    throw new Exception("⚠️ Missing Jwt:Key in appsettings.json or environment variables.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true; // true for production
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(10)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("SuperAdmin", "EnterpriseAdmin"));
    options.AddPolicy("EnterpriseAdminOrSuperAdmin", policy => policy.RequireRole("SuperAdmin", "EnterpriseAdmin"));
    options.AddPolicy("SystemOrQc", policy => policy.RequireRole("SuperAdmin", "TunewaveQC", "System"));
    options.AddPolicy("SupportOrSuperAdmin", policy => policy.RequireRole("SuperAdmin", "Support"));
    options.AddPolicy("WorkerOrAdmin", policy => policy.RequireRole("SuperAdmin", "Worker", "System"));
    options.AddPolicy("FinanceOrAdmin", policy => policy.RequireRole("SuperAdmin", "LabelFinance", "EnterpriseFinance", "TunewaveFinance"));
});

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ---- Read swagger config ----
// Swagger:Mode = Disabled | Secret | Auth | Open
var swaggerMode = builder.Configuration.GetValue<string>("Swagger:Mode") ?? "Disabled";
var swaggerSecret = builder.Configuration["Swagger:Secret"]; // used for Secret mode
var allowInDevelopment = true; // keep swagger in dev always

// ---- Middlewares ----
if (app.Environment.IsDevelopment() && allowInDevelopment)
{
    // In development, keep default behavior for convenience
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tunewave API v1"));
}

// For non-development environments, decide based on swaggerMode
if (!app.Environment.IsDevelopment())
{
    switch (swaggerMode.Trim().ToLowerInvariant())
    {
        case "open":
            // Serve swagger UI to everyone (NOT recommended for public-facing apps)
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tunewave API v1"));
            break;

        case "secret":
            // Serve swagger but require a custom header X-Swagger-Secret
            if (string.IsNullOrWhiteSpace(swaggerSecret))
            {
                // fail-fast to avoid accidental open docs
                throw new Exception("Swagger:Secret must be configured when Swagger:Mode = Secret");
            }

            // Middleware checks header before serving anything under /swagger
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
                {
                    if (!context.Request.Headers.TryGetValue("X-Swagger-Secret", out var headerVal) ||
                        headerVal != swaggerSecret)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Unauthorized - missing or invalid Swagger secret");
                        return;
                    }
                }
                await next();
            });

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tunewave API v1"));
            break;

        case "auth":
            // Require JWT authentication to access Swagger endpoints
            // Map a branch to apply auth only to swagger endpoints
            app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments("/swagger"), builder =>
            {
                builder.UseAuthentication();
                builder.UseAuthorization();

                builder.Use(async (context, next) =>
                {
                    // If not authenticated, return 401
                    if (!context.User?.Identity?.IsAuthenticated ?? true)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Unauthorized - valid JWT required to access Swagger UI");
                        return;
                    }
                    await next();
                });

                builder.UseSwagger();
                builder.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tunewave API v1"));
            });
            break;

        case "disabled":
        default:
            // Do not register swagger middleware in production
            // You can still enable it temporarily by changing config or using environment variable
            break;
    }
}

// ---------- Static files with audio mime mappings & caching ----------
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".m4a"] = "audio/mp4";
provider.Mappings[".aac"] = "audio/aac";
provider.Mappings[".oga"] = "audio/ogg";
provider.Mappings[".ogg"] = "audio/ogg";
// Add any other mappings you need (e.g., .flac)

// Serve static files (wwwroot) with the custom provider and light caching
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    OnPrepareResponse = ctx =>
    {
        // Cache static audio for 1 day (adjust as needed)
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=86400";
    }
});
// -------------------------------------------------------------------- //

app.UseCors("OpenCors");
app.UseHttpsRedirection();

// Hangfire Dashboard (optional - can be secured)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
