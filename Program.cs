using TippspielWeb.Components;
using TippspielWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Port-Konfiguration für Render.com (nutzt $PORT Umgebungsvariable)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Logging hinzufügen
builder.Services.AddLogging(configure => configure.AddConsole());

// Supabase Service für Datenbankzugriff registrieren
// Hier wird auch der ILogger für SupabaseService hinzugefügt
builder.Services.AddSingleton<SupabaseService>();

// Tippspiel Service als Singleton registrieren
// TippspielService hängt von SupabaseService und LiveUpdateService ab
builder.Services.AddSingleton<TippspielService>();
builder.Services.AddSingleton<AuthService>();

// HttpClient für API-Calls
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<OpenLigaDBService>();

// Live-Update Service als Singleton UND als HostedService
builder.Services.AddSingleton<LiveUpdateService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveUpdateService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// HTTPS-Umleitung für Netzwerkzugriff deaktiviert
// app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();