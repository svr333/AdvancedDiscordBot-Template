using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedBot.Dashboard.Data
{
    public class AccountController : ControllerBase
    {
        public IActionResult Login(string returnUrl = "/server-picker")
        {
            return Challenge(new AuthenticationProperties { RedirectUri = returnUrl}, "Discord");
        }

        public async Task<IActionResult> LogOut(string returnUrl = "/")
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return LocalRedirect(returnUrl);
        }
    }
}
