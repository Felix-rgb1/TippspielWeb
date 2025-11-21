using Npgsql;
using TippspielWeb.Models;
using System.Data;

namespace TippspielWeb.Services;

public class SupabaseService
{
    private readonly string _connectionString;
    private readonly object _lock = new();

    public SupabaseService(IConfiguration configuration)
    {
        Console.WriteLine("=== SUPABASE SERVICE CONSTRUCTOR START ===");
        
        // Prüfe zuerst Environment Variable, dann appsettings.json
        var envConnectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING");
        var configConnectionString = configuration.GetConnectionString("Supabase");
        
        var rawConnectionString = !string.IsNullOrWhiteSpace(envConnectionString) 
            ? envConnectionString 
            : configConnectionString 
            ?? throw new InvalidOperationException("Supabase connection string not found in environment or appsettings");
        
        Console.WriteLine($"Connection String Quelle: {(!string.IsNullOrWhiteSpace(envConnectionString) ? "Environment Variable" : "appsettings.json")}");
        Console.WriteLine($"Raw Connection String: '{rawConnectionString}'");
        
        // Konvertiere PostgreSQL URI zu Npgsql Connection String Format
        // Von: postgresql://user:pass@host:port/database
        // Zu: Host=host;Port=port;Database=database;Username=user;Password=pass
        _connectionString = ConvertPostgresUriToConnectionString(rawConnectionString);
        
        Console.WriteLine($"Konvertierte Connection String: '{_connectionString}'");
        
        // Teste die Verbindung beim Start
        try
        {
            Console.WriteLine("Teste Datenbankverbindung...");
            using var conn = GetConnection();
            conn.Open();
            Console.WriteLine("✓ Datenbankverbindung erfolgreich getestet!");
            conn.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ WARNUNG: Datenbankverbindung fehlgeschlagen!");
            Console.WriteLine($"  - Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"  - Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  - Inner Exception: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"  - Inner Message: {ex.InnerException.Message}");
            }
            Console.WriteLine("Die Anwendung startet trotzdem, aber Datenbankoperationen werden fehlschlagen.");
        }
        
        Console.WriteLine("=== SUPABASE SERVICE CONSTRUCTOR END ===");
    }
    
    private static string ConvertPostgresUriToConnectionString(string uri)
    {
        // Wenn es bereits im richtigen Format ist (enthält "Host="), gib es direkt zurück
        if (uri.Contains("Host="))
        {
            return uri;
        }
        
        // Parse PostgreSQL URI: postgresql://user:password@host:port/database
        try
        {
            var parsedUri = new Uri(uri);
            var userInfo = parsedUri.UserInfo.Split(':');
            var username = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            var host = parsedUri.Host;
            var port = parsedUri.Port;
            var database = parsedUri.AbsolutePath.TrimStart('/');
            
            return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FEHLER beim Konvertieren der URI: {ex.Message}");
            // Fallback: versuche es trotzdem
            return uri;
        }
    }

    private NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    // Benutzer-Verwaltung
    public bool BenutzerRegistrieren(string benutzername, string passwortHash, bool istAdmin = false)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO benutzer (benutzername, passwort_hash, ist_admin) VALUES (@user, @hash, @admin) ON CONFLICT (benutzername) DO UPDATE SET passwort_hash = @hash", conn);
                cmd.Parameters.AddWithValue("user", benutzername);
                cmd.Parameters.AddWithValue("hash", passwortHash);
                cmd.Parameters.AddWithValue("admin", istAdmin);
                cmd.ExecuteNonQuery();
                
                Console.WriteLine($"Benutzer {benutzername} erfolgreich registriert");
                
                // Auch als Spieler registrieren (falls noch nicht vorhanden)
                using var cmd2 = new NpgsqlCommand(
                    "INSERT INTO spieler (name, punkte) VALUES (@name, 0) ON CONFLICT (name) DO NOTHING", conn);
                cmd2.Parameters.AddWithValue("name", benutzername);
                cmd2.ExecuteNonQuery();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei Registrierung: {ex.Message}");
                return false;
            }
        }
    }

    public Benutzer? BenutzerLogin(string benutzername, string passwortHash)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "SELECT passwort_hash, ist_admin FROM benutzer WHERE benutzername = @user", conn);
                cmd.Parameters.AddWithValue("user", benutzername);
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var gespeicherterHash = reader.GetString(0);
                    var istAdmin = reader.GetBoolean(1);
                    if (gespeicherterHash == passwortHash)
                    {
                        return new Benutzer(benutzername, passwortHash) { IstAdmin = istAdmin };
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Login: {ex.Message}");
                return null;
            }
        }
    }

    public List<Benutzer> GetAlleBenutzer()
    {
        lock (_lock)
        {
            var benutzer = new List<Benutzer>();
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "SELECT benutzername, passwort_hash, registriert_am, ist_admin, weltmeister_tipp, vizemeister_tipp FROM benutzer", conn);
                using var reader = cmd.ExecuteReader();
                
                while (reader.Read())
                {
                    benutzer.Add(new Benutzer
                    {
                        Benutzername = reader.GetString(0),
                        PasswortHash = reader.GetString(1),
                        RegistriertAm = reader.GetDateTime(2),
                        IstAdmin = reader.GetBoolean(3),
                        WeltmeisterTipp = reader.IsDBNull(4) ? null : reader.GetString(4),
                        VizemeisterTipp = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden der Benutzer: {ex.Message}");
            }
            return benutzer;
        }
    }

    // Mannschaften-Verwaltung
    public bool MannschaftHinzufuegen(string name)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO mannschaften (name) VALUES (@name) ON CONFLICT (name) DO NOTHING", conn);
                cmd.Parameters.AddWithValue("name", name);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Hinzufügen der Mannschaft: {ex.Message}");
                return false;
            }
        }
    }

    public List<Mannschaft> GetAlleMannschaften()
    {
        lock (_lock)
        {
            var mannschaften = new List<Mannschaft>();
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand("SELECT name FROM mannschaften ORDER BY name", conn);
                using var reader = cmd.ExecuteReader();
                
                while (reader.Read())
                {
                    mannschaften.Add(new Mannschaft(reader.GetString(0)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden der Mannschaften: {ex.Message}");
            }
            return mannschaften;
        }
    }

    public bool MannschaftLoeschen(string name)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand("DELETE FROM mannschaften WHERE name = @name", conn);
                cmd.Parameters.AddWithValue("name", name);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Löschen der Mannschaft: {ex.Message}");
                return false;
            }
        }
    }

    // Spieler-Verwaltung
    public List<Spieler> GetAlleSpieler()
    {
        lock (_lock)
        {
            var spieler = new List<Spieler>();
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand("SELECT name, punkte FROM spieler", conn);
                using var reader = cmd.ExecuteReader();
                
                while (reader.Read())
                {
                    spieler.Add(new Spieler
                    {
                        Name = reader.GetString(0),
                        Punkte = reader.GetInt32(1)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden der Spieler: {ex.Message}");
            }
            return spieler;
        }
    }

    public void SpielerPunkteAktualisieren(string name, int punkte)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "UPDATE spieler SET punkte = @punkte WHERE name = @name", conn);
                cmd.Parameters.AddWithValue("punkte", punkte);
                cmd.Parameters.AddWithValue("name", name);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Aktualisieren der Punkte: {ex.Message}");
            }
        }
    }

    // Spiele-Verwaltung
    public void SpielHinzufuegen(int spielNummer, string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                // Verwende COALESCE um sicherzustellen, dass spiel_nummer gesetzt wird
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO spiele (spiel_nummer, spieltag, heimmannschaft, gastmannschaft, spiel_datum) 
                      VALUES (COALESCE(@nr, (SELECT COALESCE(MAX(spiel_nummer), 0) + 1 FROM spiele)), @st, @heim, @gast, @datum) 
                      ON CONFLICT (spiel_nummer) DO UPDATE SET spieltag = @st, heimmannschaft = @heim, gastmannschaft = @gast, spiel_datum = @datum 
                      RETURNING spiel_nummer", conn);
                cmd.Parameters.AddWithValue("nr", spielNummer);
                cmd.Parameters.AddWithValue("st", spieltag);
                cmd.Parameters.AddWithValue("heim", heimmannschaft);
                cmd.Parameters.AddWithValue("gast", gastmannschaft);
                cmd.Parameters.AddWithValue("datum", spielDatum);
                
                var insertedId = cmd.ExecuteScalar();
                Console.WriteLine($"SpielHinzufuegen: Spiel #{insertedId} {heimmannschaft} vs {gastmannschaft} in DB gespeichert");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Hinzufügen des Spiels: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }

    public List<Spiel> GetAlleSpiele()
    {
        lock (_lock)
        {
            var spiele = new List<Spiel>();
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "SELECT spiel_nummer, spieltag, heimmannschaft, gastmannschaft, spiel_datum, heim_tore, gast_tore FROM spiele ORDER BY spiel_nummer", conn);
                using var reader = cmd.ExecuteReader();
                
                while (reader.Read())
                {
                    var spiel = new Spiel
                    {
                        SpielNummer = reader.GetInt32(0),
                        Spieltag = reader.GetString(1),
                        Heimmannschaft = reader.GetString(2),
                        Gastmannschaft = reader.GetString(3),
                        SpielDatum = reader.GetDateTime(4),
                        HeimTore = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        GastTore = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                    };
                    spiele.Add(spiel);
                }
                
                Console.WriteLine($"GetAlleSpiele: {spiele.Count} Spiele aus DB geladen");
                
                // Lade Tipps für alle Spiele
                conn.Close();
                conn.Open();
                using var cmd2 = new NpgsqlCommand(
                    "SELECT spiel_nummer, spieler_name, heim_tore, gast_tore FROM tipps", conn);
                using var reader2 = cmd2.ExecuteReader();
                
                while (reader2.Read())
                {
                    var spielNummer = reader2.GetInt32(0);
                    var spielerName = reader2.GetString(1);
                    var heimTore = reader2.GetInt32(2);
                    var gastTore = reader2.GetInt32(3);
                    
                    var spiel = spiele.FirstOrDefault(s => s.SpielNummer == spielNummer);
                    if (spiel != null)
                    {
                        spiel.Tipps[spielerName] = new Tipp(spielerName, heimTore, gastTore);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden der Spiele: {ex.Message}");
            }
            return spiele;
        }
    }

    public bool ErgebnisEintragen(int spielNummer, int heimTore, int gastTore)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "UPDATE spiele SET heim_tore = @heim, gast_tore = @gast WHERE spiel_nummer = @nr", conn);
                cmd.Parameters.AddWithValue("heim", heimTore);
                cmd.Parameters.AddWithValue("gast", gastTore);
                cmd.Parameters.AddWithValue("nr", spielNummer);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Eintragen des Ergebnisses: {ex.Message}");
                return false;
            }
        }
    }

    // Tipps-Verwaltung
    public bool TippAbgeben(int spielNummer, string spielerName, int heimTore, int gastTore)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO tipps (spiel_nummer, spieler_name, heim_tore, gast_tore) 
                      VALUES (@nr, @spieler, @heim, @gast)
                      ON CONFLICT (spiel_nummer, spieler_name) 
                      DO UPDATE SET heim_tore = @heim, gast_tore = @gast", conn);
                cmd.Parameters.AddWithValue("nr", spielNummer);
                cmd.Parameters.AddWithValue("spieler", spielerName);
                cmd.Parameters.AddWithValue("heim", heimTore);
                cmd.Parameters.AddWithValue("gast", gastTore);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Tipp abgeben: {ex.Message}");
                return false;
            }
        }
    }

    // Turniertipps
    public void TurniertippSpeichern(string spielerName, string? weltmeister, string? vizemeister)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "UPDATE benutzer SET weltmeister_tipp = @wm, vizemeister_tipp = @vm WHERE benutzername = @name", conn);
                cmd.Parameters.AddWithValue("wm", (object?)weltmeister ?? DBNull.Value);
                cmd.Parameters.AddWithValue("vm", (object?)vizemeister ?? DBNull.Value);
                cmd.Parameters.AddWithValue("name", spielerName);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Speichern des Turniertipps: {ex.Message}");
            }
        }
    }

    public (string? weltmeister, string? vizemeister) GetTurniertipp(string spielerName)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "SELECT weltmeister_tipp, vizemeister_tipp FROM benutzer WHERE benutzername = @name", conn);
                cmd.Parameters.AddWithValue("name", spielerName);
                
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return (
                        reader.IsDBNull(0) ? null : reader.GetString(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1)
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden des Turniertipps: {ex.Message}");
            }
            return (null, null);
        }
    }

    // Prahl-Aktionen
    public void PrahlAktionSpeichern(string spielerName, string nachricht, string spieltag, int platz, int punkte)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO prahl_aktionen (spieler_name, nachricht, spieltag, platz, punkte) VALUES (@name, @msg, @st, @platz, @punkte)", conn);
                cmd.Parameters.AddWithValue("name", spielerName);
                cmd.Parameters.AddWithValue("msg", nachricht);
                cmd.Parameters.AddWithValue("st", spieltag);
                cmd.Parameters.AddWithValue("platz", platz);
                cmd.Parameters.AddWithValue("punkte", punkte);
                cmd.ExecuteNonQuery();
                
                // Behalte nur die letzten 50
                using var cmd2 = new NpgsqlCommand(
                    @"DELETE FROM prahl_aktionen WHERE id NOT IN 
                      (SELECT id FROM prahl_aktionen ORDER BY zeitstempel DESC LIMIT 50)", conn);
                cmd2.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Speichern der Prahl-Aktion: {ex.Message}");
            }
        }
    }

    public List<PrahlAktion> GetPrahlAktionen(int anzahl = 10)
    {
        lock (_lock)
        {
            var aktionen = new List<PrahlAktion>();
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "SELECT spieler_name, nachricht, zeitstempel, spieltag, platz, punkte FROM prahl_aktionen ORDER BY zeitstempel DESC LIMIT @limit", conn);
                cmd.Parameters.AddWithValue("limit", anzahl);
                
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    aktionen.Add(new PrahlAktion
                    {
                        SpielerName = reader.GetString(0),
                        Nachricht = reader.GetString(1),
                        Zeitstempel = reader.GetDateTime(2),
                        Spieltag = reader.GetString(3),
                        Platz = reader.GetInt32(4),
                        Punkte = reader.GetInt32(5)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden der Prahl-Aktionen: {ex.Message}");
            }
            return aktionen;
        }
    }

    public bool BenutzerLoeschen(string benutzername)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                // Lösche Benutzer und Spieler
                using var cmd = new NpgsqlCommand("DELETE FROM benutzer WHERE benutzername = @name", conn);
                cmd.Parameters.AddWithValue("name", benutzername);
                cmd.ExecuteNonQuery();
                
                using var cmd2 = new NpgsqlCommand("DELETE FROM spieler WHERE name = @name", conn);
                cmd2.Parameters.AddWithValue("name", benutzername);
                cmd2.ExecuteNonQuery();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Löschen des Benutzers: {ex.Message}");
                return false;
            }
        }
    }

    public bool PasswortAendern(string benutzername, string neuerHash)
    {
        lock (_lock)
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                
                using var cmd = new NpgsqlCommand(
                    "UPDATE benutzer SET passwort_hash = @hash WHERE benutzername = @name", conn);
                cmd.Parameters.AddWithValue("hash", neuerHash);
                cmd.Parameters.AddWithValue("name", benutzername);
                return cmd.ExecuteNonQuery() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Ändern des Passworts: {ex.Message}");
                return false;
            }
        }
    }
}
