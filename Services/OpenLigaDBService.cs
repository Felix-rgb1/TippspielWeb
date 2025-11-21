using System.Text.Json;
using System.Text.Json.Serialization;

namespace TippspielWeb.Services;

public class OpenLigaDBService
{
    private readonly HttpClient _httpClient;
    private const string BASE_URL = "https://api.openligadb.de";
    
    public OpenLigaDBService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Hole aktuelle Bundesliga-Spiele
    public async Task<List<OpenLigaMatch>?> GetAktuellerSpieltagAsync(int saison = 2024, int spieltag = 1)
    {
        try
        {
            var url = $"{BASE_URL}/getmatchdata/bl1/{saison}/{spieltag}";
            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<OpenLigaMatch>>(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Abrufen der Spiele: {ex.Message}");
            return null;
        }
    }

    // Hole Live-Spiele
    public async Task<List<OpenLigaMatch>?> GetLiveSpieleAsync()
    {
        try
        {
            var url = $"{BASE_URL}/getmatchdata/bl1";
            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<OpenLigaMatch>>(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Abrufen der Live-Spiele: {ex.Message}");
            return null;
        }
    }

    // Hole verfügbare Spieltage
    public async Task<List<OpenLigaGroup>?> GetSpieltageAsync(int saison = 2024)
    {
        try
        {
            var url = $"{BASE_URL}/getavailablegroups/bl1/{saison}";
            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<OpenLigaGroup>>(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Abrufen der Spieltage: {ex.Message}");
            return null;
        }
    }
}

// OpenLigaDB Datenmodelle
public class OpenLigaMatch
{
    [JsonPropertyName("matchID")]
    public int MatchId { get; set; }
    
    [JsonPropertyName("matchDateTime")]
    public DateTime MatchDateTime { get; set; }
    
    [JsonPropertyName("matchIsFinished")]
    public bool MatchIsFinished { get; set; }
    
    [JsonPropertyName("matchResults")]
    public List<OpenLigaResult> MatchResults { get; set; } = new();
    
    [JsonPropertyName("team1")]
    public OpenLigaTeam Team1 { get; set; } = new();
    
    [JsonPropertyName("team2")]
    public OpenLigaTeam Team2 { get; set; } = new();
    
    [JsonPropertyName("group")]
    public OpenLigaGroup Group { get; set; } = new();

    // Hilfsmethode für finales Ergebnis
    public (int? heim, int? gast) GetErgebnis()
    {
        var endResult = MatchResults.FirstOrDefault(r => r.ResultName == "Endergebnis");
        if (endResult != null)
            return (endResult.PointsTeam1, endResult.PointsTeam2);
        return (null, null);
    }
}

public class OpenLigaResult
{
    [JsonPropertyName("resultName")]
    public string ResultName { get; set; } = string.Empty;
    
    [JsonPropertyName("pointsTeam1")]
    public int PointsTeam1 { get; set; }
    
    [JsonPropertyName("pointsTeam2")]
    public int PointsTeam2 { get; set; }
}

public class OpenLigaTeam
{
    [JsonPropertyName("teamId")]
    public int TeamId { get; set; }
    
    [JsonPropertyName("teamName")]
    public string TeamName { get; set; } = string.Empty;
    
    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = string.Empty;
    
    [JsonPropertyName("teamIconUrl")]
    public string TeamIconUrl { get; set; } = string.Empty;
}

public class OpenLigaGroup
{
    [JsonPropertyName("groupName")]
    public string GroupName { get; set; } = string.Empty;
    
    [JsonPropertyName("groupOrderID")]
    public int GroupOrderId { get; set; }
}
