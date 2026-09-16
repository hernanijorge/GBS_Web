using System.Security.Claims;
using GBS_Web.Components;
using GBS_Web.Data;
using GBS_Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ComponentRepository>();
builder.Services.AddSingleton<EquipamentoRepository>();
builder.Services.AddSingleton<DashboardRepository>();
builder.Services.AddSingleton<RemessaRepository>();
builder.Services.AddSingleton<UpgradeRepository>();
builder.Services.AddSingleton<ClienteRepository>();
builder.Services.AddSingleton<InvoiceRepository>();
builder.Services.AddSingleton<BackupRepository>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/account/login", async (HttpContext http, [FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl) =>
{
    var expectedUser = app.Configuration["AppLogin:Username"] ?? "admin";
    var expectedPassword = Environment.GetEnvironmentVariable("GBS_APP_PASSWORD");

    if (string.IsNullOrEmpty(expectedPassword))
    {
        throw new InvalidOperationException("Variável de ambiente GBS_APP_PASSWORD não definida.");
    }

    if (username == expectedUser && password == expectedPassword)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, username)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    return Results.Redirect("/login?error=1");
}).AllowAnonymous().DisableAntiforgery();

app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous().DisableAntiforgery();

app.MapGet("/invoices/{id:int}/pdf", async (int id, InvoiceRepository invoiceRepository) =>
{
    var data = await invoiceRepository.GetDocumentDataAsync(id);
    if (data is null)
    {
        return Results.NotFound();
    }

    var pdfBytes = InvoicePdfGenerator.Generate(data, null);
    return Results.File(pdfBytes, "application/pdf", $"{data.Invoice.InvoiceNumber}.pdf");
}).RequireAuthorization();

app.Run();
