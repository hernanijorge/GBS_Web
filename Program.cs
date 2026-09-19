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
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<HistoryRepository>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// One-time seed: if TBL_APP_USER is empty, create the initial admin user
// from the same env var that used to be compared in plain text, hashed on
// the way in. Subsequent logins never touch this env var again.
using (var scope = app.Services.CreateScope())
{
    var seedUsername = app.Configuration["AppLogin:Username"] ?? "admin";
    var seedPassword = Environment.GetEnvironmentVariable("GBS_APP_PASSWORD");
    if (string.IsNullOrEmpty(seedPassword))
    {
        throw new InvalidOperationException("Variável de ambiente GBS_APP_PASSWORD não definida.");
    }

    var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
    await userRepository.EnsureSeedUserAsync(seedUsername, seedPassword);
}

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

app.MapPost("/account/login", async (HttpContext http, UserRepository userRepository, [FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl) =>
{
    if (await userRepository.ValidateCredentialsAsync(username, password))
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

app.MapGet("/shipments/{remessaRef}/pdf", async (string remessaRef, RemessaRepository remessaRepository) =>
{
    var items = await remessaRepository.GetItemsForReportAsync(remessaRef);
    var pdfBytes = ShipmentReportGenerator.GeneratePdf(items, remessaRef);
    return Results.File(pdfBytes, "application/pdf", $"{remessaRef}.pdf");
}).RequireAuthorization();

app.MapGet("/shipments/{remessaRef}/excel", async (string remessaRef, RemessaRepository remessaRepository) =>
{
    var items = await remessaRepository.GetItemsForReportAsync(remessaRef);
    var excelBytes = ShipmentReportGenerator.GenerateExcel(items, remessaRef);
    return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{remessaRef}.xlsx");
}).RequireAuthorization();

// Reports operate on the same search/status filter the Inventory page has
// active — the web equivalent of the desktop's "current filter" report mode
// (ColetarItensRelatorio's fallback when no custom list/checkbox selection is
// active). See docs/DEPLOY_LOG.md Fase E for why the custom-list/checkbox
// modes weren't ported.
app.MapGet("/inventory/report/pdf", async (string? search, string? status, EquipamentoRepository equipamentoRepository) =>
{
    var items = await equipamentoRepository.GetAllAsync(search, status);
    var pdfBytes = InventoryReportGenerator.GeneratePdf(items);
    return Results.File(pdfBytes, "application/pdf", $"InventoryReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
}).RequireAuthorization();

app.MapGet("/inventory/report/excel", async (string? search, string? status, EquipamentoRepository equipamentoRepository) =>
{
    var items = await equipamentoRepository.GetAllAsync(search, status);
    var excelBytes = InventoryReportGenerator.GenerateExcel(items);
    return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"InventoryReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
}).RequireAuthorization();

app.MapGet("/components/report/pdf", async (ComponentRepository componentRepository) =>
{
    var items = await componentRepository.GetAllAsync();
    var pdfBytes = ComponentReportGenerator.GeneratePdf(items);
    return Results.File(pdfBytes, "application/pdf", $"ComponentsReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
}).RequireAuthorization();

app.MapGet("/components/report/excel", async (ComponentRepository componentRepository) =>
{
    var items = await componentRepository.GetAllAsync();
    var excelBytes = ComponentReportGenerator.GenerateExcel(items);
    return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ComponentsReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
}).RequireAuthorization();

app.MapGet("/components/summary/pdf", async (ComponentRepository componentRepository) =>
{
    var items = await componentRepository.GetSummaryAsync();
    var pdfBytes = ComponentSummaryReportGenerator.GeneratePdf(items);
    return Results.File(pdfBytes, "application/pdf", $"ComponentSummary_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
}).RequireAuthorization();

app.MapGet("/components/summary/excel", async (ComponentRepository componentRepository) =>
{
    var items = await componentRepository.GetSummaryAsync();
    var excelBytes = ComponentSummaryReportGenerator.GenerateExcel(items);
    return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ComponentSummary_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
}).RequireAuthorization();

app.MapGet("/upgrades/report/excel", async (int? days, UpgradeRepository upgradeRepository) =>
{
    var rows = await upgradeRepository.GetUpgradesForClientReportAsync(days ?? 90);
    var excelBytes = UpgradeClientReportGenerator.GenerateExcel(rows);
    return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"UpgradeReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
}).RequireAuthorization();

app.MapGet("/inventory/{id:int}/history/pdf", async (int id, EquipamentoRepository equipamentoRepository, HistoryRepository historyRepository) =>
{
    var eq = await equipamentoRepository.GetByIdAsync(id);
    if (eq is null) return Results.NotFound();
    var upgrades = await historyRepository.GetUpgradesAsync(id);
    var shipments = await historyRepository.GetShipmentsAsync(id);
    var pdfBytes = HistoryReportGenerator.GeneratePdf(eq, upgrades, shipments);
    return Results.File(pdfBytes, "application/pdf", $"History_{eq.InternalUid}.pdf");
}).RequireAuthorization();

app.MapGet("/inventory/{id:int}/history/excel", async (int id, EquipamentoRepository equipamentoRepository, HistoryRepository historyRepository) =>
{
    var eq = await equipamentoRepository.GetByIdAsync(id);
    if (eq is null) return Results.NotFound();
    var upgrades = await historyRepository.GetUpgradesAsync(id);
    var shipments = await historyRepository.GetShipmentsAsync(id);
    var excelBytes = HistoryReportGenerator.GenerateExcel(eq, upgrades, shipments);
    return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"History_{eq.InternalUid}.xlsx");
}).RequireAuthorization();

app.MapGet("/import/quality/pdf", async (string? uids, string? file, EquipamentoRepository equipamentoRepository) =>
{
    var uidList = (uids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    var items = await equipamentoRepository.GetForImportAnalysisAsync(uidList);
    var pdfBytes = ImportQualityReportGenerator.GeneratePdf(items, file ?? "");
    return Results.File(pdfBytes, "application/pdf", $"ImportQuality_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
}).RequireAuthorization();

app.MapGet("/import/quality/excel", async (string? uids, string? file, EquipamentoRepository equipamentoRepository) =>
{
    var uidList = (uids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    var items = await equipamentoRepository.GetForImportAnalysisAsync(uidList);
    var excelBytes = ImportQualityReportGenerator.GenerateExcel(items, file ?? "");
    return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ImportQuality_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
}).RequireAuthorization();

app.Run();
