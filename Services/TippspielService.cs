using TippspielWeb.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ClosedXML.Excel;

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

            try
            {
                InitializeServiceAsync().GetAwaiter().GetResult(); // Blockieren für den Initialisierungsprozess
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initialisierung ohne Datenbank gestartet. Caches bleiben zunächst leer.");
            }
        }

        private async Task InitializeServiceAsync()
        {
            await LadeAlleDatenAusSupabase();
            await MigrateFromJsonIfEmpty();
            await LadeAlleDatenAusSupabase();
        }

        private async Task MigrateFromJsonIfEmpty()
        {
            if (_spieleCache.Count > 0) return;

            var jsonPath = Path.Combine(AppContext.BaseDirectory, "tippspiel_daten.json");
            if (!File.Exists(jsonPath)) return;

            _logger.LogInformation("Starte JSON-Migration nach Supabase...");
            try
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var jsonData = JsonSerializer.Deserialize<JsonMigrationData>(jsonContent, options);
                if (jsonData == null) return;

                foreach (var benutzer in jsonData.Benutzer)
                {
                    try { await _supabaseService.AddBenutzer(benutzer); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Benutzer {Name} bereits vorhanden oder Fehler.", benutzer.Benutzername); }
                }

                foreach (var mannschaft in jsonData.Mannschaften)
                {
                    try { await _supabaseService.AddMannschaft(mannschaft); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Mannschaft {Name} Fehler.", mannschaft.Name); }
                }

                foreach (var spielJson in jsonData.Spiele)
                {
                    try
                    {
                        var spiel = new Spiel
                        {
                            Spieltag = spielJson.Spieltag,
                            Heimmannschaft = spielJson.Heimmannschaft,
                            Gastmannschaft = spielJson.Gastmannschaft,
                            SpielDatum = spielJson.SpielDatum,
                            HeimTore = spielJson.HeimTore,
                            GastTore = spielJson.GastTore
                        };
                        int neueSpielId = await _supabaseService.AddSpielAndGetId(spiel);

                        if (spielJson.Tipps != null)
                        {
                            foreach (var (benutzername, tippJson) in spielJson.Tipps)
                            {
                                try
                                {
                                    await _supabaseService.AddOrUpdateTipp(new Tipp
                                    {
                                        Benutzername = benutzername,
                                        SpielId = neueSpielId,
                                        HeimTore = tippJson.HeimTore,
                                        GastTore = tippJson.GastTore
                                    });
                                }
                                catch (Exception ex) { _logger.LogWarning(ex, "Tipp-Migration Fehler für {User}.", benutzername); }
                            }
                        }
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Spiel-Migration Fehler: {Home} vs {Away}.", spielJson.Heimmannschaft, spielJson.Gastmannschaft); }
                }

                _logger.LogInformation("JSON-Migration abgeschlossen.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler während JSON-Migration.");
            }
        }

        private class JsonMigrationData
        {
            public List<Benutzer> Benutzer { get; set; } = new();
            public List<Mannschaft> Mannschaften { get; set; } = new();
            public List<JsonSpielMigration> Spiele { get; set; } = new();
        }

        private class JsonSpielMigration
        {
            public string Spieltag { get; set; } = string.Empty;
            public string Heimmannschaft { get; set; } = string.Empty;
            public string Gastmannschaft { get; set; } = string.Empty;
            public DateTime SpielDatum { get; set; }
            public int? HeimTore { get; set; }
            public int? GastTore { get; set; }
            public Dictionary<string, JsonTippMigration>? Tipps { get; set; }
        }

        private class JsonTippMigration
        {
            public int HeimTore { get; set; }
            public int GastTore { get; set; }
        }

        private async Task LadeAlleDatenAusSupabase()
        {
            _logger.LogInformation("Lade alle Daten aus Supabase...");
            try
            {
                var benutzerList = await _supabaseService.GetBenutzer();
                _benutzerCache = new ConcurrentDictionary<string, Benutzer>(benutzerList.ToDictionary(b => b.Benutzername));

                var mannschaftenList = await _supabaseService.GetMannschaften();
                _mannschaftenCache = new ConcurrentDictionary<string, Mannschaft>(mannschaftenList.ToDictionary(m => m.Name));

                var spieleList = await _supabaseService.GetSpiele();
                _spieleCache = new ConcurrentDictionary<int, Spiel>(spieleList.ToDictionary(s => s.SpielId));

                // Tipps werden spiel- oder benutzerspezifisch geladen, nicht alle auf einmal
                _logger.LogInformation("Daten aus Supabase geladen.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supabase momentan nicht erreichbar. Es werden leere Daten angezeigt.");
            }
        }

        private void InvalidateCache()
        {
            _benutzerCache.Clear();
            _mannschaftenCache.Clear();
            _spieleCache.Clear();
            _userTippsCache.Clear();
            // Alle Daten werden bei Bedarf neu aus der Datenbank geladen.
            // Eine feinere Cache-Invalidierung könnte hier implementiert werden.
            try
            {
                LadeAlleDatenAusSupabase().GetAwaiter().GetResult(); // Erneut blockieren oder als Task ausführen
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache-Refresh ohne Datenbankverbindung übersprungen.");
            }
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

        public async Task<List<Benutzer>> GetAllBenutzerAsync()
        {
            // Stellt sicher, dass der Cache aktuell ist und gibt dann die Liste zurück
            await LadeAlleDatenAusSupabase(); 
            return _benutzerCache.Values.OrderByDescending(b => b.Punkte).ToList();
        }

        public List<Benutzer> GetAlleBenutzer()
        {
            return GetAllBenutzerAsync().GetAwaiter().GetResult();
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

        public List<Spiel> GetAlleSpiele()
        {
            if (_spieleCache.IsEmpty) LadeAlleDatenAusSupabase().GetAwaiter().GetResult(); // Ensure cache is populated
            return _spieleCache.Values.OrderBy(s => s.SpielDatum).ToList();
        }

        public Task<List<Spiel>> GetAlleSpieleAsync()
        {
            return Task.FromResult(GetAlleSpiele());
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

        public List<Mannschaft> GetAlleMannschaften()
        {
            if (_mannschaftenCache.IsEmpty) LadeAlleDatenAusSupabase().GetAwaiter().GetResult();
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
            if (!spiel.IstBeendet()) return;

            var allBenutzer = await GetAllBenutzerAsync(); // Re-fetch all users to get latest points
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

        // Legacy-API fuer bestehende Razor-Seiten
        public bool BenutzerExistiert(string benutzername)
        {
            return GetBenutzer(benutzername).GetAwaiter().GetResult() != null;
        }

        public bool BenutzerRegistrieren(string benutzername, string passwort)
        {
            return RegistriereBenutzer(benutzername, passwort).GetAwaiter().GetResult();
        }

        public Benutzer? BenutzerLogin(string benutzername, string passwort)
        {
            var gueltig = BestaetigeLogin(benutzername, passwort).GetAwaiter().GetResult();
            if (!gueltig)
            {
                return null;
            }

            return GetBenutzer(benutzername).GetAwaiter().GetResult();
        }

        public bool AdminLoginPruefen(string passwort)
        {
            return BestaetigeAdminPasswort(passwort).GetAwaiter().GetResult();
        }

        public List<Spieler> GetAlleSpieler()
        {
            return GetAlleBenutzer()
                .Select(b => new Spieler { Name = b.Benutzername, Punkte = b.Punkte })
                .OrderByDescending(s => s.Punkte)
                .ThenBy(s => s.Name)
                .ToList();
        }

        public bool SpielHinzufuegen(string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
        {
            var spiel = new Spiel
            {
                Spieltag = spieltag,
                Heimmannschaft = heimmannschaft,
                Gastmannschaft = gastmannschaft,
                SpielDatum = spielDatum
            };

            AddSpiel(spiel).GetAwaiter().GetResult();
            return true;
        }

        public bool SpielBearbeiten(int spielNummer, string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
        {
            var spiel = GetSpiel(spielNummer).GetAwaiter().GetResult();
            if (spiel == null)
            {
                return false;
            }

            spiel.Spieltag = spieltag;
            spiel.Heimmannschaft = heimmannschaft;
            spiel.Gastmannschaft = gastmannschaft;
            spiel.SpielDatum = spielDatum;
            UpdateSpiel(spiel).GetAwaiter().GetResult();
            return true;
        }

        public bool SpielLoeschen(int spielNummer)
        {
            var spiel = GetSpiel(spielNummer).GetAwaiter().GetResult();
            if (spiel == null)
            {
                return false;
            }

            DeleteSpiel(spielNummer).GetAwaiter().GetResult();
            return true;
        }

        public void ErgebnisEintragen(int spielNummer, int heimTore, int gastTore)
        {
            SetSpielErgebnis(spielNummer, heimTore, gastTore).GetAwaiter().GetResult();
        }

        public bool TippAbgeben(int spielNummer, string benutzername, int heimTore, int gastTore)
        {
            if (!IstTippMoeglich(spielNummer))
            {
                return false;
            }

            AddOrUpdateTipp(benutzername, spielNummer, heimTore, gastTore).GetAwaiter().GetResult();
            return true;
        }

        public bool IstTippMoeglich(int spielNummer)
        {
            var spiel = GetSpiel(spielNummer).GetAwaiter().GetResult();
            return spiel != null && !spiel.IstGesperrt;
        }

        public bool MannschaftHinzufuegen(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (GetMannschaft(name).GetAwaiter().GetResult() != null)
            {
                return false;
            }

            AddMannschaft(new Mannschaft { Name = name }).GetAwaiter().GetResult();
            return true;
        }

        public void MannschaftLoeschen(string name)
        {
            DeleteMannschaft(name).GetAwaiter().GetResult();
        }

        public bool MannschaftUmbenennen(string alterName, string neuerName)
        {
            if (string.IsNullOrWhiteSpace(alterName) || string.IsNullOrWhiteSpace(neuerName))
            {
                return false;
            }

            if (string.Equals(alterName, neuerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var alteMannschaft = GetMannschaft(alterName).GetAwaiter().GetResult();
            if (alteMannschaft == null)
            {
                return false;
            }

            if (GetMannschaft(neuerName).GetAwaiter().GetResult() != null)
            {
                return false;
            }

            AddMannschaft(new Mannschaft { Name = neuerName }).GetAwaiter().GetResult();

            var spiele = GetAlleSpiele();
            foreach (var spiel in spiele)
            {
                var geaendert = false;
                if (spiel.Heimmannschaft == alterName)
                {
                    spiel.Heimmannschaft = neuerName;
                    geaendert = true;
                }

                if (spiel.Gastmannschaft == alterName)
                {
                    spiel.Gastmannschaft = neuerName;
                    geaendert = true;
                }

                if (geaendert)
                {
                    UpdateSpiel(spiel).GetAwaiter().GetResult();
                }
            }

            DeleteMannschaft(alterName).GetAwaiter().GetResult();
            return true;
        }

        public bool BenutzernameAendern(string alterBenutzername, string neuerBenutzername)
        {
            if (string.IsNullOrWhiteSpace(alterBenutzername) || string.IsNullOrWhiteSpace(neuerBenutzername))
            {
                return false;
            }

            if (string.Equals(alterBenutzername, neuerBenutzername, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (BenutzerExistiert(neuerBenutzername))
            {
                return false;
            }

            return _supabaseService.RenameBenutzer(alterBenutzername, neuerBenutzername).GetAwaiter().GetResult();
        }

        public bool PasswortAendern(string benutzername, string altesPasswort, string neuesPasswort)
        {
            var benutzer = GetBenutzer(benutzername).GetAwaiter().GetResult();
            if (benutzer == null || !benutzer.PasswortHash.VerifyPassword(altesPasswort))
            {
                return false;
            }

            benutzer.PasswortHash = neuesPasswort.HashPassword();
            _supabaseService.UpdateBenutzer(benutzer).GetAwaiter().GetResult();
            _benutzerCache[benutzer.Benutzername] = benutzer;
            return true;
        }

        public bool PasswortAendernAlsAdmin(string benutzername, string neuesPasswort)
        {
            var benutzer = GetBenutzer(benutzername).GetAwaiter().GetResult();
            if (benutzer == null)
            {
                return false;
            }

            benutzer.PasswortHash = neuesPasswort.HashPassword();
            _supabaseService.UpdateBenutzer(benutzer).GetAwaiter().GetResult();
            _benutzerCache[benutzer.Benutzername] = benutzer;
            return true;
        }

        public bool BenutzerLoeschen(string benutzername)
        {
            var benutzer = GetBenutzer(benutzername).GetAwaiter().GetResult();
            if (benutzer == null)
            {
                return false;
            }

            _supabaseService.DeleteBenutzer(benutzername).GetAwaiter().GetResult();
            _benutzerCache.TryRemove(benutzername, out _);
            _userTippsCache.TryRemove(benutzername, out _);
            return true;
        }

        public void PunkteBerechnen()
        {
            var benutzer = GetAlleBenutzer();
            foreach (var b in benutzer)
            {
                b.Punkte = 0;
                _supabaseService.UpdateBenutzer(b).GetAwaiter().GetResult();
            }

            var beendeteSpiele = GetAlleSpiele().Where(s => s.IstBeendet()).ToList();
            foreach (var spiel in beendeteSpiele)
            {
                BerechnePunkteFuerSpiel(spiel).GetAwaiter().GetResult();
            }
        }

        public (string? Weltmeister, string? Vizemeister) GetTurniertipp(string benutzername)
        {
            var benutzer = GetBenutzer(benutzername).GetAwaiter().GetResult();
            if (benutzer == null)
            {
                return (null, null);
            }

            return (benutzer.WeltmeisterTipp, benutzer.VizemeisterTipp);
        }

        public void TurniertippSpeichern(string benutzername, string? weltmeister, string? vizemeister)
        {
            var benutzer = GetBenutzer(benutzername).GetAwaiter().GetResult();
            if (benutzer == null)
            {
                return;
            }

            benutzer.WeltmeisterTipp = weltmeister;
            benutzer.VizemeisterTipp = vizemeister;
            _supabaseService.UpdateBenutzer(benutzer).GetAwaiter().GetResult();
            _benutzerCache[benutzer.Benutzername] = benutzer;
        }

        public bool KannPrahlen(string spielerName, out string grund)
        {
            var ranking = GetAlleSpieler();
            if (ranking.Count == 0)
            {
                grund = "Noch keine Daten vorhanden.";
                return false;
            }

            var erster = ranking[0];
            var ich = ranking.FirstOrDefault(r => r.Name == spielerName);
            if (ich == null)
            {
                grund = "Spieler nicht gefunden.";
                return false;
            }

            if (ich.Name != erster.Name)
            {
                grund = "Nur der aktuell Erstplatzierte kann prahlen.";
                return false;
            }

            var beendeteSpiele = GetAlleSpiele().Count(s => s.IstBeendet());
            if (beendeteSpiele == 0)
            {
                grund = "Es gibt noch keine beendeten Spiele.";
                return false;
            }

            grund = string.Empty;
            return true;
        }

        public void PrahlAktionSpeichern(string spielerName, string nachricht)
        {
            _logger.LogInformation("Prahl-Aktion von {Spieler}: {Nachricht}", spielerName, nachricht);
            _liveUpdateService.TriggerPrahlAktion(spielerName, nachricht);
        }

        public byte[] ExportiereNachExcel()
        {
            using var workbook = new XLWorkbook();

            var benutzerSheet = workbook.Worksheets.Add("Benutzer");
            benutzerSheet.Cell(1, 1).Value = "Benutzername";
            benutzerSheet.Cell(1, 2).Value = "Punkte";
            var benutzer = GetAlleBenutzer();
            for (int i = 0; i < benutzer.Count; i++)
            {
                benutzerSheet.Cell(i + 2, 1).Value = benutzer[i].Benutzername;
                benutzerSheet.Cell(i + 2, 2).Value = benutzer[i].Punkte;
            }

            var spieleSheet = workbook.Worksheets.Add("Spiele");
            spieleSheet.Cell(1, 1).Value = "Spieltag";
            spieleSheet.Cell(1, 2).Value = "Heim";
            spieleSheet.Cell(1, 3).Value = "Gast";
            spieleSheet.Cell(1, 4).Value = "Datum";
            spieleSheet.Cell(1, 5).Value = "Ergebnis";
            var spiele = GetAlleSpiele();
            for (int i = 0; i < spiele.Count; i++)
            {
                var s = spiele[i];
                spieleSheet.Cell(i + 2, 1).Value = s.Spieltag;
                spieleSheet.Cell(i + 2, 2).Value = s.Heimmannschaft;
                spieleSheet.Cell(i + 2, 3).Value = s.Gastmannschaft;
                spieleSheet.Cell(i + 2, 4).Value = s.SpielDatum;
                spieleSheet.Cell(i + 2, 5).Value = s.IstBeendet() ? $"{s.HeimTore}:{s.GastTore}" : "-";
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] ExportiereNachExcelFuerSpieler(string spielerName)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Meine Tipps");

            sheet.Cell(1, 1).Value = "Spieltag";
            sheet.Cell(1, 2).Value = "Heim";
            sheet.Cell(1, 3).Value = "Gast";
            sheet.Cell(1, 4).Value = "Mein Tipp";
            sheet.Cell(1, 5).Value = "Ergebnis";

            var spiele = GetAlleSpiele();
            var tipps = GetTippsFuerBenutzer(spielerName).GetAwaiter().GetResult().ToDictionary(t => t.SpielId, t => t);

            int row = 2;
            foreach (var spiel in spiele)
            {
                sheet.Cell(row, 1).Value = spiel.Spieltag;
                sheet.Cell(row, 2).Value = spiel.Heimmannschaft;
                sheet.Cell(row, 3).Value = spiel.Gastmannschaft;
                if (tipps.TryGetValue(spiel.SpielId, out var tipp))
                {
                    sheet.Cell(row, 4).Value = $"{tipp.HeimTore}:{tipp.GastTore}";
                }
                else
                {
                    sheet.Cell(row, 4).Value = "-";
                }

                sheet.Cell(row, 5).Value = spiel.IstBeendet() ? $"{spiel.HeimTore}:{spiel.GastTore}" : "-";
                row++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}