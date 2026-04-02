using Npgsql;
using TippspielWeb.Models;
using System.Security.Cryptography;
using System.Text;

namespace TippspielWeb.Services
{
    public class SupabaseService
    {
        private string _connectionString;
        private readonly ILogger<SupabaseService> _logger;
        private volatile bool _isAvailable = true;

        public SupabaseService(IConfiguration configuration, ILogger<SupabaseService> logger)
        {
            _logger = logger;

            var directConnection = configuration.GetConnectionString("Supabase");
            var poolerConnection = configuration.GetConnectionString("SupabasePooler");

            if (string.IsNullOrWhiteSpace(directConnection) && string.IsNullOrWhiteSpace(poolerConnection))
            {
                throw new InvalidOperationException("Keine Supabase ConnectionStrings gefunden (Supabase oder SupabasePooler).");
            }

            _connectionString = directConnection ?? poolerConnection!;

            try
            {
                InitializeDatabase().GetAwaiter().GetResult();
                _isAvailable = true;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(poolerConnection) && !string.Equals(_connectionString, poolerConnection, StringComparison.Ordinal))
                {
                    _logger.LogWarning(ex, "Direct-Verbindung fehlgeschlagen, versuche SupabasePooler...");
                    _connectionString = poolerConnection;

                    try
                    {
                        InitializeDatabase().GetAwaiter().GetResult();
                        _isAvailable = true;
                        _logger.LogInformation("Supabase-Verbindung über Pooler erfolgreich.");
                        return;
                    }
                    catch (Exception poolerEx)
                    {
                        _isAvailable = false;
                        _logger.LogError(poolerEx, "Supabase ist weder direkt noch über Pooler erreichbar. Die Anwendung läuft im eingeschränkten Modus.");
                        return;
                    }
                }

                _isAvailable = false;
                _logger.LogError(ex, "Supabase ist aktuell nicht erreichbar. Die Anwendung läuft im eingeschränkten Modus.");
            }
        }

        private async Task ExecuteNonQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            if (!_isAvailable)
            {
                throw new InvalidOperationException("Supabase nicht verfügbar.");
            }

            _logger.LogDebug("Executing SQL: {Sql}", sql);
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddRange(parameters);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _logger.LogError(ex, "Error executing SQL: {Sql}", sql);
                throw;
            }
        }

        private async Task<List<T>> ExecuteQueryAsync<T>(string sql, Func<NpgsqlDataReader, T> mapFunction, params NpgsqlParameter[] parameters)
        {
            if (!_isAvailable)
            {
                return new List<T>();
            }

            _logger.LogDebug("Executing Query SQL: {Sql}", sql);
            var results = new List<T>();
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddRange(parameters);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(mapFunction(reader));
                }
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _logger.LogError(ex, "Error executing query SQL: {Sql}", sql);
                throw;
            }
            return results;
        }

        public async Task InitializeDatabase()
        {
            _logger.LogInformation("Initializing database schema...");
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS Benutzer (
                    Benutzername VARCHAR(255) PRIMARY KEY,
                    PasswortHash TEXT NOT NULL,
                    RegistriertAm TIMESTAMP WITH TIME ZONE NOT NULL,
                    IstAdmin BOOLEAN NOT NULL DEFAULT FALSE,
                    WeltmeisterTipp VARCHAR(255),
                    VizemeisterTipp VARCHAR(255),
                    Punkte INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS Mannschaften (
                    Name VARCHAR(255) PRIMARY KEY
                );

                CREATE TABLE IF NOT EXISTS Spiele (
                    SpielId SERIAL PRIMARY KEY,
                    Spieltag VARCHAR(255) NOT NULL,
                    Heimmannschaft VARCHAR(255) NOT NULL,
                    Gastmannschaft VARCHAR(255) NOT NULL,
                    SpielDatum TIMESTAMP WITH TIME ZONE NOT NULL,
                    HeimTore INTEGER,
                    GastTore INTEGER
                );

                CREATE TABLE IF NOT EXISTS Tipps (
                    TippId SERIAL PRIMARY KEY,
                    Benutzername VARCHAR(255) NOT NULL REFERENCES Benutzer(Benutzername) ON DELETE CASCADE,
                    SpielId INTEGER NOT NULL REFERENCES Spiele(SpielId) ON DELETE CASCADE,
                    HeimTore INTEGER NOT NULL,
                    GastTore INTEGER NOT NULL,
                    UNIQUE (Benutzername, SpielId)
                );
            ";

            await ExecuteNonQueryAsync(createTableSql);
            _logger.LogInformation("Database schema initialized.");

            // Optionally, create an admin user if none exists
            if (!(await GetBenutzer()).Any(u => u.IstAdmin))
            {
                _logger.LogInformation("No admin user found. Creating default admin.");
                var adminUser = new Benutzer
                {
                    Benutzername = "admin",
                    PasswortHash = "admin123".HashPassword(), // Use a proper hashing utility here
                    RegistriertAm = DateTime.UtcNow,
                    IstAdmin = true
                };
                await AddBenutzer(adminUser);
            }
        }

        // Benutzer Operations
        public async Task<List<Benutzer>> GetBenutzer()
        {
            return await ExecuteQueryAsync("SELECT * FROM Benutzer", reader => new Benutzer
            {
                Benutzername = reader.GetString(0),
                PasswortHash = reader.GetString(1),
                RegistriertAm = reader.GetDateTime(2),
                IstAdmin = reader.GetBoolean(3),
                WeltmeisterTipp = reader.IsDBNull(4) ? null : reader.GetString(4),
                VizemeisterTipp = reader.IsDBNull(5) ? null : reader.GetString(5),
                Punkte = reader.GetInt32(6)
            });
        }

        public async Task AddBenutzer(Benutzer benutzer)
        {
            await ExecuteNonQueryAsync(
                "INSERT INTO Benutzer (Benutzername, PasswortHash, RegistriertAm, IstAdmin, WeltmeisterTipp, VizemeisterTipp, Punkte) VALUES (@username, @passwordHash, @registeredAt, @isAdmin, @wmTipp, @vmTipp, @punkte)",
                new NpgsqlParameter("username", benutzer.Benutzername),
                new NpgsqlParameter("passwordHash", benutzer.PasswortHash),
                new NpgsqlParameter("registeredAt", benutzer.RegistriertAm.ToUniversalTime()),
                new NpgsqlParameter("isAdmin", benutzer.IstAdmin),
                new NpgsqlParameter("wmTipp", benutzer.WeltmeisterTipp ?? (object)DBNull.Value),
                new NpgsqlParameter("vmTipp", benutzer.VizemeisterTipp ?? (object)DBNull.Value),
                new NpgsqlParameter("punkte", benutzer.Punkte)
            );
        }

        public async Task UpdateBenutzer(Benutzer benutzer)
        {
            await ExecuteNonQueryAsync(
                "UPDATE Benutzer SET PasswortHash = @passwordHash, RegistriertAm = @registeredAt, IstAdmin = @isAdmin, WeltmeisterTipp = @wmTipp, VizemeisterTipp = @vmTipp, Punkte = @punkte WHERE Benutzername = @username",
                new NpgsqlParameter("passwordHash", benutzer.PasswortHash),
                new NpgsqlParameter("registeredAt", benutzer.RegistriertAm.ToUniversalTime()),
                new NpgsqlParameter("isAdmin", benutzer.IstAdmin),
                new NpgsqlParameter("wmTipp", benutzer.WeltmeisterTipp ?? (object)DBNull.Value),
                new NpgsqlParameter("vmTipp", benutzer.VizemeisterTipp ?? (object)DBNull.Value),
                new NpgsqlParameter("punkte", benutzer.Punkte),
                new NpgsqlParameter("username", benutzer.Benutzername)
            );
        }

        // Mannschaft Operations
        public async Task<List<Mannschaft>> GetMannschaften()
        {
            return await ExecuteQueryAsync("SELECT * FROM Mannschaften", reader => new Mannschaft
            {
                Name = reader.GetString(0)
            });
        }

        public async Task AddMannschaft(Mannschaft mannschaft)
        {
            await ExecuteNonQueryAsync("INSERT INTO Mannschaften (Name) VALUES (@name) ON CONFLICT (Name) DO NOTHING",
                new NpgsqlParameter("name", mannschaft.Name));
        }

        public async Task DeleteMannschaft(string name)
        {
            await ExecuteNonQueryAsync("DELETE FROM Mannschaften WHERE Name = @name",
                new NpgsqlParameter("name", name));
        }

        // Spiel Operations
        public async Task<List<Spiel>> GetSpiele()
        {
            return await ExecuteQueryAsync("SELECT * FROM Spiele", reader => new Spiel
            {
                SpielId = reader.GetInt32(0),
                Spieltag = reader.GetString(1),
                Heimmannschaft = reader.GetString(2),
                Gastmannschaft = reader.GetString(3),
                SpielDatum = reader.GetDateTime(4),
                HeimTore = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                GastTore = reader.IsDBNull(6) ? null : reader.GetInt32(6)
            });
        }

        public async Task<Spiel?> GetSpielById(int spielId)
        {
            return (await ExecuteQueryAsync("SELECT * FROM Spiele WHERE SpielId = @spielId", reader => new Spiel
            {
                SpielId = reader.GetInt32(0),
                Spieltag = reader.GetString(1),
                Heimmannschaft = reader.GetString(2),
                Gastmannschaft = reader.GetString(3),
                SpielDatum = reader.GetDateTime(4),
                HeimTore = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                GastTore = reader.IsDBNull(6) ? null : reader.GetInt32(6)
            }, new NpgsqlParameter("spielId", spielId))).FirstOrDefault();
        }

        public async Task AddSpiel(Spiel spiel)
        {
            await ExecuteNonQueryAsync(
                "INSERT INTO Spiele (Spieltag, Heimmannschaft, Gastmannschaft, SpielDatum, HeimTore, GastTore) VALUES (@spieltag, @homeTeam, @awayTeam, @gameDate, @homeGoals, @awayGoals)",
                new NpgsqlParameter("spieltag", spiel.Spieltag),
                new NpgsqlParameter("homeTeam", spiel.Heimmannschaft),
                new NpgsqlParameter("awayTeam", spiel.Gastmannschaft),
                new NpgsqlParameter("gameDate", spiel.SpielDatum.ToUniversalTime()),
                new NpgsqlParameter("homeGoals", spiel.HeimTore ?? (object)DBNull.Value),
                new NpgsqlParameter("awayGoals", spiel.GastTore ?? (object)DBNull.Value)
            );
        }

        public async Task UpdateSpiel(Spiel spiel)
        {
            await ExecuteNonQueryAsync(
                "UPDATE Spiele SET Spieltag = @spieltag, Heimmannschaft = @homeTeam, Gastmannschaft = @awayTeam, SpielDatum = @gameDate, HeimTore = @homeGoals, GastTore = @awayGoals WHERE SpielId = @spielId",
                new NpgsqlParameter("spieltag", spiel.Spieltag),
                new NpgsqlParameter("homeTeam", spiel.Heimmannschaft),
                new NpgsqlParameter("awayTeam", spiel.Gastmannschaft),
                new NpgsqlParameter("gameDate", spiel.SpielDatum.ToUniversalTime()),
                new NpgsqlParameter("homeGoals", spiel.HeimTore ?? (object)DBNull.Value),
                new NpgsqlParameter("awayGoals", spiel.GastTore ?? (object)DBNull.Value),
                new NpgsqlParameter("spielId", spiel.SpielId)
            );
        }

        public async Task DeleteSpiel(int spielId)
        {
            await ExecuteNonQueryAsync("DELETE FROM Spiele WHERE SpielId = @spielId",
                new NpgsqlParameter("spielId", spielId));
        }

        // Tipp Operations
        public async Task<List<Tipp>> GetTippsForSpiel(int spielId)
        {
            return await ExecuteQueryAsync("SELECT * FROM Tipps WHERE SpielId = @spielId", reader => new Tipp
            {
                TippId = reader.GetInt32(0),
                Benutzername = reader.GetString(1),
                SpielId = reader.GetInt32(2),
                HeimTore = reader.GetInt32(3),
                GastTore = reader.GetInt32(4)
            }, new NpgsqlParameter("spielId", spielId));
        }

        public async Task<List<Tipp>> GetAllTippsForUser(string benutzername)
        {
            return await ExecuteQueryAsync("SELECT * FROM Tipps WHERE Benutzername = @benutzername", reader => new Tipp
            {
                TippId = reader.GetInt32(0),
                Benutzername = reader.GetString(1),
                SpielId = reader.GetInt32(2),
                HeimTore = reader.GetInt32(3),
                GastTore = reader.GetInt32(4)
            }, new NpgsqlParameter("benutzername", benutzername));
        }

        public async Task AddOrUpdateTipp(Tipp tipp)
        {
            await ExecuteNonQueryAsync(
                "INSERT INTO Tipps (Benutzername, SpielId, HeimTore, GastTore) VALUES (@username, @spielId, @homeGoals, @awayGoals) ON CONFLICT (Benutzername, SpielId) DO UPDATE SET HeimTore = @homeGoals, GastTore = @awayGoals",
                new NpgsqlParameter("username", tipp.Benutzername),
                new NpgsqlParameter("spielId", tipp.SpielId),
                new NpgsqlParameter("homeGoals", tipp.HeimTore),
                new NpgsqlParameter("awayGoals", tipp.GastTore)
            );
        }

        public async Task DeleteBenutzer(string benutzername)
        {
            await ExecuteNonQueryAsync(
                "DELETE FROM Benutzer WHERE Benutzername = @username",
                new NpgsqlParameter("username", benutzername)
            );
        }

        public async Task<bool> RenameBenutzer(string alterBenutzername, string neuerBenutzername)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var tx = await conn.BeginTransactionAsync();

                await using (var updateTipps = new NpgsqlCommand(
                    "UPDATE Tipps SET Benutzername = @newUsername WHERE Benutzername = @oldUsername", conn, tx))
                {
                    updateTipps.Parameters.AddWithValue("newUsername", neuerBenutzername);
                    updateTipps.Parameters.AddWithValue("oldUsername", alterBenutzername);
                    await updateTipps.ExecuteNonQueryAsync();
                }

                await using (var updateBenutzer = new NpgsqlCommand(
                    "UPDATE Benutzer SET Benutzername = @newUsername WHERE Benutzername = @oldUsername", conn, tx))
                {
                    updateBenutzer.Parameters.AddWithValue("newUsername", neuerBenutzername);
                    updateBenutzer.Parameters.AddWithValue("oldUsername", alterBenutzername);
                    var changedRows = await updateBenutzer.ExecuteNonQueryAsync();
                    if (changedRows == 0)
                    {
                        await tx.RollbackAsync();
                        return false;
                    }
                }

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming user from {Old} to {New}", alterBenutzername, neuerBenutzername);
                throw;
            }
        }
    }

    public static class AuthExtensions // Moved password hashing here for reusability
    {
        public static string HashPassword(this string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public static bool VerifyPassword(this string hashedPassword, string passwordToCheck)
        {
            return hashedPassword == passwordToCheck.HashPassword();
        }
    }
}