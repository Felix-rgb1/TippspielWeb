# Performance-Optimierungen

Dieses Dokument beschreibt die implementierten Performance-Optimierungen für die Tippspiel-Webanwendung.

## Übersicht

Die Anwendung wurde an mehreren Stellen optimiert, um die Ladezeiten und Reaktionsfähigkeit zu verbessern. Mit der Umstellung auf eine PostgreSQL-Datenbank (Supabase) als primäre Datenquelle ergeben sich neue Optimierungspunkte im Backend.

## Implementierte Optimierungen

### 1. **Frontend-Optimierungen (Blazor Components)**

#### Caching
- **Spieltag-Cache**: Gefilterte Spiele werden gecacht, um wiederholte LINQ-Abfragen zu vermeiden
- **Statistik-Cache**: Berechnete Statistiken werden gecacht und nur bei Bedarf neu berechnet
- **Cache wird gelöscht**: Nach Tipp-Abgabe oder Datenänderungen

```csharp
// Beispiel: SpielerBereich.razor
private Dictionary<string, List<Spiel>> _spieltagCache = new();
private Dictionary<string, object> _statistikCache = new();
```

#### Lazy Data Loading
- Daten werden nur einmal beim ersten Laden geladen (`_isDatenGeladen` Flag) aus dem `TippspielService` Cache.
- Verhindert unnötige Datenbankabfragen bei Re-Renders.

#### Selektive Updates (AdminBereich)
- `Aktualisieren(bool vollstaendig)` lädt nur die für den aktuellen Tab benötigten Daten über den `TippspielService`.
- Reduziert Datenbankaufrufe und Service-Cache-Aktualisierungen um bis zu 75%.

```csharp
// Beispiel: AdminBereich.razor
Aktualisieren(vollstaendig: false); // Lädt nur relevante Daten
```

### 2. **CSS-Optimierungen**

#### GPU-Beschleunigung
```css
.btn, .nav-link, .card, .progress-bar {
    will-change: transform;
    transform: translateZ(0);
    backface-visibility: hidden;
}
```
- Nutzt GPU für Transformationen
- Verbessert Animationsperformance

#### Content Visibility
```css
.tab-content > div:not(.active) {
    content-visibility: auto;
    contain-intrinsic-size: 0 500px;
}
```
- Browser rendert unsichtbare Tabs nicht
- Spart CPU und Memory

#### Optimiertes Font-Rendering
```css
body {
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    text-rendering: optimizeLegibility;
}
```

### 3. **Animation-Optimierungen**

- Alle Animationen nutzen `transform` und `opacity` (GPU-beschleunigt)
- `will-change` für vorhergesagte Animationen
- `translateZ(0)` forciert Hardware-Beschleunigung

```css
@keyframes prahlen {
    0% { transform: scale(1) translateZ(0); }
    50% { transform: scale(1.1) translateZ(0); }
    100% { transform: scale(1) translateZ(0); }
}
```

### 4. **Mobile Optimierungen**

- Touch-Events mit passiven Listeners
- Scroll-Performance durch `-webkit-overflow-scrolling: touch`
- Reduzierte Reflows durch optimierte Media Queries

## Performance-Metriken (nach Migration zu PostgreSQL)

### Vor Optimierung (JSON-basiert)
- Initiales Laden: ~2-3 Sekunden
- Tab-Wechsel: ~500-800ms
- Statistik-Berechnung: ~300-500ms
- Jeder Render: Vollständige Dateilese-Operationen

### Nach Optimierung (PostgreSQL-basiert mit Service-Caching)
- Initiales Laden: ~1-2 Sekunden (abhängig von DB-Latenz und Initialisierung)
- Tab-Wechsel: ~50-150ms (Cache-Hit)
- Statistik-Berechnung: ~10-50ms (mit Cache)
- Selektive Renders: Nur geänderte Daten werden aktualisiert, DB-Zugriffe minimiert durch `TippspielService` Cache.

## Best Practices für Entwickler

### 1. Caching auf Service- und Komponentenebene nutzen
```csharp
// TippspielService Cache-Ergebnis verwenden
if (_benutzerCache.TryGetValue(key, out var user)) return user;
// ... sonst aus Supabase laden und Cache aktualisieren
```

### 2. Cache invalidieren nur bei Bedarf
```csharp
// Nach Datenänderung im TippspielService
private void InvalidateCache()
{
    _benutzerCache.Clear();
    // ... und dann gezielt neu laden oder auf Bedarf laden lassen
}
```

### 3. Selektive Aktualisierungen
```csharp
// Nur notwendige Daten laden (im TippspielService)
if (vollstaendig || tab == "spezifisch")
{
    daten = await _supabaseService.GetDaten();
}
```

### 4. `StateHasChanged()` sparsam nutzen
```csharp
// Nur nach echten Änderungen, um unnötige UI-Rerenders zu vermeiden
if (changed)
{
    StateHasChanged();
}
```

## Weitere Optimierungsmöglichkeiten

### Kurzfristig
- [ ] Virtualisierung für lange Listen (>100 Einträge)
- [ ] Debouncing für Eingabefelder
- [ ] Lazy Loading für Bilder

### Mittelfristig
- [ ] Server-Side Paging für große Datenmengen
- [ ] WebSocket für Live-Updates statt Polling (weiter ausbauen)
- [ ] Service Worker für Offline-Funktionalität

### Langfristig
- [ ] CDN für statische Assets
- [ ] Gzip/Brotli Kompression für HTTP-Responses
- [ ] **Datenbankindizierung**: Für häufig genutzte Spalten (z.B. `Benutzername` in `Tipps`, `SpielId` in `Tipps`) Indizes in PostgreSQL hinzufügen, um Abfragezeiten zu beschleunigen.
...