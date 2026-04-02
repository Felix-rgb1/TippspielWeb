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

- **Framework**: .NET 8.0 (nicht 10.0, da 8.0 der aktuelle LTS ist)
- **UI**: Blazor Server (Interactive Server Rendering)
- **Datenbank**: PostgreSQL (via Supabase) - **WICHTIG: Nicht mehr JSON-Datei!**
- **Styling**: Bootstrap 5 + Bootstrap Icons
- **Excel-Export**: ClosedXML 0.105.0

## Voraussetzungen

- .NET 8.0 SDK oder höher
- Windows/Linux/macOS
- Eine Supabase (PostgreSQL) Instanz mit Zugangsdaten in `appsettings.json`

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

Diese Anwendung ist für Render.com vorbereitet und nutzt Supabase (PostgreSQL) für die Datenpersistenz.

### Render Einstellungen:

- **Build Command**: `dotnet publish -c Release -o out`
- **Start Command**: `cd out && dotnet TippspielWeb.dll --urls "http://0.0.0.0:$PORT"`
- **Environment**: .NET

### Umgebungsvariablen:

Stelle sicher, dass die PostgreSQL-Verbindungszeichenfolge in `appsettings.json` oder als Umgebungsvariable `ConnectionStrings__Supabase` auf Render konfiguriert ist:

`ConnectionStrings__Supabase="Host=db.prjlfyahewmzqidfzmei.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=Asde2221;SSL Mode=Require;Trust Server Certificate=true"`

### Datenpersistenz auf Render (Wichtig!)

Durch die Migration zu Supabase (PostgreSQL) sind deine Anwendungsdaten nun persistent und gehen bei einem Neustart des Render-Dienstes nicht mehr verloren. Du benötigst keine Render Disks mehr für die Datenhaltung selbst. Stelle sicher, dass deine Supabase-Instanz aktiv ist und die Verbindungszeichenfolge korrekt ist.

## Konfiguration

### Admin-Zugang

- **Admin-Passwort**: `admin123` (Standardwert in `TippspielService.cs` änderbar. Wird beim ersten Start des `SupabaseService` gehasht und in der `Benutzer`-Tabelle als Admin-User angelegt, falls noch kein Admin existiert).

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
│   └── TippspielModels.cs    # Datenmodelle (angepasst für PostgreSQL)
├── Services/
│   ├── TippspielService.cs   # Business-Logik (nutzt SupabaseService)
│   ├── AuthService.cs        # Authentifizierung
│   └── SupabaseService.cs    # Datenbank-Interaktion mit PostgreSQL/Npgsql
├── wwwroot/                   # Statische Dateien
├── Program.cs                 # Startup-Konfiguration
└── appsettings.json          # App-Konfiguration
```

## Verwendung

### Als Spieler:

1.  **Registrieren**: Erstelle einen Account mit eindeutigem Benutzernamen
2.  **Tipps abgeben**: Tippe Ergebnisse bis 1 Stunde vor Spielbeginn
3.  **Turniertipp**: Tippe auf Weltmeister und Vizemeister
4.  **Statistiken**: Verfolge deine Erfolgsquote
5.  **Alle Tipps**: Sehe alle Tipps sobald Spiele gesperrt sind

### Als Admin:

1.  **Login**: Verwende das Admin-Passwort (Standard: 'admin123')
2.  **Mannschaften**: Lege Mannschaften für schnellere Spielerstellung an
3.  **Spiele**: Erstelle Spiele und trage Ergebnisse ein
4.  **Tipps überwachen**: Sehe alle abgegebenen Tipps
5.  **Turniertipps**: Übersicht aller Turniertipps
6.  **Benutzerverwaltung**: Verwalte Benutzer-Accounts
7.  **Excel-Export**: Exportiere alle Daten

## Punkte-System

- **3 Punkte**: Exaktes Ergebnis
- **2 Punkte**: Richtige Tordifferenz
- **1 Punkt**: Richtige Tendenz (Sieg/Unentschieden/Niederlage)
- **0 Punkte**: Falsch

## Sicherheit

- Passwörter werden mit SHA256 gehasht gespeichert
- Admin-Bereich durch Passwort geschützt
- Session-basierte Authentifizierung