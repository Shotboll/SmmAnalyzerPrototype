using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Auth;

namespace SmmAnalyzerPrototype.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AccountApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public AccountApiController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        [HttpPost]
        public async Task<ActionResult<AuthUserDto>> Register([FromBody] RegisterRequest request)
        {
            if (request == null)
                return BadRequest("Некорректные данные.");

            if (string.IsNullOrWhiteSpace(request.Login))
                return BadRequest("Введите логин.");

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Введите пароль.");

            if (request.Password.Length <= 6)
                return BadRequest("Пароль должен содержать более 6 символов.");

            var normalizedLogin = request.Login.Trim();
            var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : request.Email.Trim();

            var loginExists = await _context.Users
                .AnyAsync(x => x.Login == normalizedLogin);

            if (loginExists)
                return Conflict("Пользователь с таким логином уже существует.");

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                var emailExists = await _context.Users
                    .AnyAsync(x => x.Email == normalizedEmail);

                if (emailExists)
                    return Conflict("Пользователь с такой электронной почтой уже существует.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Login = normalizedLogin,
                Email = normalizedEmail,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new AuthUserDto
            {
                Id = user.Id,
                Login = user.Login,
                Email = user.Email
            });
        }

        [HttpPost]
        public async Task<ActionResult<AuthUserDto>> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest("Некорректные данные.");

            if (string.IsNullOrWhiteSpace(request.LoginOrEmail))
                return BadRequest("Введите логин или email.");

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Введите пароль.");

            var loginOrEmail = request.LoginOrEmail.Trim();

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Login == loginOrEmail || x.Email == loginOrEmail);

            if (user == null)
                return Unauthorized("Неверный логин или пароль.");

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Неверный логин или пароль.");

            return Ok(new AuthUserDto
            {
                Id = user.Id,
                Login = user.Login,
                Email = user.Email
            });
        }
    }
}