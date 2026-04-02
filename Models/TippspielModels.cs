using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TippspielWeb.Models
{
    public class TippspielDaten
    {
        // Diese Klasse wird nach der Migration zu Supabase/PostgreSQL
        // als Hauptdatenstruktur nicht mehr direkt verwendet.
        // Die Daten werden stattdessen direkt aus der Datenbank geladen und dort gespeichert.
        public List<Benutzer> Benutzer { get; set; } = new();
        public List<Mannschaft> Mannschaften { get; set; } = new();
        public List<Spiel> Spiele { get; set; } = new();
    }

    public class Benutzer
    {
        [Key]
        public string Benutzername { get; set; } = string.Empty;
        public string PasswortHash { get; set; } = string.Empty;
        public DateTime RegistriertAm { get; set; } = DateTime.UtcNow;
        public bool IstAdmin { get; set; } = false;
        public string? WeltmeisterTipp { get; set; }
        public string? VizemeisterTipp { get; set; }
        public int Punkte { get; set; } = 0; // Punkte werden direkt im Benutzerobjekt gespeichert
    }

    public class Mannschaft
    {
        [Key]
        public string Name { get; set; } = string.Empty;
    }

    public class Spiel
    {
        [Key]
        public int SpielId { get; set; } // Auto-incrementing primary key in DB
        public int SpielNummer
        {
            get => SpielId;
            set => SpielId = value;
        }
        public string Spieltag { get; set; } = string.Empty;
        public string Heimmannschaft { get; set; } = string.Empty;
        public string Gastmannschaft { get; set; } = string.Empty;
        public DateTime SpielDatum { get; set; }
        public int? HeimTore { get; set; }
        public int? GastTore { get; set; }

        // Tipps werden nun in einer separaten Tabelle gespeichert und hier
        // bei Bedarf geladen (z.B. für die Anzeige).
        // [JsonIgnore] // Nicht direkt serialisieren, da aus DB geladen
        // public Dictionary<string, Tipp> Tipps { get; set; } = new();

        [JsonIgnore]
        public Dictionary<string, Tipp> Tipps { get; set; } = new();

        [JsonIgnore]
        public bool IstGesperrt => SpielDatum.AddHours(-1) < DateTime.Now;

        public bool IstBeendet() => HeimTore.HasValue && GastTore.HasValue;

        public bool IstLive()
        {
            var now = DateTime.Now;
            return !IstBeendet() && now >= SpielDatum && now <= SpielDatum.AddHours(3);
        }
    }

    public class Tipp
    {
        [Key]
        public int TippId { get; set; } // Auto-incrementing primary key in DB
        public string Benutzername { get; set; } = string.Empty; // Foreign Key to Benutzer
        public int SpielId { get; set; } // Foreign Key to Spiel
        public int HeimTore { get; set; }
        public int GastTore { get; set; }

        // Navigation properties (optional, für ORMs, hier nicht direkt verwendet aber hilfreich für Verständnis)
        [JsonIgnore]
        public Benutzer? Benutzer { get; set; }
        [JsonIgnore]
        public Spiel? Spiel { get; set; }
    }

    public class Spieler
    {
        public string Name { get; set; } = string.Empty;
        public int Punkte { get; set; } = 0;
    }
}