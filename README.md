# TippspielWeb

Eine ASP.NET Core Blazor Server Anwendung für Fußball-Tippspiele mit Benutzerverwaltung, Turniertipps und umfangreichen Statistiken.

## Features

- 🔐 **Benutzer-Authentifizierung**: Registrierung mit eindeutigen Benutzernamen und sicherer Passwort-Hashing (SHA256)
- ⚽ **Spielverwaltung**: Admin kann Spiele anlegen, bearbeiten und Ergebnisse eintragen
- 🏆 **Turniertipps**: Benutzer können auf Weltmeister und Vizemeister tippen
- 📊 **Statistiken**: Detaillierte Tipp-Statistiken mit Erfolgsquoten
- 👥 **Mannschaftsverwaltung**: Vordefinierte Mannschaften für schnellere Spielerstellung
- 📈 **Live-Tabelle**: Aktuelle Rangliste aller Teilnehmer
- 🔒 **Tipp-Sperre**: Automatische Sperrung 1 Stunde vor Spielbeginn
- 👀 **Transparenz**: Gesperrte Spiele zeigen alle Tipps aller Benutzer
- 📑 **Excel-Export**: Umfangreiche Excel-Exporte mit allen Daten und Turniertipps
- 📱 **Responsive Design**: Bootstrap-basiertes Design für alle Geräte
- 💪 **Prahlen-Feature**: Top 3 Spieler können prahlen - Animation wird allen Benutzern in Echtzeit angezeigt

## Technologie-Stack

- **Framework**: .NET 10.0
- **UI**: Blazor Server (Interactive Server Rendering)
- **Datenbank**: JSON-Datei (tippspiel_daten.json)
- **Styling**: Bootstrap 5 + Bootstrap Icons
- **Excel-Export**: ClosedXML 0.105.0

## Voraussetzungen

- .NET 10.0 SDK oder höher
- Windows/Linux/macOS

## Installation & Lokale Entwicklung

```bash
# Repository klonen
git clone <repository-url>
cd TippspielWeb

# Abhängigkeiten installieren
dotnet restore

# Anwendung starten
dotnet run

# Anwendung läuft auf http://localhost:5000
```

## Deployment auf Render

Diese Anwendung ist für Render.com vorbereitet.

### Render Einstellungen:

- **Build Command**: `dotnet publish -c Release -o out`
- **Start Command**: `cd out && dotnet TippspielWeb.dll --urls "http://0.0.0.0:$PORT"`
- **Environment**: .NET

### Umgebungsvariablen (optional):

Keine zusätzlichen Umgebungsvariablen erforderlich.

## Konfiguration

### Admin-Zugang

- **Admin-Passwort**: `admin123` (in `TippspielService.cs` änderbar)

### Server-Einstellungen

Die Anwendung läuft standardmäßig auf Port 5000. Dies kann in `Program.cs` angepasst werden.

## Projekt-Struktur

```
TippspielWeb/
├── Components/
│   ├── Layout/          # Layout-Komponenten
│   ├── Pages/           # Razor-Seiten
│   │   ├── AdminBereich.razor
│   │   ├── SpielerBereich.razor
│   │   ├── Login.razor
│   │   ├── Registrierung.razor
│   │   └── Profil.razor
│   └── App.razor
├── Models/
│   └── TippspielModels.cs    # Datenmodelle
├── Services/
│   ├── TippspielService.cs   # Business-Logik
│   └── AuthService.cs        # Authentifizierung
├── wwwroot/                   # Statische Dateien
├── Program.cs                 # Startup-Konfiguration
└── appsettings.json          # App-Konfiguration
```

## Verwendung

### Als Spieler:

1. **Registrieren**: Erstelle einen Account mit eindeutigem Benutzernamen
2. **Tipps abgeben**: Tippe Ergebnisse bis 1 Stunde vor Spielbeginn
3. **Turniertipp**: Tippe auf Weltmeister und Vizemeister
4. **Statistiken**: Verfolge deine Erfolgsquote
5. **Alle Tipps**: Sehe alle Tipps sobald Spiele gesperrt sind

### Als Admin:

1. **Login**: Verwende das Admin-Passwort
2. **Mannschaften**: Lege Mannschaften für schnellere Spielerstellung an
3. **Spiele**: Erstelle Spiele und trage Ergebnisse ein
4. **Tipps überwachen**: Sehe alle abgegebenen Tipps
5. **Turniertipps**: Übersicht aller Turniertipps
6. **Benutzerverwaltung**: Verwalte Benutzer-Accounts
7. **Excel-Export**: Exportiere alle Daten

## Punkte-System

- **3 Punkte**: Exaktes Ergebnis
- **2 Punkte**: Richtige Tordifferenz
- **1 Punkt**: Richtige Tendenz (Sieg/Unentschieden/Niederlage)
- **0 Punkte**: Falsch

## Sicherheit

- Passwörter werden mit SHA256 gehasht gespeichert
- Admin-Bereich durch Passwort geschützt
- Session-basierte Authentifizierung
- Validierung von Benutzereingaben

## Datenpersistenz

Alle Daten werden in `tippspiel_daten.json` gespeichert:
- Benutzer mit Passwort-Hashes
- Mannschaften
- Spieler mit Punkten
- Spiele mit Tipps
- Turniertipps

**Wichtig**: Stelle sicher, dass diese Datei regelmäßig gesichert wird!

## Google Analytics (optional)

Google Analytics ist integriert. Analytics-ID kann in `Components/App.razor` angepasst werden.

## Lizenz

Dieses Projekt ist für private/kommerzielle Nutzung frei verfügbar.

## Support

Bei Fragen oder Problemen erstelle bitte ein Issue im Repository.
