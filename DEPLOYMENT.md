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

### 5. Environment Variables (wichtig für Datenbank)

Die Anwendung verwendet nun eine PostgreSQL-Datenbank (z.B. Supabase) für die Datenpersistenz. Stelle sicher, dass die Verbindungszeichenfolge korrekt über die `appsettings.json` oder über Umgebungsvariablen bereitgestellt wird.

Füge die Umgebungsvariable für die Datenbankverbindung hinzu (falls nicht direkt in `appsettings.json` für Produktion): 
- **Key**: `ConnectionStrings__Supabase`
- **Value**: `Host=db.prjlfyahewmzqidfzmei.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=Asde2221;SSL Mode=Require;Trust Server Certificate=true`

(Der Wert sollte dem aus deiner `appsettings.Production.json` entsprechen oder von Supabase bereitgestellt werden.)

### 6. Deployment starten

1. Klicke auf "Create Web Service"
2. Render beginnt automatisch mit dem Build
3. Der erste Build dauert ~5-10 Minuten
4. Nach erfolgreichem Build ist deine App unter der Render-URL erreichbar

### 7. Deine App ist live! 🎉

Die App ist erreichbar unter: `https://tippspielweb.onrender.com` (oder dein gewählter Name)

## Wichtige Hinweise

### Datenpersistenz

✅ **Die Anwendung verwendet jetzt PostgreSQL (z.B. Supabase) für die Datenpersistenz!**

Das bedeutet, die `tippspiel_daten.json` Datei wird nicht mehr für die Speicherung der Anwendungsdaten verwendet und deine Daten gehen bei einem Neustart des Render-Dienstes **NICHT** mehr verloren. Stelle sicher, dass deine Supabase-Instanz korrekt konfiguriert ist und die Verbindungszeichenfolge stimmt.

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

Das Admin-Passwort ist in `Services/TippspielService.cs` definiert. Es wird beim ersten Start der Anwendung in der Datenbank gehasht und ein Admin-Benutzer angelegt, falls noch keiner existiert.

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

### Datenzugriffsprobleme
- Prüfe die PostgreSQL-Verbindungszeichenfolge in `appsettings.json` oder den Umgebungsvariablen.
- Stelle sicher, dass deine Supabase-Datenbankinstanz erreichbar und korrekt konfiguriert ist.

## Updates deployen

Nach Änderungen
...