using System.Text.Json.Serialization;

namespace TippspielWeb.Models;

// Klasse für einen Benutzer (mit Authentifizierung)
public class Benutzer
{
    public string Benutzername { get; set; } = string.Empty;
    public string PasswortHash { get; set; } = string.Empty;
    public DateTime RegistriertAm { get; set; } = DateTime.Now;
    public bool IstAdmin { get; set; } = false;
    public string? WeltmeisterTipp { get; set; }
    public string? VizemeisterTipp { get; set; }

    public Benutzer() { }

    public Benutzer(string benutzername, string passwortHash, bool istAdmin = false)
    {
        Benutzername = benutzername;
        PasswortHash = passwortHash;
        IstAdmin = istAdmin;
        RegistriertAm = DateTime.Now;
    }
}

// Klasse für eine Mannschaft
public class Mannschaft
{
    public string Name { get; set; } = string.Empty;

    public Mannschaft() { }

    public Mannschaft(string name)
    {
        Name = name;
    }

    public override string ToString()
    {
        return Name;
    }
}

// Klasse für einen Spieler
public class Spieler
{
    public string Name { get; set; } = string.Empty;
    public int Punkte { get; set; }

    public Spieler() { }

    public Spieler(string name)
    {
        Name = name;
        Punkte = 0;
    }

    public override string ToString()
    {
        return $"{Name} - Punkte: {Punkte}";
    }
}

// Klasse für ein Spiel
public class Spiel
{
    public int SpielNummer { get; set; }
    public string Spieltag { get; set; } = string.Empty;
    public string Heimmannschaft { get; set; } = string.Empty;
    public string Gastmannschaft { get; set; } = string.Empty;
    public DateTime SpielDatum { get; set; }
    public int? HeimTore { get; set; }
    public int? GastTore { get; set; }
    public Dictionary<string, Tipp> Tipps { get; set; } = new();

    public Spiel() { }

    public Spiel(int spielNummer, string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
    {
        SpielNummer = spielNummer;
        Spieltag = spieltag;
        Heimmannschaft = heimmannschaft;
        Gastmannschaft = gastmannschaft;
        SpielDatum = spielDatum;
        HeimTore = null;
        GastTore = null;
        Tipps = new Dictionary<string, Tipp>();
    }

    public void SetzeErgebnis(int heimTore, int gastTore)
    {
        HeimTore = heimTore;
        GastTore = gastTore;
    }

    public bool IstBeendet()
    {
        return HeimTore.HasValue && GastTore.HasValue;
    }

    public override string ToString()
    {
        string ergebnis = IstBeendet() ? $"{HeimTore}:{GastTore}" : "offen";
        string datum = SpielDatum.ToString("dd.MM.yyyy HH:mm");
        return $"ST{Spieltag}: {Heimmannschaft} vs {Gastmannschaft} - {datum} ({ergebnis})";
    }
}

// Klasse für einen Tipp
public class Tipp
{
    public string SpielerName { get; set; } = string.Empty;
    public int HeimTore { get; set; }
    public int GastTore { get; set; }

    public Tipp() { }

    public Tipp(string spielerName, int heimTore, int gastTore)
    {
        SpielerName = spielerName;
        HeimTore = heimTore;
        GastTore = gastTore;
    }

    public override string ToString()
    {
        return $"{HeimTore}:{GastTore}";
    }
}

// Klasse für JSON-Serialisierung
public class TippspielDaten
{
    public List<Benutzer> Benutzer { get; set; } = new();
    public List<Mannschaft> Mannschaften { get; set; } = new();
    public List<Spieler> Spieler { get; set; } = new();
    public List<Spiel> Spiele { get; set; } = new();
}
