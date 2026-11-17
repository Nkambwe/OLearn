using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OLearn.Api;
using OLearn.Data;
using OLearn.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<TestModeOptions>(builder.Configuration.GetSection("TestMode"));
builder.Services.AddSingleton<RuntimeSettings>();
builder.Services.AddSingleton<LeaseCryptoService>();
builder.Services.AddScoped<LeaseService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<VersionService>();
builder.Services.AddScoped<ResourceService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<SyncService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "OLearn.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);   // long, so a student who is offline stays signed in on the device
        o.SlidingExpiration = true;
        // API calls get 401 (not a redirect to the sign-in page) so the offline code can tell the difference.
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 401;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", p => p.RequireRole("Admin")));

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Error");
    o.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
});

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 250_000_000);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 250_000_000);

var app = builder.Build();

// Creating the service writes wwwroot/js/lease-public-key.js, so it must exist before the first request.
app.Services.GetRequiredService<LeaseCryptoService>();
await DbInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapOLearnApi();
app.MapRazorPages();
app.Run();