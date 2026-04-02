using System.Security.Claims;
using TippspielWeb.Models;

namespace TippspielWeb.Services
{
    public class AuthService
    {
        private readonly TippspielService _tippspielService;

        public bool IstAngemeldet { get; private set; }
        public bool IstAdmin { get; private set; }
        public string AktuellerBenutzer { get; private set; } = string.Empty;

        public AuthService(TippspielService tippspielService)
        {
            _tippspielService = tippspielService;
        }

        public void Anmelden(string benutzername, bool istAdmin)
        {
            AktuellerBenutzer = benutzername;
            IstAdmin = istAdmin;
            IstAngemeldet = true;
        }

        public void Abmelden()
        {
            AktuellerBenutzer = string.Empty;
            IstAdmin = false;
            IstAngemeldet = false;
        }

        public async Task<ClaimsPrincipal?> ValidateLogin(string benutzername, string passwort)
        {
            if (await _tippspielService.BestaetigeLogin(benutzername, passwort))
            {
                var benutzer = await _tippspielService.GetBenutzer(benutzername);
                if (benutzer != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, benutzer.Benutzername),
                        new Claim(ClaimTypes.Role, benutzer.IstAdmin ? "Admin" : "User")
                    };

                    var identity = new ClaimsIdentity(claims, "TippspielAuth");
                    return new ClaimsPrincipal(identity);
                }
            }
            return null;
        }

        public async Task<ClaimsPrincipal?> ValidateAdminLogin(string passwort)
        {
            if (await _tippspielService.BestaetigeAdminPasswort(passwort))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, "Admin"),
                    new Claim(ClaimTypes.Role, "Admin")
                };
                var identity = new ClaimsIdentity(claims, "TippspielAdminAuth");
                return new ClaimsPrincipal(identity);
            }
            return null;
        }
    }
}