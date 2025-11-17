using System.Security.Cryptography;
using System.Text;

namespace TippspielWeb.Services;

public class AuthService
{
    private string? _aktuellerBenutzer = null;
    private bool _istAdmin = false;

    public string? AktuellerBenutzer => _aktuellerBenutzer;
    public bool IstAngemeldet => !string.IsNullOrEmpty(_aktuellerBenutzer);
    public bool IstAdmin => _istAdmin;

    public event Action? OnAuthStateChanged;

    public void Anmelden(string benutzername, bool istAdmin)
    {
        _aktuellerBenutzer = benutzername;
        _istAdmin = istAdmin;
        OnAuthStateChanged?.Invoke();
    }

    public void Abmelden()
    {
        _aktuellerBenutzer = null;
        _istAdmin = false;
        OnAuthStateChanged?.Invoke();
    }

    public static string HashPasswort(string passwort)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(passwort);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPasswort(string passwort, string hash)
    {
        return HashPasswort(passwort) == hash;
    }
}
