using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MVC_EF_Start_8.Models;
using MVC_EF_Start_8.Services;

namespace MVC_EF_Start_8.Controllers
{
    public class AuthController : Controller
    {
        // Shared with Program.cs's cookie-extraction logic -- both need to
        // agree on the same cookie name.
        public const string AuthCookieName = "auth_token";

        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (user, error) = await _authService.RegisterAsync(model.Username, model.Email, model.Password);
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            SignInWithJwt(user!);
            return RedirectToAction("Read", "Home");
        }

        public IActionResult Login() => View(new LoginViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _authService.ValidateCredentialsAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            SignInWithJwt(user);
            return RedirectToAction("Read", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            Response.Cookies.Delete(AuthCookieName);
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Secure is conditional on the current request's scheme, not
        /// hardcoded true -- see README's Step 3 notes. The app runs over
        /// plain HTTP in local Docker (http://localhost:8090); a hardcoded
        /// Secure cookie would silently never get sent back by the browser
        /// there, breaking login locally, even though it's exactly right
        /// once deployed to Render with real HTTPS.
        /// </summary>
        private void SignInWithJwt(User user)
        {
            var token = _authService.GenerateJwt(user);
            Response.Cookies.Append(AuthCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = System.DateTimeOffset.UtcNow.AddDays(1),
            });
        }
    }
}
