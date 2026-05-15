using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RAGTEST.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Auth;
using System.Security.Claims;

namespace RAGTEST.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
                return View(model);

            var client = _httpClientFactory.CreateClient("Api");

            var request = new RegisterRequest
            {
                Login = model.Login.Trim(),
                Password = model.Password,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim()
            };

            var response = await client.PostAsJsonAsync("api/accountapi/register", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(error))
                    error = "Не удалось зарегистрировать пользователя.";

                ModelState.AddModelError("", error.Trim('"'));
                return View(model);
            }

            var user = await response.Content.ReadFromJsonAsync<AuthUserDto>();

            if (user == null)
            {
                ModelState.AddModelError("", "Не удалось получить данные пользователя.");
                return View(model);
            }

            await SignInUserAsync(user, false);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
                return View(model);

            var client = _httpClientFactory.CreateClient("Api");

            var request = new LoginRequest
            {
                LoginOrEmail = model.LoginOrEmail.Trim(),
                Password = model.Password
            };

            var response = await client.PostAsJsonAsync("api/accountapi/login", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(error))
                    error = "Неверный логин или пароль.";

                ModelState.AddModelError("", error.Trim('"'));
                return View(model);
            }

            var user = await response.Content.ReadFromJsonAsync<AuthUserDto>();

            if (user == null)
            {
                ModelState.AddModelError("", "Не удалось получить данные пользователя.");
                return View(model);
            }

            await SignInUserAsync(user, model.RememberMe);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        private async Task SignInUserAsync(AuthUserDto user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Login),
                new("Login", user.Login)
            };

            if (!string.IsNullOrWhiteSpace(user.Email))
                claims.Add(new Claim(ClaimTypes.Email, user.Email));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(14)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);
        }
    }
}