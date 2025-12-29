using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Volonterko.Components;
using Volonterko.Components.Account;
using Volonterko.Data;
using Volonterko.Domain.Constants;
using Volonterko.Domain.Enums;


var builder = WebApplication.CreateBuilder(args);

// SERVICES
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddScoped<Volonterko.Services.OrganizationService>();
builder.Services.AddScoped<Volonterko.Services.VolunteerActionService>();
builder.Services.AddScoped<Volonterko.Services.SignupService>();

builder.Services.AddAuthorization();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.Configure<CircuitOptions>(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
});


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// PIPELINE
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage(); // ✅ so we actually SEE the exception
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.MapPost("/api/org/logo", [Authorize(Roles = Roles.Organization)] async (
    HttpContext http,
    IWebHostEnvironment env,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) =>
{
    const long maxBytes = 2_000_000; // 2MB

    var userId = userManager.GetUserId(http.User);
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Redirect("/org/profile?logo=forbidden");

    var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == userId);
    if (org is null)
        return Results.Redirect("/org/profile?logo=forbidden");

    if (!http.Request.HasFormContentType)
        return Results.Redirect("/org/profile?logo=error");

    var form = await http.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file is null || file.Length == 0)
        return Results.Redirect("/org/profile?logo=empty");

    if (file.Length > maxBytes)
        return Results.Redirect("/org/profile?logo=too_large");

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
        return Results.Redirect("/org/profile?logo=bad_type");

    var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "orgs");
    Directory.CreateDirectory(uploadsDir);

    var safeName = $"{org.Id}_{Guid.NewGuid():N}{ext}";
    var fullPath = Path.Combine(uploadsDir, safeName);

    await using (var fs = File.Create(fullPath))
    {
        await file.CopyToAsync(fs);
    }

    org.LogoUrl = $"uploads/orgs/{safeName}";
    await db.SaveChangesAsync();

    return Results.Redirect("/org/profile?logo=ok");
})
.DisableAntiforgery();

// =======================================
// ACTION IMAGE UPLOAD ENDPOINT (HTTP FORM POST)
// =======================================
app.MapPost("/api/actions/{id:int}/image", [Authorize(Roles = Roles.Organization)] async (
    int id,
    HttpContext http,
    IWebHostEnvironment env,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("ActionImageUpload");

    try
    {
        const long maxBytes = 5_000_000; // 5MB

        // sanity: web root must exist
        if (string.IsNullOrWhiteSpace(env.WebRootPath))
        {
            log.LogError("WebRootPath is null/empty.");
            return Results.Redirect($"/org/actions/edit/{id}?img=server_root");
        }

        // must be logged in + org owner
        var userId = userManager.GetUserId(http.User);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OwnerUserId == userId);
        if (org is null)
            return Results.Forbid();

        var action = await db.VolunteerActions.FirstOrDefaultAsync(a => a.Id == id);
        if (action is null)
            return Results.Redirect($"/org/actions?img=not_found");

        if (action.OrganizationId != org.Id)
            return Results.Forbid();

        if (!http.Request.HasFormContentType)
            return Results.Redirect($"/org/actions/edit/{id}?img=bad_request");

        var form = await http.Request.ReadFormAsync();
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
            return Results.Redirect($"/org/actions/edit/{id}?img=empty");

        if (file.Length > maxBytes)
            return Results.Redirect($"/org/actions/edit/{id}?img=too_large");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            return Results.Redirect($"/org/actions/edit/{id}?img=bad_type");

        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "actions");
        Directory.CreateDirectory(uploadsDir);

        var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsDir, safeName);

        await using (var fs = File.Create(fullPath))
        {
            await file.CopyToAsync(fs);
        }

        action.ImageUrl = $"uploads/actions/{safeName}";
        await db.SaveChangesAsync();

        return Results.Redirect($"/org/actions/edit/{id}?img=ok");
    }
   
  catch (Exception ex)
    {
        log.LogError(ex, "Upload failed for actionId={ActionId}", id);
        return Results.Redirect($"/org/actions/edit/{id}?img=error");
    }

})
.DisableAntiforgery();

// Seed roles + admin
await SeedRolesAndAdminAsync(app);

app.Run();

static async Task SeedRolesAndAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = [Roles.Volunteer, Roles.Organization, Roles.Admin];

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var config = app.Configuration;

    var adminEmail = config["AdminSeed:Email"];
    var adminPassword = config["AdminSeed:Password"];

    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        // Ako nije definirano – preskoči seed
        return;
    }


    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser is null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create admin user: {errors}");
        }
    }

    if (!await userManager.IsInRoleAsync(adminUser, Roles.Admin))
    {
        var addRoleResult = await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        if (!addRoleResult.Succeeded)
        {
            var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to add admin role: {errors}");
        }
    }
}
