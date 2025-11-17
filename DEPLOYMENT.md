# Render Deployment Anleitung für TippspielWeb

## Schritte zur Veröffentlichung auf Render.com

### 1. GitHub Repository erstellen

1. Gehe zu https://github.com und erstelle ein neues Repository
2. Name: `TippspielWeb` (oder beliebiger Name)
3. **NICHT** "Initialize with README" wählen (wir haben bereits eine)
4. Repository erstellen

### 2. Code zu GitHub hochladen

Führe folgende Befehle im Terminal aus (im Projektverzeichnis):

```bash
# Git initialisieren
git init

# Dateien hinzufügen
git add .

# Ersten Commit erstellen
git commit -m "Initial commit - TippspielWeb Blazor App"

# GitHub Repository als Remote hinzufügen (ersetze USERNAME und REPO-NAME)
git remote add origin https://github.com/USERNAME/REPO-NAME.git

# Code hochladen
git push -u origin main
```

Falls dein Hauptbranch "master" heißt statt "main":
```bash
git branch -M main
git push -u origin main
```

### 3. Render.com einrichten

1. Gehe zu https://render.com und erstelle einen Account (oder logge dich ein)
2. Klicke auf "New +" → "Web Service"
3. Verbinde dein GitHub Repository
4. Wähle dein `TippspielWeb` Repository aus

### 4. Render Konfiguration

Fülle die Einstellungen wie folgt aus:

**Basic Settings:**
- **Name**: `tippspielweb` (oder beliebiger Name)
- **Region**: Europe (Frankfurt) - für bessere Performance in Deutschland
- **Branch**: `main`
- **Runtime**: `.NET`

**Build Settings:**
- **Build Command**: 
  ```
  dotnet publish -c Release -o out
  ```

**Start Command**:
  ```
  cd out && dotnet TippspielWeb.dll --urls "http://0.0.0.0:$PORT"
  ```

**Instance Type:**
- Wähle "Free" für kostenlosen Start (mit Einschränkungen)
- Oder "Starter" für bessere Performance

**Advanced Settings** (optional):
- **Auto-Deploy**: YES (automatisches Deployment bei Git-Push)

### 5. Environment Variables (optional)

Keine Umgebungsvariablen erforderlich, außer du möchtest:
- `ASPNETCORE_ENVIRONMENT=Production` (wird automatisch gesetzt)

### 6. Deployment starten

1. Klicke auf "Create Web Service"
2. Render beginnt automatisch mit dem Build
3. Der erste Build dauert ~5-10 Minuten
4. Nach erfolgreichem Build ist deine App unter der Render-URL erreichbar

### 7. Deine App ist live! 🎉

Die App ist erreichbar unter: `https://tippspielweb.onrender.com` (oder dein gewählter Name)

## Wichtige Hinweise

### Datenpersistenz

⚠️ **WICHTIG**: Render Free Tier hat keinen persistenten Speicher!

Die `tippspiel_daten.json` wird bei jedem Neustart gelöscht. Lösungen:

**Option 1: Render Disk (kostenpflichtig)**
- Füge einen Persistent Disk hinzu in den Render Settings
- Mount Path: `/app/out`

**Option 2: Externe Datenbank**
- Migriere zu PostgreSQL oder MongoDB (kostenlos auf Render verfügbar)
- Erfordert Code-Änderungen

**Option 3: Regelmäßige Backups**
- Erstelle einen Cron-Job für Excel-Exports
- Speichere Backups extern

### Performance

**Free Tier Einschränkungen:**
- App "schläft" nach 15 Minuten Inaktivität
- Erster Aufruf nach Schlaf: ~30 Sekunden Ladezeit
- 750 Stunden/Monat kostenlos

**Starter Plan ($7/Monat):**
- Keine Schlafmodus
- Bessere Performance
- Mehr RAM

### Admin-Passwort ändern

Vor dem Deployment das Admin-Passwort ändern in:
`Services/TippspielService.cs` → Zeile mit `ADMIN_PASSWORT`

### Custom Domain (optional)

In Render Settings → "Custom Domains" kannst du deine eigene Domain verbinden.

## Troubleshooting

### Build schlägt fehl
- Prüfe ob `.gitignore` richtig gesetzt ist
- Stelle sicher, dass alle NuGet-Pakete in der `.csproj` definiert sind

### App startet nicht
- Überprüfe die Start Command Syntax
- Prüfe die Logs in Render Dashboard

### Port-Probleme
- Stelle sicher, dass `$PORT` Variable verwendet wird
- Render weist dynamisch einen Port zu

### Daten gehen verloren
- Siehe "Datenpersistenz" Lösungen oben
- Erwäge Migration zu echter Datenbank

## Updates deployen

Nach Änderungen am Code:

```bash
git add .
git commit -m "Beschreibung der Änderungen"
git push
```

Render deployed automatisch die neuen Änderungen!

## Support

Bei Problemen:
1. Prüfe Render Logs im Dashboard
2. Schaue in die .NET Logs
3. Erstelle ein Issue im GitHub Repository

## Kosten-Übersicht

- **Free Tier**: $0/Monat (mit Einschränkungen)
- **Starter**: $7/Monat (empfohlen für Produktion)
- **Persistent Disk**: +$1/GB/Monat (optional)

Viel Erfolg mit deinem Tippspiel! ⚽🏆
