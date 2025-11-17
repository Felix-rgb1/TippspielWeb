# GitHub Upload Anleitung (ohne Git)

Da Git nicht auf deinem System installiert ist, kannst du das Projekt manuell zu GitHub hochladen.

## Methode 1: GitHub Web Interface (Einfachste Methode)

### Schritt 1: Projekt vorbereiten

1. **Erstelle einen ZIP des Projekts**:
   - Gehe zu `C:\Users\User\Desktop\C#\Test\`
   - Rechtsklick auf den Ordner `TippspielWeb`
   - "Senden an" → "ZIP-komprimierter Ordner"
   - Nenne ihn `TippspielWeb.zip`

2. **Lösche temporäre Ordner** (WICHTIG - VOR dem Zippen oder danach aus dem ZIP entfernen):
   - `bin/`
   - `obj/`
   - `publish/`
   - `TippspielWeb_Deploy/`
   - `.vs/`

### Schritt 2: GitHub Repository erstellen

1. Gehe zu https://github.com
2. Klicke oben rechts auf "+" → "New repository"
3. Repository-Einstellungen:
   - **Repository name**: `TippspielWeb`
   - **Description**: "ASP.NET Blazor Tippspiel-Anwendung"
   - **Public** oder **Private** (deine Wahl)
   - ❌ **NICHT** "Add a README file" anhaken
   - ❌ **NICHT** .gitignore hinzufügen
   - ❌ **NICHT** License wählen
4. Klicke "Create repository"

### Schritt 3: Dateien hochladen

1. Auf der Repository-Seite klicke "uploading an existing file"
2. Ziehe die entpackten Dateien in das Upload-Feld ODER:
   - Entpacke `TippspielWeb.zip`
   - Wähle ALLE Dateien im Ordner aus
   - Ziehe sie ins GitHub-Fenster
3. **Commit-Nachricht**: "Initial commit - TippspielWeb"
4. Klicke "Commit changes"

⚠️ **Wichtig**: Lade NICHT diese Ordner hoch:
- `bin/`
- `obj/`
- `publish/`
- `TippspielWeb_Deploy/`

## Methode 2: GitHub Desktop (Empfohlen für regelmäßige Updates)

### Installation:

1. Downloade GitHub Desktop: https://desktop.github.com/
2. Installiere und melde dich an

### Repository erstellen:

1. Klicke "File" → "New repository"
2. **Name**: `TippspielWeb`
3. **Local Path**: `C:\Users\User\Desktop\C#\Test`
4. **Git ignore**: `VisualStudio`
5. Klicke "Create repository"

### Code hochladen:

1. GitHub Desktop zeigt alle Änderungen
2. Schreibe Commit-Message: "Initial commit"
3. Klicke "Commit to main"
4. Klicke "Publish repository"
5. Wähle Public/Private
6. Klicke "Publish repository"

## Methode 3: Git installieren (Terminal)

### Git installieren:

1. Downloade Git: https://git-scm.com/download/win
2. Installiere mit Standard-Einstellungen
3. Öffne PowerShell neu

### Repository erstellen und hochladen:

```powershell
cd "C:\Users\User\Desktop\C#\Test\TippspielWeb"

# Git initialisieren
git init

# Dateien hinzufügen
git add .

# Ersten Commit
git commit -m "Initial commit - TippspielWeb"

# GitHub Repository verbinden (ersetze USERNAME)
git remote add origin https://github.com/USERNAME/TippspielWeb.git

# Hochladen
git branch -M main
git push -u origin main
```

## Nach dem Upload

### Weiter mit Render Deployment:

1. Öffne https://render.com
2. Melde dich an (oder erstelle Account)
3. Klicke "New +" → "Web Service"
4. Verbinde GitHub Account
5. Wähle `TippspielWeb` Repository
6. **Build Command**: `dotnet publish -c Release -o out`
7. **Start Command**: `cd out && dotnet TippspielWeb.dll --urls "http://0.0.0.0:$PORT"`
8. Wähle "Free" oder "Starter" Plan
9. Klicke "Create Web Service"

### Deine App ist live unter:
`https://tippspielweb.onrender.com`

## Dateien die NICHT hochgeladen werden sollen

Die `.gitignore` Datei verhindert automatisch Upload von:
- `bin/` - Build-Ausgaben
- `obj/` - Temporäre Build-Dateien  
- `publish/` - Publish-Ausgaben
- `.vs/` - Visual Studio Cache
- `TippspielWeb_Deploy/` - Alte Deploy-Ordner
- `*.user` - Benutzer-spezifische Einstellungen

## Wichtige Dateien für Deployment

✅ **Diese Dateien MÜSSEN hochgeladen werden**:
- `*.cs` - Alle C# Dateien
- `*.razor` - Alle Razor-Komponenten
- `*.csproj` - Projekt-Datei
- `*.sln` - Solution-Datei
- `appsettings.json`
- `Program.cs`
- `wwwroot/` - Kompletter Ordner
- `Components/` - Kompletter Ordner
- `Models/` - Kompletter Ordner
- `Services/` - Kompletter Ordner
- `.gitignore`
- `README.md`
- `render.yaml`

## Troubleshooting

### "File too large"
- Stelle sicher, dass `bin/` und `obj/` nicht hochgeladen werden
- GitHub hat 100MB Datei-Limit

### "Repository is empty"
- Du hast vergessen Dateien hochzuladen
- Nutze "uploading an existing file" Link

### Updates hochladen
- **Web**: Lösche alte Dateien, lade neue hoch
- **GitHub Desktop**: Commit → Push
- **Git**: `git add .` → `git commit -m "Update"` → `git push`

Viel Erfolg! 🚀
