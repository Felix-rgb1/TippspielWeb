using System;
using Npgsql;

var connectionString = "Host=db.prjlfyahewmzqidfzmei.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=Asde2221;SSL Mode=Require;Trust Server Certificate=true;Timeout=30";

Console.WriteLine("Teste Verbindung zu Supabase...");
Console.WriteLine($"Connection String: {connectionString}");

try {
    using var conn = new NpgsqlConnection(connectionString);
    Console.WriteLine("Öffne Verbindung...");
    conn.Open();
    Console.WriteLine(" Verbindung erfolgreich!");
    
    using var cmd = new NpgsqlCommand("SELECT version()", conn);
    var version = cmd.ExecuteScalar();
    Console.WriteLine($"PostgreSQL Version: {version}");
    
    conn.Close();
} catch (Exception ex) {
    Console.WriteLine($" FEHLER: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    if (ex.InnerException != null) {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }
}
