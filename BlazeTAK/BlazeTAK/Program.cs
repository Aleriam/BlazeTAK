using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BlazeTAK.Client.Pages;
using BlazeTAK.Components;
using BlazeTAK.Components.Account;
using BlazeTAK.Data;
using BlazeTAK.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseNetTopologySuite()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddSingleton<IUserLocationService, UserLocationService>();
builder.Services.AddSingleton<IMessageService, MessageService>();
//also registered server-side: static prerendering of the wasm chat page resolves
//services from the server container; the connection itself starts interactively in the browser
builder.Services.AddScoped<BlazeTAK.Client.Services.ChatService>();

builder.Services.AddScoped<IUserInfoService, UserInfoService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHub<BlazeTAK.Hubs.MapHub>("/hubs/map");
app.MapHub<BlazeTAK.Hubs.ChatHub>("/hubs/chat");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazeTAK.Client._Imports).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Seed roles (admin, groupleader, user) and assign them:
//  - admin account is read from Admin:Email (launchSettings: Admin__Email)
//  - every other account gets the 'user' role
//  - appointed leaders (Group.LeaderId / Unit.LeaderId) get the 'groupleader' role
await SeedRolesAsync(app);

app.Run();

static async Task SeedRolesAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RoleSeed");

    //apply pending migrations first, so the seed queries never hit an outdated schema
    await dbContext.Database.MigrateAsync();

    foreach (var role in AppRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
            logger.LogInformation("Created role '{Role}'.", role);
        }
    }

    //admin account is NOT pre-created: the admin registers himself with Admin:Email
    //(Register.razor elevates the account on registration). If it already exists in the db,
    //just make sure the roles are assigned.
    var adminEmail = app.Configuration["Admin:Email"];
    if (!string.IsNullOrWhiteSpace(adminEmail))
    {
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin != null)
        {
            foreach (var role in AppRoles.All)
            {
                if (!await userManager.IsInRoleAsync(admin, role))
                    await userManager.AddToRoleAsync(admin, role);
            }
        }
        else
        {
            logger.LogInformation("Admin email '{Email}' configured - account will be elevated on registration.", adminEmail);
        }
    }

    //every account: 'user' role; group/unit leaders: 'groupleader' role
    //admins: EditGroupData is always forced true
    var profiles = await dbContext.Profiles.Include(p => p.LeadingGroup).ToListAsync();
    var profileIds = profiles.Select(p => p.ApplicationUserId).ToList();
    var leaderIds = await dbContext.Groups
        .Where(g => g.LeaderId != null)
        .Select(g => g.Leader!.ApplicationUserId)
        .ToListAsync();

    foreach (var appUserId in profileIds.Distinct())
    {
        var appUser = await userManager.FindByIdAsync(appUserId);
        if (appUser == null) continue;

        var isAdmin = await userManager.IsInRoleAsync(appUser, AppRoles.Admin);
        if (!isAdmin && !await userManager.IsInRoleAsync(appUser, AppRoles.User))
            await userManager.AddToRoleAsync(appUser, AppRoles.User);

        //unit leaders lead too
        var leadsUnit = await dbContext.Units.AnyAsync(u => u.Leader != null && u.Leader.ApplicationUserId == appUserId);

        if ((leaderIds.Contains(appUserId) || leadsUnit) && !isAdmin
            && !await userManager.IsInRoleAsync(appUser, AppRoles.GroupLeader))
        {
            await userManager.AddToRoleAsync(appUser, AppRoles.GroupLeader);
        }

        //admins always keep EditGroupData = true
        if (isAdmin)
        {
            var profile = profiles.FirstOrDefault(p => p.ApplicationUserId == appUserId);
            if (profile != null && !profile.EditGroupData)
            {
                profile.EditGroupData = true;
            }
        }
    }

    await dbContext.SaveChangesAsync();
}