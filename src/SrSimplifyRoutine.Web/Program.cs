using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SrSimplifyRoutine.Web.Components;
using SrSimplifyRoutine.Web.Components.Account;
using SrSimplifyRoutine.Web.Data;
using SrSimplifyRoutine.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var development = builder.Environment.IsDevelopment();
var migrateOnly = args.Contains("--migrate");
if (!development && !migrateOnly)
{
    var origin = builder.Configuration["Application:PublicOrigin"];
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var publicUri) || publicUri.Scheme != "https" ||
        string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]) ||
        string.IsNullOrWhiteSpace(builder.Configuration["Smtp:From"]) ||
        !builder.Configuration.GetValue<bool>("BotProtection:Enabled") ||
        string.IsNullOrWhiteSpace(builder.Configuration["BotProtection:SiteKey"]) ||
        string.IsNullOrWhiteSpace(builder.Configuration["BotProtection:SecretKey"]) ||
        string.IsNullOrWhiteSpace(builder.Configuration["AllowedHosts"]) || builder.Configuration["AllowedHosts"]!.Contains('*'))
        throw new InvalidOperationException("Production requires HTTPS PublicOrigin, explicit AllowedHosts, SMTP and enabled Turnstile keys. See README.md.");
}

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32 * 1024);
builder.Services.Configure<FormOptions>(options => { options.ValueLengthLimit = 8192; options.ValueCountLimit = 64; options.MultipartBodyLengthLimit = 32768; });
builder.Services.AddRazorComponents().AddInteractiveServerComponents(options =>
{
    options.DisconnectedCircuitMaxRetained = 100;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);
    options.MaxBufferedUnacknowledgedRenderBatches = 5;
}).AddHubOptions(options => options.MaximumReceiveMessageSize = 32 * 1024);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireDigit = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager().AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = development ? "SR.Session" : "__Host-SR.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
});
var keysPath = builder.Configuration["Application:KeysPath"] ?? Path.Combine(builder.Environment.ContentRootPath, ".keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection().SetApplicationName("SR.SimplifyRoutine").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AbuseGuard>();
builder.Services.AddSingleton<CircuitBudget>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, BudgetCircuitHandler>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<PlannerService>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, RoutineEmailSender>();
builder.Services.AddHttpClient<BotProtection>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHostedService<UnverifiedAccountCleanup>();

var app = builder.Build();
// Development bootstraps itself. Production migrations are an explicit deployment operation.
if (development || migrateOnly)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}
if (migrateOnly) return;
if (development) app.UseMigrationsEndPoint();
else { app.UseExceptionHandler("/Error", createScopeForErrors: true); app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
    if (context.Request.Path.StartsWithSegments("/Account")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseMiddleware<RequestThrottleMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();
app.Run();

public partial class Program;
