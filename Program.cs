using TippspielWeb.Components;
using TippspielWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Port-Konfiguration für Render.com (nutzt $PORT Umgebungsvariable)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Supabase Service für Datenbankzugriff
builder.Services.AddSingleton<SupabaseService>();

// Tippspiel Service als Singleton registrieren
builder.Services.AddSingleton<TippspielService>();
builder.Services.AddSingleton<AuthService>();

// HttpClient für API-Calls
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<OpenLigaDBService>();

// Live-Update Hintergrund-Service
builder.Services.AddHostedService<LiveUpdateService>();

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
