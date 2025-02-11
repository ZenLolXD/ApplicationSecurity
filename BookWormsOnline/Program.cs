using BookWormsOnline.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHttpClient();

// ✅ Register HttpContextAccessor to enable session access
builder.Services.AddHttpContextAccessor();

// ✅ Configure MySQL DB context.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    new MySqlServerVersion(new Version(8, 0, 23))));

// ✅ Add session services with secure defaults.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15); // ⏳ Auto logout after 15 mins of inactivity
    options.Cookie.HttpOnly = true;  // Prevent client-side script access.
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Enforce HTTPS.
    options.Cookie.SameSite = SameSiteMode.Strict; // Mitigate CSRF.
});

// ✅ If authentication is used, register it.
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

// ✅ Handle Global Errors & Redirect to Error Pages
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/500"); // Redirects application errors to `/500`
    app.UseHsts();
}

// ✅ Middleware to Handle Status Codes (403, 404, etc.)
app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == 404)
    {
        context.Request.Path = "/404";
        await next();
    }
    else if (context.Response.StatusCode == 403)
    {
        context.Request.Path = "/403";
        await next();
    }
    else if (context.Response.StatusCode == 500)
    {
        context.Request.Path = "/500";
        await next();
    }
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ✅ Enable authentication middleware (needed if you use authentication)
app.UseAuthentication();

// ✅ Enable session middleware (must be before authorization!)
app.UseSession();

app.UseAuthorization();

// ✅ Map Razor Pages
app.MapRazorPages();

app.Run();
