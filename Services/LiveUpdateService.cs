using TippspielWeb.Models;

namespace TippspielWeb.Services;

public class LiveUpdateService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private Timer? _timer;
    private readonly ILogger<LiveUpdateService> _logger;
    private const int UPDATE_INTERVAL_MINUTES = 3; // Alle 3 Minuten aktualisieren

    public LiveUpdateService(IServiceProvider serviceProvider, ILogger<LiveUpdateService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Live Update Service gestartet - Updates alle {Minutes} Minuten", UPDATE_INTERVAL_MINUTES);
        
        // Starte Timer für automatische Updates
        _timer = new Timer(
            AktualisiereErgebnisse,
            null,
            TimeSpan.Zero, // Starte sofort
            TimeSpan.FromMinutes(UPDATE_INTERVAL_MINUTES)
        );

        return Task.CompletedTask;
    }

    private async void AktualisiereErgebnisse(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var tippspielService = scope.ServiceProvider.GetRequiredService<TippspielService>();
            var openLigaService = scope.ServiceProvider.GetRequiredService<OpenLigaDBService>();

            var alleSpiele = tippspielService.GetAlleSpiele();
            var heuteSpiele = alleSpiele.Where(s => 
                s.SpielDatum.Date == DateTime.Today && 
                !s.IstBeendet()
            ).ToList();

            if (!heuteSpiele.Any())
            {
                _logger.LogInformation("Keine offenen Spiele heute - Skip Update");
                return;
            }

            _logger.LogInformation("Aktualisiere {Count} Spiele von heute", heuteSpiele.Count);

            // Hole Live-Spiele von OpenLigaDB
            var liveSpiele = await openLigaService.GetLiveSpieleAsync();
            if (liveSpiele == null || !liveSpiele.Any())
            {
                _logger.LogWarning("Keine Live-Spiele von API erhalten");
                return;
            }

            int aktualisiert = 0;
            foreach (var spiel in heuteSpiele)
            {
                // Finde passendes Live-Spiel
                var liveSpiel = liveSpiele.FirstOrDefault(ls => 
                    ls.Team1.TeamName == spiel.Heimmannschaft &&
                    ls.Team2.TeamName == spiel.Gastmannschaft &&
                    ls.MatchDateTime.Date == spiel.SpielDatum.Date
                );

                if (liveSpiel != null && liveSpiel.MatchIsFinished)
                {
                    var (heim, gast) = liveSpiel.GetErgebnis();
                    if (heim.HasValue && gast.HasValue)
                    {
                        tippspielService.ErgebnisEintragen(spiel.SpielNummer, heim.Value, gast.Value);
                        aktualisiert++;
                        _logger.LogInformation("Ergebnis aktualisiert: {Heim} vs {Gast} = {HeimTore}:{GastTore}", 
                            spiel.Heimmannschaft, spiel.Gastmannschaft, heim, gast);
                    }
                }
            }

            if (aktualisiert > 0)
            {
                _logger.LogInformation("✅ {Count} Spiel-Ergebnisse wurden aktualisiert", aktualisiert);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim automatischen Ergebnis-Update");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Live Update Service gestoppt");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
