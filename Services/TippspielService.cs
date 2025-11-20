using System.Text.Json;
using TippspielWeb.Models;
using ClosedXML.Excel;

namespace TippspielWeb.Services;

public class TippspielService
{
    private const string SPEICHER_DATEI = "tippspiel_daten.json";
    private const string ADMIN_PASSWORT = "admin123";
    
    private List<Benutzer> benutzerListe = new();
    private List<Mannschaft> mannschaftsListe = new();
    private List<Spieler> spielerListe = new();
    private List<Spiel> spieleListe = new();
    private List<PrahlAktion> prahlAktionen = new();
    private readonly object _lock = new();

    public TippspielService()
    {
        LadeDaten();
        // Berechne Punkte beim Start neu, falls bereits Ergebnisse vorhanden sind
        PunkteBerechnen();
    }

    // Mannschafts-Verwaltung
    public List<Mannschaft> GetAlleMannschaften()
    {
        lock (_lock)
        {
            return mannschaftsListe.OrderBy(m => m.Name).ToList();
        }
    }

    public bool MannschaftHinzufuegen(string name)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (mannschaftsListe.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false;

            mannschaftsListe.Add(new Mannschaft(name.Trim()));
            SpeichereDaten();
            return true;
        }
    }

    public bool MannschaftLoeschen(string name)
    {
        lock (_lock)
        {
            var mannschaft = mannschaftsListe.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (mannschaft == null) return false;

            mannschaftsListe.Remove(mannschaft);
            SpeichereDaten();
            return true;
        }
    }

    public bool MannschaftUmbenennen(string alterName, string neuerName)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(neuerName))
                return false;

            var mannschaft = mannschaftsListe.FirstOrDefault(m => m.Name.Equals(alterName, StringComparison.OrdinalIgnoreCase));
            if (mannschaft == null) return false;

            // Prüfe ob neuer Name schon existiert
            if (!alterName.Equals(neuerName, StringComparison.OrdinalIgnoreCase) &&
                mannschaftsListe.Any(m => m.Name.Equals(neuerName, StringComparison.OrdinalIgnoreCase)))
                return false;

            mannschaft.Name = neuerName.Trim();
            SpeichereDaten();
            return true;
        }
    }

    // Spieler-Verwaltung
    public List<Spieler> GetAlleSpieler()
    {
        lock (_lock)
        {
            return spielerListe.ToList();
        }
    }

    public bool SpielerHinzufuegen(string name)
    {
        lock (_lock)
        {
            if (spielerListe.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false;

            spielerListe.Add(new Spieler(name));
            SpeichereDaten();
            return true;
        }
    }

    public bool SpielerLoeschen(string name)
    {
        lock (_lock)
        {
            var spieler = spielerListe.FirstOrDefault(s => s.Name == name);
            if (spieler == null) return false;

            spielerListe.Remove(spieler);
            
            // Tipps des Spielers aus allen Spielen entfernen
            foreach (var spiel in spieleListe)
            {
                spiel.Tipps.Remove(name);
            }
            
            SpeichereDaten();
            return true;
        }
    }

    // Spiel-Verwaltung
    public List<Spiel> GetAlleSpiele()
    {
        lock (_lock)
        {
            return spieleListe.OrderBy(s => s.Spieltag).ThenBy(s => s.SpielDatum).ToList();
        }
    }

    public void SpielHinzufuegen(string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
    {
        lock (_lock)
        {
            int neueSpielNr = spieleListe.Count > 0 ? spieleListe.Max(s => s.SpielNummer) + 1 : 1;
            var spiel = new Spiel(neueSpielNr, spieltag, heimmannschaft, gastmannschaft, spielDatum);
            spieleListe.Add(spiel);
            SpeichereDaten();
        }
    }

    public bool SpielBearbeiten(int spielNummer, string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
    {
        lock (_lock)
        {
            var spiel = spieleListe.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null) return false;

            // Erstelle ein neues Spiel-Objekt mit den aktualisierten Daten aber behalte die Tipps
            var tipps = spiel.Tipps;
            var heimTore = spiel.HeimTore;
            var gastTore = spiel.GastTore;
            
            spieleListe.Remove(spiel);
            var neuesSpiel = new Spiel(spielNummer, spieltag, heimmannschaft, gastmannschaft, spielDatum);
            
            // Übertrage Ergebnis und Tipps
            if (heimTore.HasValue && gastTore.HasValue)
                neuesSpiel.SetzeErgebnis(heimTore.Value, gastTore.Value);
            
            foreach (var tipp in tipps)
                neuesSpiel.Tipps[tipp.Key] = tipp.Value;
            
            spieleListe.Add(neuesSpiel);
            SpeichereDaten();
            return true;
        }
    }

    public bool SpielLoeschen(int spielNummer)
    {
        lock (_lock)
        {
            var spiel = spieleListe.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null) return false;

            spieleListe.Remove(spiel);
            SpeichereDaten();
            PunkteBerechnen();
            return true;
        }
    }

    public bool ErgebnisEintragen(int spielNummer, int heimTore, int gastTore)
    {
        lock (_lock)
        {
            var spiel = spieleListe.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null) return false;

            spiel.SetzeErgebnis(heimTore, gastTore);
            PunkteBerechnen();
            return true;
        }
    }

    // Tipp-Verwaltung
    public bool TippAbgeben(int spielNummer, string spielerName, int heimTore, int gastTore)
    {
        lock (_lock)
        {
            var spiel = spieleListe.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null || spiel.IstBeendet()) return false;

            // Prüfe ob das Spiel in weniger als 1 Stunde beginnt
            if (DateTime.Now >= spiel.SpielDatum.AddHours(-1))
                return false;

            var tipp = new Tipp(spielerName, heimTore, gastTore);
            spiel.Tipps[spielerName] = tipp;
            SpeichereDaten();
            return true;
        }
    }

    public bool IstTippMoeglich(int spielNummer)
    {
        lock (_lock)
        {
            var spiel = spieleListe.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null || spiel.IstBeendet()) return false;
            
            // Tipp ist möglich wenn noch mehr als 1 Stunde bis Spielbeginn
            return DateTime.Now < spiel.SpielDatum.AddHours(-1);
        }
    }

    public Dictionary<int, Tipp> GetSpielerTipps(string spielerName)
    {
        lock (_lock)
        {
            return spieleListe
                .Where(s => s.Tipps.ContainsKey(spielerName))
                .ToDictionary(s => s.SpielNummer, s => s.Tipps[spielerName]);
        }
    }

    // Punkte berechnen
    public void PunkteBerechnen()
    {
        lock (_lock)
        {
            foreach (var spieler in spielerListe)
            {
                spieler.Punkte = 0;
            }

            foreach (var spiel in spieleListe.Where(s => s.IstBeendet()))
            {
                foreach (var tippEntry in spiel.Tipps)
                {
                    var spieler = spielerListe.FirstOrDefault(s => s.Name == tippEntry.Key);
                    if (spieler != null)
                    {
                        int punkte = BerechneTippPunkte(tippEntry.Value, spiel);
                        spieler.Punkte += punkte;
                    }
                }
            }

            SpeichereDaten();
        }
    }

    private int BerechneTippPunkte(Tipp tipp, Spiel spiel)
    {
        if (!spiel.IstBeendet()) return 0;

        int heimTore = spiel.HeimTore ?? 0;
        int gastTore = spiel.GastTore ?? 0;

        if (tipp.HeimTore == heimTore && tipp.GastTore == gastTore)
            return 3;

        int tippDiff = tipp.HeimTore - tipp.GastTore;
        int spielDiff = heimTore - gastTore;

        if (tippDiff == spielDiff)
            return 2;

        if ((tippDiff > 0 && spielDiff > 0) || 
            (tippDiff == 0 && spielDiff == 0) || 
            (tippDiff < 0 && spielDiff < 0))
            return 1;

        return 0;
    }

    // Benutzer-Verwaltung
    public bool BenutzerRegistrieren(string benutzername, string passwort)
    {
        lock (_lock)
        {
            if (benutzerListe.Any(b => b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase)))
                return false;

            string passwortHash = AuthService.HashPasswort(passwort);
            benutzerListe.Add(new Benutzer(benutzername, passwortHash));
            
            // Automatisch einen Spieler mit dem gleichen Namen erstellen
            spielerListe.Add(new Spieler(benutzername));
            
            SpeichereDaten();
            return true;
        }
    }

    public Benutzer? BenutzerLogin(string benutzername, string passwort)
    {
        lock (_lock)
        {
            var benutzer = benutzerListe.FirstOrDefault(b => 
                b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            if (benutzer != null && AuthService.VerifyPasswort(passwort, benutzer.PasswortHash))
                return benutzer;
            
            return null;
        }
    }

    public bool BenutzerExistiert(string benutzername)
    {
        lock (_lock)
        {
            return benutzerListe.Any(b => b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
        }
    }

    public List<Benutzer> GetAlleBenutzer()
    {
        lock (_lock)
        {
            return benutzerListe.ToList();
        }
    }

    public bool BenutzernameAendern(string alterBenutzername, string neuerBenutzername)
    {
        lock (_lock)
        {
            // Prüfe ob neuer Name schon existiert (außer es ist der gleiche Benutzer)
            if (!alterBenutzername.Equals(neuerBenutzername, StringComparison.OrdinalIgnoreCase) &&
                benutzerListe.Any(b => b.Benutzername.Equals(neuerBenutzername, StringComparison.OrdinalIgnoreCase)))
                return false;

            var benutzer = benutzerListe.FirstOrDefault(b => 
                b.Benutzername.Equals(alterBenutzername, StringComparison.OrdinalIgnoreCase));
            
            if (benutzer == null) return false;

            // Ändere Benutzername
            benutzer.Benutzername = neuerBenutzername;

            // Ändere zugehörigen Spielernamen
            var spieler = spielerListe.FirstOrDefault(s => 
                s.Name.Equals(alterBenutzername, StringComparison.OrdinalIgnoreCase));
            if (spieler != null)
            {
                spieler.Name = neuerBenutzername;
            }

            // Aktualisiere alle Tipps mit dem neuen Namen
            foreach (var spiel in spieleListe)
            {
                if (spiel.Tipps.ContainsKey(alterBenutzername))
                {
                    var tipp = spiel.Tipps[alterBenutzername];
                    tipp.SpielerName = neuerBenutzername;
                    spiel.Tipps.Remove(alterBenutzername);
                    spiel.Tipps[neuerBenutzername] = tipp;
                }
            }

            SpeichereDaten();
            return true;
        }
    }

    public bool PasswortAendern(string benutzername, string altesPasswort, string neuesPasswort)
    {
        lock (_lock)
        {
            var benutzer = benutzerListe.FirstOrDefault(b => 
                b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            if (benutzer == null) return false;

            // Prüfe altes Passwort
            if (!AuthService.VerifyPasswort(altesPasswort, benutzer.PasswortHash))
                return false;

            // Setze neues Passwort
            benutzer.PasswortHash = AuthService.HashPasswort(neuesPasswort);
            SpeichereDaten();
            return true;
        }
    }

    public bool PasswortAendernAlsAdmin(string benutzername, string neuesPasswort)
    {
        lock (_lock)
        {
            var benutzer = benutzerListe.FirstOrDefault(b => 
                b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            if (benutzer == null) return false;

            benutzer.PasswortHash = AuthService.HashPasswort(neuesPasswort);
            SpeichereDaten();
            return true;
        }
    }

    public bool BenutzerLoeschen(string benutzername)
    {
        lock (_lock)
        {
            var benutzer = benutzerListe.FirstOrDefault(b => 
                b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            if (benutzer == null) return false;

            benutzerListe.Remove(benutzer);

            // Lösche auch zugehörigen Spieler
            var spieler = spielerListe.FirstOrDefault(s => 
                s.Name.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            if (spieler != null)
            {
                spielerListe.Remove(spieler);
            }

            // Entferne alle Tipps des Benutzers
            foreach (var spiel in spieleListe)
            {
                spiel.Tipps.Remove(benutzername);
            }

            SpeichereDaten();
            return true;
        }
    }

    // Turniertipps (Weltmeister/Vizemeister)
    public void TurniertippSpeichern(string benutzername, string? weltmeister, string? vizemeister)
    {
        lock (_lock)
        {
            var benutzer = benutzerListe.FirstOrDefault(b => 
                b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            if (benutzer != null)
            {
                benutzer.WeltmeisterTipp = weltmeister;
                benutzer.VizemeisterTipp = vizemeister;
                SpeichereDaten();
            }
        }
    }

    public (string? weltmeister, string? vizemeister) GetTurniertipp(string benutzername)
    {
        lock (_lock)
        {
            var benutzer = benutzerListe.FirstOrDefault(b => 
                b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            return (benutzer?.WeltmeisterTipp, benutzer?.VizemeisterTipp);
        }
    }

    // Admin-Authentifizierung
    public bool AdminLoginPruefen(string passwort)
    {
        return passwort == ADMIN_PASSWORT;
    }

    // Excel-Export für einzelnen Spieler
    public byte[] ExportiereNachExcelFuerSpieler(string spielerName)
    {
        lock (_lock)
        {
            using var workbook = new XLWorkbook();
            
            // Worksheet 1: Meine Tipps
            var wsTipps = workbook.Worksheets.Add("Meine Tipps");
            wsTipps.Cell(1, 1).Value = "Spieltag";
            wsTipps.Cell(1, 2).Value = "Spiel";
            wsTipps.Cell(1, 3).Value = "Mein Tipp";
            wsTipps.Cell(1, 4).Value = "Ergebnis";
            wsTipps.Cell(1, 5).Value = "Punkte";

            var headerTipps = wsTipps.Range("A1:E1");
            headerTipps.Style.Font.Bold = true;
            headerTipps.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerTipps.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2;
            string? letzterSpieltag = null;
            
            foreach (var spiel in spieleListe.Where(s => s.Tipps.ContainsKey(spielerName) && !s.IstBeendet())
                                             .OrderBy(s => s.Spieltag)
                                             .ThenBy(s => s.SpielDatum))
            {
                if (letzterSpieltag == null || spiel.Spieltag != letzterSpieltag)
                {
                    if (letzterSpieltag != null) row++;
                    
                    wsTipps.Cell(row, 1).Value = $"SPIELTAG {spiel.Spieltag}";
                    wsTipps.Range(row, 1, row, 5).Merge();
                    wsTipps.Range(row, 1, row, 5).Style.Font.Bold = true;
                    wsTipps.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    wsTipps.Range(row, 1, row, 5).Style.Font.FontColor = XLColor.White;
                    wsTipps.Range(row, 1, row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    row++;
                    
                    letzterSpieltag = spiel.Spieltag;
                }
                
                var tipp = spiel.Tipps[spielerName];
                
                wsTipps.Cell(row, 1).Value = spiel.Spieltag;
                wsTipps.Cell(row, 2).Value = $"{spiel.Heimmannschaft} - {spiel.Gastmannschaft}";
                wsTipps.Cell(row, 3).Value = $"{tipp.HeimTore}:{tipp.GastTore}";
                
                if (spiel.IstBeendet())
                {
                    wsTipps.Cell(row, 4).Value = $"{spiel.HeimTore}:{spiel.GastTore}";
                    int punkte = BerechneTippPunkte(tipp, spiel);
                    wsTipps.Cell(row, 5).Value = punkte;
                    
                    if (punkte == 3)
                        wsTipps.Range(row, 3, row, 5).Style.Fill.BackgroundColor = XLColor.Green;
                    else if (punkte == 2)
                        wsTipps.Range(row, 3, row, 5).Style.Fill.BackgroundColor = XLColor.LightGreen;
                    else if (punkte == 1)
                        wsTipps.Range(row, 3, row, 5).Style.Fill.BackgroundColor = XLColor.Yellow;
                }
                else
                {
                    wsTipps.Cell(row, 4).Value = "-";
                    wsTipps.Cell(row, 5).Value = "-";
                }
                
                row++;
            }

            wsTipps.Columns().AdjustToContents();

            // Worksheet 2: Tabelle
            var wsTabelle = workbook.Worksheets.Add("Tabelle");
            wsTabelle.Cell(1, 1).Value = "Platz";
            wsTabelle.Cell(1, 2).Value = "Spieler";
            wsTabelle.Cell(1, 3).Value = "Punkte";

            var headerTabelle = wsTabelle.Range("A1:C1");
            headerTabelle.Style.Font.Bold = true;
            headerTabelle.Style.Fill.BackgroundColor = XLColor.Gold;
            headerTabelle.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var sortiert = spielerListe.OrderByDescending(s => s.Punkte).ToList();
            row = 2;
            int platz = 1;
            for (int i = 0; i < sortiert.Count; i++)
            {
                if (i > 0 && sortiert[i].Punkte < sortiert[i - 1].Punkte)
                {
                    platz = i + 1;
                }

                wsTabelle.Cell(row, 1).Value = platz;
                wsTabelle.Cell(row, 2).Value = sortiert[i].Name;
                wsTabelle.Cell(row, 3).Value = sortiert[i].Punkte;

                if (platz == 1)
                    wsTabelle.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.Gold;
                else if (platz == 2)
                    wsTabelle.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.Silver;
                else if (platz == 3)
                    wsTabelle.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromArgb(205, 127, 50);
                else if (sortiert[i].Name == spielerName)
                    wsTabelle.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightBlue;

                row++;
            }

            wsTabelle.Columns().AdjustToContents();

            // Worksheet 3: Alle Tipps (nur abgeschlossene Spiele)
            var wsAlleTipps = workbook.Worksheets.Add("Alle Tipps");
            
            wsAlleTipps.Cell(1, 1).Value = "Spieltag";
            wsAlleTipps.Cell(1, 2).Value = "Spiel";
            wsAlleTipps.Cell(1, 3).Value = "Ergebnis";
            
            int col = 4;
            foreach (var spieler in spielerListe.OrderBy(s => s.Name))
            {
                wsAlleTipps.Cell(1, col).Value = spieler.Name;
                wsAlleTipps.Cell(1, col).Style.Border.LeftBorder = XLBorderStyleValues.Medium;
                wsAlleTipps.Cell(1, col).Style.Border.RightBorder = XLBorderStyleValues.Medium;
                col++;
            }
            
            var headerAlleTipps = wsAlleTipps.Range(1, 1, 1, col - 1);
            headerAlleTipps.Style.Font.Bold = true;
            headerAlleTipps.Style.Fill.BackgroundColor = XLColor.LightGreen;
            headerAlleTipps.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            
            row = 2;
            letzterSpieltag = null;
            
            // Nur abgeschlossene Spiele anzeigen
            foreach (var spiel in spieleListe.Where(s => s.IstBeendet())
                                             .OrderBy(s => s.Spieltag)
                                             .ThenBy(s => s.SpielDatum))
            {
                if (letzterSpieltag == null || spiel.Spieltag != letzterSpieltag)
                {
                    if (letzterSpieltag != null) row++;
                    
                    wsAlleTipps.Cell(row, 1).Value = $"SPIELTAG {spiel.Spieltag}";
                    wsAlleTipps.Range(row, 1, row, col - 1).Merge();
                    wsAlleTipps.Range(row, 1, row, col - 1).Style.Font.Bold = true;
                    wsAlleTipps.Range(row, 1, row, col - 1).Style.Fill.BackgroundColor = XLColor.DarkGreen;
                    wsAlleTipps.Range(row, 1, row, col - 1).Style.Font.FontColor = XLColor.White;
                    wsAlleTipps.Range(row, 1, row, col - 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    row++;
                    
                    letzterSpieltag = spiel.Spieltag;
                }
                
                wsAlleTipps.Cell(row, 1).Value = spiel.Spieltag;
                wsAlleTipps.Cell(row, 2).Value = $"{spiel.Heimmannschaft} - {spiel.Gastmannschaft}";
                wsAlleTipps.Cell(row, 3).Value = $"{spiel.HeimTore}:{spiel.GastTore}";
                
                col = 4;
                foreach (var spieler in spielerListe.OrderBy(s => s.Name))
                {
                    if (spiel.Tipps.ContainsKey(spieler.Name))
                    {
                        var tipp = spiel.Tipps[spieler.Name];
                        wsAlleTipps.Cell(row, col).Value = $"{tipp.HeimTore}:{tipp.GastTore}";
                        
                        int punkte = BerechneTippPunkte(tipp, spiel);
                        
                        if (punkte == 3)
                            wsAlleTipps.Cell(row, col).Style.Fill.BackgroundColor = XLColor.Green;
                        else if (punkte == 2)
                            wsAlleTipps.Cell(row, col).Style.Fill.BackgroundColor = XLColor.LightGreen;
                        else if (punkte == 1)
                            wsAlleTipps.Cell(row, col).Style.Fill.BackgroundColor = XLColor.Yellow;
                    }
                    else
                    {
                        wsAlleTipps.Cell(row, col).Value = "-";
                    }
                    
                    wsAlleTipps.Cell(row, col).Style.Border.LeftBorder = XLBorderStyleValues.Medium;
                    wsAlleTipps.Cell(row, col).Style.Border.RightBorder = XLBorderStyleValues.Medium;
                    wsAlleTipps.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    
                    col++;
                }
                
                row++;
            }
            
            wsAlleTipps.Columns().AdjustToContents();

            // Worksheet 4: Turniertipps
            var wsTurnier = workbook.Worksheets.Add("Turniertipps");
            wsTurnier.Cell(1, 1).Value = "Spieler";
            wsTurnier.Cell(1, 2).Value = "Weltmeister";
            wsTurnier.Cell(1, 3).Value = "Vizemeister";

            var headerTurnier = wsTurnier.Range("A1:C1");
            headerTurnier.Style.Font.Bold = true;
            headerTurnier.Style.Fill.BackgroundColor = XLColor.Gold;
            headerTurnier.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row = 2;
            foreach (var benutzer in benutzerListe.OrderBy(b => b.Benutzername))
            {
                wsTurnier.Cell(row, 1).Value = benutzer.Benutzername;
                wsTurnier.Cell(row, 2).Value = string.IsNullOrEmpty(benutzer.WeltmeisterTipp) ? "-" : benutzer.WeltmeisterTipp;
                wsTurnier.Cell(row, 3).Value = string.IsNullOrEmpty(benutzer.VizemeisterTipp) ? "-" : benutzer.VizemeisterTipp;

                if (benutzer.Benutzername.Equals(spielerName, StringComparison.OrdinalIgnoreCase))
                    wsTurnier.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightBlue;

                row++;
            }

            wsTurnier.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }

    // Daten-Persistierung
    private void LadeDaten()
    {
        try
        {
            if (File.Exists(SPEICHER_DATEI))
            {
                string json = File.ReadAllText(SPEICHER_DATEI);
                var daten = JsonSerializer.Deserialize<TippspielDaten>(json);

                if (daten != null)
                {
                    benutzerListe = daten.Benutzer ?? new List<Benutzer>();
                    mannschaftsListe = daten.Mannschaften ?? new List<Mannschaft>();
                    spielerListe = daten.Spieler ?? new List<Spieler>();
                    spieleListe = daten.Spiele ?? new List<Spiel>();
                    prahlAktionen = daten.PrahlAktionen ?? new List<PrahlAktion>();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Laden: {ex.Message}");
        }
    }

    private void SpeichereDaten()
    {
        try
        {
            var daten = new TippspielDaten
            {
                Benutzer = benutzerListe,
                Mannschaften = mannschaftsListe,
                Spieler = spielerListe,
                Spiele = spieleListe,
                PrahlAktionen = prahlAktionen
            };

            var optionen = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(daten, optionen);
            File.WriteAllText(SPEICHER_DATEI, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Speichern: {ex.Message}");
        }
    }

    // Excel-Export
    public byte[] ExportiereNachExcel()
    {
        lock (_lock)
        {
            using var workbook = new XLWorkbook();
            
            // Worksheet 1: Spiele
            var wsSpiele = workbook.Worksheets.Add("Spiele");
            wsSpiele.Cell(1, 1).Value = "Spieltag";
            wsSpiele.Cell(1, 2).Value = "Heimmannschaft";
            wsSpiele.Cell(1, 3).Value = "Gastmannschaft";
            wsSpiele.Cell(1, 4).Value = "Datum";
            wsSpiele.Cell(1, 5).Value = "Uhrzeit";
            wsSpiele.Cell(1, 6).Value = "Ergebnis";
            wsSpiele.Cell(1, 7).Value = "Status";

            var headerRange = wsSpiele.Range("A1:G1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2;
            string? letzterSpieltag = null;
            
            foreach (var spiel in spieleListe.OrderBy(s => s.Spieltag).ThenBy(s => s.SpielDatum))
            {
                if (letzterSpieltag == null || spiel.Spieltag != letzterSpieltag)
                {
                    if (letzterSpieltag != null) row++;
                    
                    wsSpiele.Cell(row, 1).Value = $"SPIELTAG {spiel.Spieltag}";
                    wsSpiele.Range(row, 1, row, 7).Merge();
                    wsSpiele.Range(row, 1, row, 7).Style.Font.Bold = true;
                    wsSpiele.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    wsSpiele.Range(row, 1, row, 7).Style.Font.FontColor = XLColor.White;
                    wsSpiele.Range(row, 1, row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    row++;
                    
                    letzterSpieltag = spiel.Spieltag;
                }
                
                wsSpiele.Cell(row, 1).Value = spiel.Spieltag;
                wsSpiele.Cell(row, 2).Value = spiel.Heimmannschaft;
                wsSpiele.Cell(row, 3).Value = spiel.Gastmannschaft;
                wsSpiele.Cell(row, 4).Value = spiel.SpielDatum.ToString("dd.MM.yyyy");
                wsSpiele.Cell(row, 5).Value = spiel.SpielDatum.ToString("HH:mm");
                wsSpiele.Cell(row, 6).Value = spiel.IstBeendet() ? $"{spiel.HeimTore}:{spiel.GastTore}" : "-";
                wsSpiele.Cell(row, 7).Value = spiel.IstBeendet() ? "Beendet" : "Offen";
                row++;
            }

            wsSpiele.Columns().AdjustToContents();

            // Worksheet 2: Tabelle
            var wsTabelle = workbook.Worksheets.Add("Tabelle");
            wsTabelle.Cell(1, 1).Value = "Platz";
            wsTabelle.Cell(1, 2).Value = "Spieler";
            wsTabelle.Cell(1, 3).Value = "Punkte";

            var headerTabelle = wsTabelle.Range("A1:C1");
            headerTabelle.Style.Font.Bold = true;
            headerTabelle.Style.Fill.BackgroundColor = XLColor.Gold;
            headerTabelle.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var sortiert = spielerListe.OrderByDescending(s => s.Punkte).ToList();
            row = 2;
            int platz = 1;
            for (int i = 0; i < sortiert.Count; i++)
            {
                if (i > 0 && sortiert[i].Punkte < sortiert[i - 1].Punkte)
                {
                    platz = i + 1;
                }

                wsTabelle.Cell(row, 1).Value = platz;
                wsTabelle.Cell(row, 2).Value = sortiert[i].Name;
                wsTabelle.Cell(row, 3).Value = sortiert[i].Punkte;

                if (platz == 1)
                    wsTabelle.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.Gold;
                else if (platz == 2)
                    wsTabelle.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.Silver;
                else if (platz == 3)
                    wsTabelle.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromArgb(205, 127, 50);

                row++;
            }

            wsTabelle.Columns().AdjustToContents();

            // Worksheet 3: Tipps
            var wsTipps = workbook.Worksheets.Add("Tipps");
            
            // Header mit Spielernamen
            wsTipps.Cell(1, 1).Value = "Spieltag";
            wsTipps.Cell(1, 2).Value = "Spiel";
            wsTipps.Cell(1, 3).Value = "Ergebnis";
            
            int col = 4;
            foreach (var spieler in spielerListe.OrderBy(s => s.Name))
            {
                wsTipps.Cell(1, col).Value = spieler.Name;
                wsTipps.Cell(1, col).Style.Border.LeftBorder = XLBorderStyleValues.Medium;
                wsTipps.Cell(1, col).Style.Border.RightBorder = XLBorderStyleValues.Medium;
                col++;
            }
            
            var headerTipps = wsTipps.Range(1, 1, 1, col - 1);
            headerTipps.Style.Font.Bold = true;
            headerTipps.Style.Fill.BackgroundColor = XLColor.LightGreen;
            headerTipps.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            
            row = 2;
            letzterSpieltag = null;
            
            foreach (var spiel in spieleListe.OrderBy(s => s.Spieltag).ThenBy(s => s.SpielDatum))
            {
                // Spieltag-Überschrift
                if (letzterSpieltag == null || spiel.Spieltag != letzterSpieltag)
                {
                    if (letzterSpieltag != null) row++;
                    
                    wsTipps.Cell(row, 1).Value = $"SPIELTAG {spiel.Spieltag}";
                    wsTipps.Range(row, 1, row, col - 1).Merge();
                    wsTipps.Range(row, 1, row, col - 1).Style.Font.Bold = true;
                    wsTipps.Range(row, 1, row, col - 1).Style.Fill.BackgroundColor = XLColor.DarkGreen;
                    wsTipps.Range(row, 1, row, col - 1).Style.Font.FontColor = XLColor.White;
                    wsTipps.Range(row, 1, row, col - 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    row++;
                    
                    letzterSpieltag = spiel.Spieltag;
                }
                
                // Spielinformationen
                wsTipps.Cell(row, 1).Value = spiel.Spieltag;
                wsTipps.Cell(row, 2).Value = $"{spiel.Heimmannschaft} - {spiel.Gastmannschaft}";
                wsTipps.Cell(row, 3).Value = spiel.IstBeendet() ? $"{spiel.HeimTore}:{spiel.GastTore}" : "-";
                
                // Tipps für jeden Spieler
                col = 4;
                foreach (var spieler in spielerListe.OrderBy(s => s.Name))
                {
                    if (spiel.Tipps.ContainsKey(spieler.Name))
                    {
                        var tipp = spiel.Tipps[spieler.Name];
                        wsTipps.Cell(row, col).Value = $"{tipp.HeimTore}:{tipp.GastTore}";
                        
                        if (spiel.IstBeendet())
                        {
                            int punkte = BerechneTippPunkte(tipp, spiel);
                            
                            // Farbmarkierung
                            if (punkte == 3)
                                wsTipps.Cell(row, col).Style.Fill.BackgroundColor = XLColor.Green;
                            else if (punkte == 2)
                                wsTipps.Cell(row, col).Style.Fill.BackgroundColor = XLColor.LightGreen;
                            else if (punkte == 1)
                                wsTipps.Cell(row, col).Style.Fill.BackgroundColor = XLColor.Yellow;
                        }
                    }
                    else
                    {
                        wsTipps.Cell(row, col).Value = "-";
                    }
                    
                    // Trennlinien zwischen Spielern
                    wsTipps.Cell(row, col).Style.Border.LeftBorder = XLBorderStyleValues.Medium;
                    wsTipps.Cell(row, col).Style.Border.RightBorder = XLBorderStyleValues.Medium;
                    wsTipps.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    
                    col++;
                }
                
                row++;
            }
            
            wsTipps.Columns().AdjustToContents();

            // Worksheet 4: Turniertipps
            var wsTurnier = workbook.Worksheets.Add("Turniertipps");
            wsTurnier.Cell(1, 1).Value = "Spieler";
            wsTurnier.Cell(1, 2).Value = "Weltmeister";
            wsTurnier.Cell(1, 3).Value = "Vizemeister";

            var headerTurnier = wsTurnier.Range("A1:C1");
            headerTurnier.Style.Font.Bold = true;
            headerTurnier.Style.Fill.BackgroundColor = XLColor.Gold;
            headerTurnier.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row = 2;
            foreach (var benutzer in benutzerListe.OrderBy(b => b.Benutzername))
            {
                wsTurnier.Cell(row, 1).Value = benutzer.Benutzername;
                wsTurnier.Cell(row, 2).Value = string.IsNullOrEmpty(benutzer.WeltmeisterTipp) ? "-" : benutzer.WeltmeisterTipp;
                wsTurnier.Cell(row, 3).Value = string.IsNullOrEmpty(benutzer.VizemeisterTipp) ? "-" : benutzer.VizemeisterTipp;
                row++;
            }

            wsTurnier.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
    
    // Prahlen-Feature
    public bool KannPrahlen(string spielerName, out string grund)
    {
        lock (_lock)
        {
            var spieler = spielerListe.FirstOrDefault(s => s.Name == spielerName);
            if (spieler == null)
            {
                grund = "Spieler nicht gefunden";
                return false;
            }

            var sortiert = spielerListe.OrderByDescending(s => s.Punkte).ToList();
            var platz = sortiert.FindIndex(s => s.Name == spielerName) + 1;

            if (platz > 3)
            {
                grund = "Du musst in den Top 3 sein um zu prahlen!";
                return false;
            }

            // Ermittle aktuellen Spieltag (höchster Spieltag mit beendetem Spiel)
            var aktuellerSpieltag = spieleListe
                .Where(s => s.IstBeendet())
                .OrderByDescending(s => int.TryParse(s.Spieltag, out int st) ? st : 0)
                .Select(s => s.Spieltag)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(aktuellerSpieltag))
            {
                grund = "Es wurden noch keine Spiele beendet";
                return false;
            }

            // Prüfe ob bereits in diesem Spieltag geprahlt wurde
            var hatBereitsGeprahlt = prahlAktionen.Any(p => 
                p.SpielerName == spielerName && 
                p.Spieltag == aktuellerSpieltag);

            if (hatBereitsGeprahlt)
            {
                grund = $"Du hast bereits im Spieltag {aktuellerSpieltag} geprahlt!";
                return false;
            }

            grund = string.Empty;
            return true;
        }
    }

    public void PrahlAktionSpeichern(string spielerName, string nachricht)
    {
        lock (_lock)
        {
            var spieler = spielerListe.FirstOrDefault(s => s.Name == spielerName);
            if (spieler == null) return;

            var sortiert = spielerListe.OrderByDescending(s => s.Punkte).ToList();
            var platz = sortiert.FindIndex(s => s.Name == spielerName) + 1;

            var aktuellerSpieltag = spieleListe
                .Where(s => s.IstBeendet())
                .OrderByDescending(s => int.TryParse(s.Spieltag, out int st) ? st : 0)
                .Select(s => s.Spieltag)
                .FirstOrDefault() ?? "0";

            var prahlAktion = new PrahlAktion(
                spielerName, 
                nachricht, 
                aktuellerSpieltag, 
                platz, 
                spieler.Punkte
            );

            prahlAktionen.Add(prahlAktion);
            
            // Behalte nur die letzten 50 Prahl-Aktionen
            if (prahlAktionen.Count > 50)
            {
                prahlAktionen = prahlAktionen
                    .OrderByDescending(p => p.Zeitstempel)
                    .Take(50)
                    .ToList();
            }

            SpeichereDaten();
        }
    }

    public List<PrahlAktion> GetPrahlAktionen(int anzahl = 10)
    {
        lock (_lock)
        {
            return prahlAktionen
                .OrderByDescending(p => p.Zeitstempel)
                .Take(anzahl)
                .ToList();
        }
    }
}
