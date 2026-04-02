using TippspielWeb.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace TippspielWeb.Services
{
    public class TippspielService
    {
        private readonly SupabaseService _supabaseService;
        private readonly LiveUpdateService _liveUpdateService;
        private readonly ILogger<TippspielService> _logger;

        // Lokaler Cache für häufig benötigte Daten. Quelle der Wahrheit bleibt Supabase.
        private ConcurrentDictionary<string, Benutzer> _benutzerCache = new();
        private ConcurrentDictionary<string, Mannschaft> _mannschaftenCache = new();
        private ConcurrentDictionary<int, Spiel> _spieleCache = new();
        private ConcurrentDictionary<string, List<Tipp>> _userTippsCache = new(); // Cache für alle Tipps eines Benutzers

        private readonly string ADMIN_PASSWORT = "admin123"; // Standard-Admin-Passwort

        public TippspielService(SupabaseService supabaseService, LiveUpdateService liveUpdateService, ILogger<TippspielService> logger)
        {
            _supabaseService = supabaseService;
            _liveUpdateService = liveUpdateService;
            _logger = logger;
            InitializeServiceAsync().Wait(); // Blockieren für den Initialisierungsprozess
        }

        private async Task InitializeServiceAsync()
        {
            await LadeAlleDatenAusSupabase();
        }

        private async Task LadeAlleDatenAusSupabase()
        {
            _logger.LogInformation("Lade alle Daten aus Supabase...");
            var benutzerList = await _supabaseService.GetBenutzer();
            _benutzerCache = new ConcurrentDictionary<string, Benutzer>(benutzerList.ToDictionary(b => b.Benutzername));

            var mannschaftenList = await _supabaseService.GetMannschaften();
            _mannschaftenCache = new ConcurrentDictionary<string, Mannschaft>(mannschaftenList.ToDictionary(m => m.Name));

            var spieleList = await _supabaseService.GetSpiele();
            _spieleCache = new ConcurrentDictionary<int, Spiel>(spieleList.ToDictionary(s => s.SpielId));

            // Tipps werden spiel- oder benutzerspezifisch geladen, nicht alle auf einmal
            _logger.LogInformation("Daten aus Supabase geladen.");
        }

        private void InvalidateCache()
        {
            _benutzerCache.Clear();
            _mannschaftenCache.Clear();
            _spieleCache.Clear();
            _userTippsCache.Clear();
            // Alle Daten werden bei Bedarf neu aus der Datenbank geladen.
            // Eine feinere Cache-Invalidierung könnte hier implementiert werden.
            LadeAlleDatenAusSupabase().Wait(); // Erneut blockieren oder als Task ausführen
        }

        // Benutzerverwaltung
        public async Task<bool> RegistriereBenutzer(string benutzername, string passwort)
        {
            if (string.IsNullOrWhiteSpace(benutzername) || string.IsNullOrWhiteSpace(passwort))
                return false;

            if (await GetBenutzer(benutzername) != null)
                return false; // Benutzername bereits vergeben

            var neuerBenutzer = new Benutzer
            {
                Benutzername = benutzername,
                PasswortHash = passwort.HashPassword(),
                RegistriertAm = DateTime.Now,
                IstAdmin = false,
                Punkte = 0
            };

            await _supabaseService.AddBenutzer(neuerBenutzer);
            _benutzerCache.TryAdd(neuerBenutzer.Benutzername, neuerBenutzer);
            _logger.LogInformation("Benutzer {Benutzername} registriert.", benutzername);
            return true;
        }

        public async Task<Benutzer?> GetBenutzer(string benutzername)
        {
            if (_benutzerCache.TryGetValue(benutzername, out var benutzer)) return benutzer;

            var allBenutzer = await _supabaseService.GetBenutzer(); // Re-fetch all to update cache if missing
            _benutzerCache = new ConcurrentDictionary<string, Benutzer>(allBenutzer.ToDictionary(b => b.Benutzername));

            _benutzerCache.TryGetValue(benutzername, out benutzer); // Try again after refresh
            return benutzer;
        }

        public async Task<bool> BestaetigeLogin(string benutzername, string passwort)
        {
            var benutzer = await GetBenutzer(benutzername);
            if (benutzer == null) return false;

            return benutzer.PasswortHash.VerifyPassword(passwort);
        }

        public async Task<bool> BestaetigeAdminPasswort(string passwort)
        {
            // Das Admin-Passwort ist ein fixes Passwort, nicht an einen Benutzer gebunden.
            // Hier wird direkt der Hash des Admin-Passworts überprüft.
            return ADMIN_PASSWORT.HashPassword().VerifyPassword(passwort);
        }

        public async Task<List<Benutzer>> GetAllBenutzer()
        {
            // Stellt sicher, dass der Cache aktuell ist und gibt dann die Liste zurück
            await LadeAlleDatenAusSupabase(); 
            return _benutzerCache.Values.OrderByDescending(b => b.Punkte).ToList();
        }

        public async Task SetWeltmeisterTipp(string benutzername, string team)
        {
            var benutzer = await GetBenutzer(benutzername);
            if (benutzer == null) return;
            benutzer.WeltmeisterTipp = team;
            await _supabaseService.UpdateBenutzer(benutzer);
            _benutzerCache[benutzername] = benutzer; // Update cache
        }

        public async Task SetVizemeisterTipp(string benutzername, string team)
        {
            var benutzer = await GetBenutzer(benutzername);
            if (benutzer == null) return;
            benutzer.VizemeisterTipp = team;
            await _supabaseService.UpdateBenutzer(benutzer);
            _benutzerCache[benutzername] = benutzer; // Update cache
        }

        public async Task ResetBenutzerPasswort(string benutzername, string neuesPasswort)
        {
            var benutzer = await GetBenutzer(benutzername);
            if (benutzer == null) return;
            benutzer.PasswortHash = neuesPasswort.HashPassword();
            await _supabaseService.UpdateBenutzer(benutzer);
            _benutzerCache[benutzername] = benutzer; // Update cache
            _logger.LogInformation("Passwort für Benutzer {Benutzername} zurückgesetzt.", benutzername);
        }

        // Spielverwaltung
        public async Task AddSpiel(Spiel spiel)
        {
            // Heim- und Gastmannschaft hinzufügen, falls noch nicht vorhanden
            if (await GetMannschaft(spiel.Heimmannschaft) == null) await AddMannschaft(new Mannschaft { Name = spiel.Heimmannschaft });
            if (await GetMannschaft(spiel.Gastmannschaft) == null) await AddMannschaft(new Mannschaft { Name = spiel.Gastmannschaft });

            await _supabaseService.AddSpiel(spiel);
            InvalidateCache(); // SpielId wird durch DB vergeben, daher Cache neu laden
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.SpieleChanged);
            _logger.LogInformation("Spiel hinzugefügt: {Home} vs {Away}", spiel.Heimmannschaft, spiel.Gastmannschaft);
        }

        public async Task UpdateSpiel(Spiel spiel)
        {
            await _supabaseService.UpdateSpiel(spiel);
            _spieleCache[spiel.SpielId] = spiel; // Update cache
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.SpieleChanged);
            _logger.LogInformation("Spiel {SpielId} aktualisiert.", spiel.SpielId);
        }

        public async Task DeleteSpiel(int spielId)
        {
            await _supabaseService.DeleteSpiel(spielId);
            InvalidateCache();
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.SpieleChanged);
            _logger.LogInformation("Spiel {SpielId} gelöscht.", spielId);
        }

        public async Task<List<Spiel>> GetAlleSpiele()
        {
            if (_spieleCache.IsEmpty) await LadeAlleDatenAusSupabase(); // Ensure cache is populated
            return _spieleCache.Values.OrderBy(s => s.SpielDatum).ToList();
        }

        public async Task<Spiel?> GetSpiel(int spielId)
        {
            if (_spieleCache.TryGetValue(spielId, out var spiel)) return spiel;
            // Falls im Cache nicht gefunden, versuchen, es aus der DB zu laden und Cache aktualisieren
            var fetchedSpiel = await _supabaseService.GetSpielById(spielId);
            if (fetchedSpiel != null) _spieleCache.TryAdd(fetchedSpiel.SpielId, fetchedSpiel);
            return fetchedSpiel;
        }

        public async Task SetSpielErgebnis(int spielId, int heimTore, int gastTore)
        {
            var spiel = await GetSpiel(spielId);
            if (spiel == null) return;

            spiel.HeimTore = heimTore;
            spiel.GastTore = gastTore;

            await _supabaseService.UpdateSpiel(spiel);
            _spieleCache[spielId] = spiel; // Update cache
            await BerechnePunkteFuerSpiel(spiel);
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.ErgebnisseChanged);
            _logger.LogInformation("Ergebnis für Spiel {SpielId} eingetragen: {Home}:{Away}", spielId, heimTore, gastTore);
        }

        // Mannschaftsverwaltung
        public async Task AddMannschaft(Mannschaft mannschaft)
        {
            if (string.IsNullOrWhiteSpace(mannschaft.Name)) return;
            if (await GetMannschaft(mannschaft.Name) != null) return; // Mannschaft existiert bereits

            await _supabaseService.AddMannschaft(mannschaft);
            _mannschaftenCache.TryAdd(mannschaft.Name, mannschaft);
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.MannschaftenChanged);
            _logger.LogInformation("Mannschaft {Name} hinzugefügt.", mannschaft.Name);
        }

        public async Task<Mannschaft?> GetMannschaft(string name)
        {
            if (_mannschaftenCache.TryGetValue(name, out var mannschaft)) return mannschaft;

            var allMannschaften = await _supabaseService.GetMannschaften(); // Re-fetch all to update cache if missing
            _mannschaftenCache = new ConcurrentDictionary<string, Mannschaft>(allMannschaften.ToDictionary(m => m.Name));

            _mannschaftenCache.TryGetValue(name, out mannschaft);
            return mannschaft;
        }

        public async Task<List<Mannschaft>> GetAlleMannschaften()
        {
            if (_mannschaftenCache.IsEmpty) await LadeAlleDatenAusSupabase();
            return _mannschaftenCache.Values.OrderBy(m => m.Name).ToList();
        }

        public async Task DeleteMannschaft(string name)
        {
            await _supabaseService.DeleteMannschaft(name);
            _mannschaftenCache.TryRemove(name, out _);
            // Invalidate all related caches that might reference this team (e.g., games, users' tips)
            InvalidateCache(); 
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.MannschaftenChanged);
            _logger.LogInformation("Mannschaft {Name} gelöscht.", name);
        }

        // Tippverwaltung
        public async Task AddOrUpdateTipp(string benutzername, int spielId, int heimTore, int gastTore)
        {
            var spiel = await GetSpiel(spielId);
            if (spiel == null || spiel.IstGesperrt) return; // Tipp kann nicht mehr abgegeben werden

            var tipp = new Tipp
            {
                Benutzername = benutzername,
                SpielId = spielId,
                HeimTore = heimTore,
                GastTore = gastTore
            };

            await _supabaseService.AddOrUpdateTipp(tipp);
            _userTippsCache.TryRemove(benutzername, out _); // Invalidate user's tips cache
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.TippsChanged);
            _logger.LogInformation("Tipp von {Benutzername} für Spiel {SpielId} gespeichert: {Home}:{Away}", benutzername, spielId, heimTore, gastTore);
        }

        public async Task<Tipp?> GetTipp(string benutzername, int spielId)
        {
            var userTipps = await GetTippsFuerBenutzer(benutzername);
            return userTipps.FirstOrDefault(t => t.SpielId == spielId);
        }

        public async Task<List<Tipp>> GetTippsFuerBenutzer(string benutzername)
        {
            if (_userTippsCache.TryGetValue(benutzername, out var tipps)) return tipps;

            var fetchedTipps = await _supabaseService.GetAllTippsForUser(benutzername);
            _userTippsCache.TryAdd(benutzername, fetchedTipps);
            return fetchedTipps;
        }

        public async Task<Dictionary<string, Tipp>> GetAlleTippsFuerSpiel(int spielId)
        {
            var tippsList = await _supabaseService.GetTippsForSpiel(spielId);
            return tippsList.ToDictionary(t => t.Benutzername, t => t);
        }

        // Punkteberechnung
        private async Task BerechnePunkteFuerSpiel(Spiel spiel)
        {
            if (!spiel.IstBeendet) return;

            var allBenutzer = await GetAllBenutzer(); // Re-fetch all users to get latest points
            var tippsFuerSpiel = await GetAlleTippsFuerSpiel(spiel.SpielId);

            foreach (var benutzer in allBenutzer)
            {
                if (tippsFuerSpiel.TryGetValue(benutzer.Benutzername, out var tipp))
                {
                    int punkte = 0;
                    if (tipp.HeimTore == spiel.HeimTore && tipp.GastTore == spiel.GastTore)
                    {
                        punkte = 3; // Exakt richtig
                    }
                    else if ((tipp.HeimTore - tipp.GastTore) == (spiel.HeimTore - spiel.GastTore))
                    {
                        punkte = 2; // Differenz richtig
                    }
                    else if ((tipp.HeimTore > tipp.GastTore && spiel.HeimTore > spiel.GastTore) ||
                             (tipp.HeimTore < tipp.GastTore && spiel.HeimTore < spiel.GastTore) ||
                             (tipp.HeimTore == tipp.GastTore && spiel.HeimTore == spiel.GastTore))
                    {
                        punkte = 1; // Tendenz richtig
                    }

                    if (punkte > 0) {
                        // Update user's points directly
                        benutzer.Punkte += punkte;
                        await _supabaseService.UpdateBenutzer(benutzer);
                        _benutzerCache[benutzer.Benutzername] = benutzer; // Update cache
                    }
                }
            }
            _liveUpdateService.SendUpdate(LiveUpdateService.UpdateType.RanglisteChanged);
            _logger.LogInformation("Punkte für Spiel {SpielId} neu berechnet.", spiel.SpielId);
        }

        // Helper für Turniertipps (noch nicht implementiert, kann später implementiert werden)
        public List<string> GetTurnierTeams()
        {
            // Hier sollten die relevanten Mannschaften für den Turniertipp geladen werden
            // Beispiel: Eine feste Liste oder eine Liste aller Mannschaften
            return _mannschaftenCache.Keys.OrderBy(n => n).ToList();
        }
    }
}