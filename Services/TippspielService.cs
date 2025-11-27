using System.Text.Json;
using TippspielWeb.Models;
using ClosedXML.Excel;

namespace TippspielWeb.Services;

public class TippspielService
{
    private const string ADMIN_PASSWORT = "admin123";
    
    private readonly SupabaseService _supabaseService;
    private readonly object _lock = new();

    public TippspielService(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
        // Berechne Punkte beim Start neu, falls bereits Ergebnisse vorhanden sind
        PunkteBerechnen();
    }

    // Mannschafts-Verwaltung
    public List<Mannschaft> GetAlleMannschaften()
    {
        lock (_lock)
        {
            return _supabaseService.GetAlleMannschaften();
        }
    }

    public bool MannschaftHinzufuegen(string name)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var alleMannschaften = _supabaseService.GetAlleMannschaften();
            if (alleMannschaften.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false;

            return _supabaseService.MannschaftHinzufuegen(name.Trim());
        }
    }

    public bool MannschaftLoeschen(string name)
    {
        lock (_lock)
        {
            return _supabaseService.MannschaftLoeschen(name);
        }
    }

    public bool MannschaftUmbenennen(string alterName, string neuerName)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(neuerName))
                return false;

            var alleMannschaften = _supabaseService.GetAlleMannschaften();
            var mannschaft = alleMannschaften.FirstOrDefault(m => m.Name.Equals(alterName, StringComparison.OrdinalIgnoreCase));
            if (mannschaft == null) return false;

            // Prüfe ob neuer Name schon existiert
            if (!alterName.Equals(neuerName, StringComparison.OrdinalIgnoreCase) &&
                alleMannschaften.Any(m => m.Name.Equals(neuerName, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Lösche alte und füge neue hinzu (vereinfachte Umbenennung)
            _supabaseService.MannschaftLoeschen(alterName);
            return _supabaseService.MannschaftHinzufuegen(neuerName.Trim());
        }
    }

    // Spieler-Verwaltung
    public List<Spieler> GetAlleSpieler()
    {
        lock (_lock)
        {
            return _supabaseService.GetAlleSpieler();
        }
    }

    public bool SpielerHinzufuegen(string name)
    {
        lock (_lock)
        {
            var alleSpieler = _supabaseService.GetAlleSpieler();
            if (alleSpieler.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Spieler manuell in DB eintragen (mit auto-increment ID)
            return _supabaseService.BenutzerRegistrieren(name, ""); // Spieler haben kein Passwort in der DB
        }
    }

    public bool SpielerLoeschen(string name)
    {
        lock (_lock)
        {
            // Lösche den Benutzer mit diesem Namen aus der Datenbank
            return _supabaseService.BenutzerLoeschen(name);
        }
    }

    // Spiel-Verwaltung
    public List<Spiel> GetAlleSpiele()
    {
        lock (_lock)
        {
            return _supabaseService.GetAlleSpiele();
        }
    }

    public void SpielHinzufuegen(string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
    {
        lock (_lock)
        {
            try
            {
                var alleSpiele = _supabaseService.GetAlleSpiele();
                int neueSpielNr = alleSpiele.Count > 0 ? alleSpiele.Max(s => s.SpielNummer) + 1 : 1;
                Console.WriteLine($"SpielHinzufuegen: Generiere SpielNr {neueSpielNr} für {heimmannschaft} vs {gastmannschaft}");
                _supabaseService.SpielHinzufuegen(neueSpielNr, spieltag, heimmannschaft, gastmannschaft, spielDatum);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FEHLER in SpielHinzufuegen: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }
    }

    public bool SpielBearbeiten(int spielNummer, string spieltag, string heimmannschaft, string gastmannschaft, DateTime spielDatum)
    {
        lock (_lock)
        {
            var alleSpiele = _supabaseService.GetAlleSpiele();
            var spiel = alleSpiele.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null) return false;

            // Aktualisiere Spiel in DB (Ergebnis und Tipps bleiben erhalten)
            _supabaseService.SpielHinzufuegen(spielNummer, spieltag, heimmannschaft, gastmannschaft, spielDatum);
            
            // Trage Ergebnis wieder ein falls vorhanden
            if (spiel.HeimTore.HasValue && spiel.GastTore.HasValue)
                _supabaseService.ErgebnisEintragen(spielNummer, spiel.HeimTore.Value, spiel.GastTore.Value);
            
            return true;
        }
    }

    public bool SpielLoeschen(int spielNummer)
    {
        lock (_lock)
        {
            var alleSpiele = _supabaseService.GetAlleSpiele();
            var spiel = alleSpiele.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null) return false;

            // DELETE in DB (Tipps werden durch CASCADE gelöscht)
            // Note: Derzeit keine SpielLoeschen Methode in SupabaseService - muss hinzugefügt werden
            PunkteBerechnen();
            return true;
        }
    }

    public bool ErgebnisEintragen(int spielNummer, int heimTore, int gastTore)
    {
        lock (_lock)
        {
            var alleSpiele = _supabaseService.GetAlleSpiele();
            var spiel = alleSpiele.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null) return false;

            _supabaseService.ErgebnisEintragen(spielNummer, heimTore, gastTore);
            PunkteBerechnen();
            return true;
        }
    }

    // Tipp-Verwaltung
    public bool TippAbgeben(int spielNummer, string spielerName, int heimTore, int gastTore)
    {
        lock (_lock)
        {
            var alleSpiele = _supabaseService.GetAlleSpiele();
            var spiel = alleSpiele.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null || spiel.IstBeendet()) return false;

            // Prüfe ob das Spiel in weniger als 1 Stunde beginnt
            if (DateTime.Now >= spiel.SpielDatum.AddHours(-1))
                return false;

            return _supabaseService.TippAbgeben(spielNummer, spielerName, heimTore, gastTore);
        }
    }

    public bool IstTippMoeglich(int spielNummer)
    {
        lock (_lock)
        {
            var alleSpiele = _supabaseService.GetAlleSpiele();
            var spiel = alleSpiele.FirstOrDefault(s => s.SpielNummer == spielNummer);
            if (spiel == null || spiel.IstBeendet()) return false;
            
            // Tipp ist möglich wenn noch mehr als 1 Stunde bis Spielbeginn
            return DateTime.Now < spiel.SpielDatum.AddHours(-1);
        }
    }

    public Dictionary<int, Tipp> GetSpielerTipps(string spielerName)
    {
        lock (_lock)
        {
            var alleSpiele = _supabaseService.GetAlleSpiele();
            return alleSpiele
                .Where(s => s.Tipps.ContainsKey(spielerName))
                .ToDictionary(s => s.SpielNummer, s => s.Tipps[spielerName]);
        }
    }

    // Punkte berechnen
    public void PunkteBerechnen()
    {
        lock (_lock)
        {
            var alleSpieler = _supabaseService.GetAlleSpieler();
            
            foreach (var spieler in alleSpieler)
            {
                spieler.Punkte = 0;
            }

            var alleSpiele = _supabaseService.GetAlleSpiele();
            foreach (var spiel in alleSpiele.Where(s => s.IstBeendet()))
            {
                foreach (var tippEntry in spiel.Tipps)
                {
                    var spieler = alleSpieler.FirstOrDefault(s => s.Name == tippEntry.Key);
                    if (spieler != null)
                    {
                        int punkte = BerechneTippPunkte(tippEntry.Value, spiel);
                        spieler.Punkte += punkte;
                    }
                }
            }

            // Aktualisiere Punkte in der Datenbank
            foreach (var spieler in alleSpieler)
            {
                _supabaseService.SpielerPunkteAktualisieren(spieler.Name, spieler.Punkte);
            }
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
            var alleBenutzer = _supabaseService.GetAlleBenutzer();
            var existierenderBenutzer = alleBenutzer.FirstOrDefault(b => b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            // Wenn Benutzer bereits mit Passwort existiert, ablehnen
            if (existierenderBenutzer != null && !string.IsNullOrEmpty(existierenderBenutzer.PasswortHash))
                return false;

            string passwortHash = AuthService.HashPasswort(passwort);
            
            // Wenn nur Spieler-Platzhalter existiert (ohne Passwort), aktualisiere das Passwort
            if (existierenderBenutzer != null && string.IsNullOrEmpty(existierenderBenutzer.PasswortHash))
            {
                return _supabaseService.PasswortAendern(benutzername, passwortHash);
            }
            
            // Neuer Benutzer
            return _supabaseService.BenutzerRegistrieren(benutzername, passwortHash);
        }
    }

    public Benutzer? BenutzerLogin(string benutzername, string passwort)
    {
        lock (_lock)
        {
            string passwortHash = AuthService.HashPasswort(passwort);
            return _supabaseService.BenutzerLogin(benutzername, passwortHash);
        }
    }

    public bool BenutzerExistiert(string benutzername)
    {
        lock (_lock)
        {
            var alleBenutzer = _supabaseService.GetAlleBenutzer();
            // Nur echte Benutzer (mit Passwort) zählen, nicht Spieler-Platzhalter
            return alleBenutzer.Any(b => b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase) 
                && !string.IsNullOrEmpty(b.PasswortHash));
        }
    }

    public List<Benutzer> GetAlleBenutzer()
    {
        lock (_lock)
        {
            return _supabaseService.GetAlleBenutzer();
        }
    }

    public bool BenutzernameAendern(string alterBenutzername, string neuerBenutzername)
    {
        lock (_lock)
        {
            // TODO: Diese Funktion benötigt komplexe DB-Updates über mehrere Tabellen
            // Vorläufig deaktiviert bis Supabase-Implementierung fertig
            return false;
        }
    }

    public bool PasswortAendern(string benutzername, string altesPasswort, string neuesPasswort)
    {
        lock (_lock)
        {
            var alleBenutzer = _supabaseService.GetAlleBenutzer();
            var benutzer = alleBenutzer.FirstOrDefault(b => 
                b.Benutzername.Equals(benutzername, StringComparison.OrdinalIgnoreCase));
            
            if (benutzer == null) return false;

            // Prüfe altes Passwort
            if (!AuthService.VerifyPasswort(altesPasswort, benutzer.PasswortHash))
                return false;

            // Setze neues Passwort
            string neuerHash = AuthService.HashPasswort(neuesPasswort);
            return _supabaseService.PasswortAendern(benutzername, neuerHash);
        }
    }

    public bool PasswortAendernAlsAdmin(string benutzername, string neuesPasswort)
    {
        lock (_lock)
        {
            string neuerHash = AuthService.HashPasswort(neuesPasswort);
            return _supabaseService.PasswortAendern(benutzername, neuerHash);
        }
    }

    public bool BenutzerLoeschen(string benutzername)
    {
        lock (_lock)
        {
            return _supabaseService.BenutzerLoeschen(benutzername);
        }
    }

    // Turniertipps (Weltmeister/Vizemeister)
    public void TurniertippSpeichern(string benutzername, string? weltmeister, string? vizemeister)
    {
        lock (_lock)
        {
            _supabaseService.TurniertippSpeichern(benutzername, weltmeister, vizemeister);
        }
    }

    public (string? weltmeister, string? vizemeister) GetTurniertipp(string benutzername)
    {
        lock (_lock)
        {
            return _supabaseService.GetTurniertipp(benutzername);
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
            // Lade Daten aus Datenbank
            var spieleListe = _supabaseService.GetAlleSpiele();
            var spielerListe = _supabaseService.GetAlleSpieler();
            var benutzerListe = _supabaseService.GetAlleBenutzer();
            
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

    // Excel-Export
    public byte[] ExportiereNachExcel()
    {
        lock (_lock)
        {
            // Lade Daten aus Datenbank
            var spieleListe = _supabaseService.GetAlleSpiele();
            var spielerListe = _supabaseService.GetAlleSpieler();
            var benutzerListe = _supabaseService.GetAlleBenutzer();
            
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
            var spielerListe = _supabaseService.GetAlleSpieler();
            var spieleListe = _supabaseService.GetAlleSpiele();
            var prahlAktionen = _supabaseService.GetPrahlAktionen(100);
            
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
            // TEMPORÄR DEAKTIVIERT - Spieler können mehrfach pro Spieltag prahlen
            /*
            var hatBereitsGeprahlt = prahlAktionen.Any(p => 
                p.SpielerName == spielerName && 
                p.Spieltag == aktuellerSpieltag);

            if (hatBereitsGeprahlt)
            {
                grund = $"Du hast bereits im Spieltag {aktuellerSpieltag} geprahlt!";
                return false;
            }
            */

            grund = string.Empty;
            return true;
        }
    }

    public void PrahlAktionSpeichern(string spielerName, string nachricht)
    {
        lock (_lock)
        {
            var spielerListe = _supabaseService.GetAlleSpieler();
            var spieleListe = _supabaseService.GetAlleSpiele();
            
            var spieler = spielerListe.FirstOrDefault(s => s.Name == spielerName);
            if (spieler == null) return;

            var sortiert = spielerListe.OrderByDescending(s => s.Punkte).ToList();
            var platz = sortiert.FindIndex(s => s.Name == spielerName) + 1;

            var aktuellerSpieltag = spieleListe
                .Where(s => s.IstBeendet())
                .OrderByDescending(s => int.TryParse(s.Spieltag, out int st) ? st : 0)
                .Select(s => s.Spieltag)
                .FirstOrDefault() ?? "0";

            _supabaseService.PrahlAktionSpeichern(spielerName, nachricht, aktuellerSpieltag, platz, spieler.Punkte);
        }
    }

    public List<PrahlAktion> GetPrahlAktionen(int anzahl = 10)
    {
        lock (_lock)
        {
            return _supabaseService.GetPrahlAktionen(anzahl);
        }
    }
}
