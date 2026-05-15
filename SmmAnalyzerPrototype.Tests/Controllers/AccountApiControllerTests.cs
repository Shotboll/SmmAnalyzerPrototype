using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Api.Controllers;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Auth;

namespace SmmAnalyzerPrototype.Tests.Controllers
{
    public class AccountApiControllerTests
    {
        private class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Ignore<Community>();
                modelBuilder.Ignore<Post>();
                modelBuilder.Ignore<AnalysisResult>();
                modelBuilder.Ignore<GrammarError>();
                modelBuilder.Ignore<ProhibitedTopicMatch>();
                modelBuilder.Ignore<CommunityPost>();
                modelBuilder.Ignore<RegulationDocument>();
                modelBuilder.Ignore<RegulationChunk>();

                modelBuilder.Entity<User>(entity =>
                {
                    entity.ToTable("users");

                    entity.HasKey(u => u.Id);

                    entity.Property(u => u.Login)
                        .IsRequired()
                        .HasMaxLength(255);

                    entity.Property(u => u.PasswordHash)
                        .IsRequired()
                        .HasMaxLength(500);

                    entity.Property(u => u.Email)
                        .HasMaxLength(255);

                    entity.Property(u => u.CreatedAt)
                        .HasColumnName("created_at");

                    entity.Ignore(u => u.Communities);

                    entity.HasIndex(u => u.Login)
                        .IsUnique();

                    entity.HasIndex(u => u.Email)
                        .IsUnique();
                });
            }
        }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"SmmAnalyzerTestDb_{Guid.NewGuid()}")
                .Options;

            return new TestAppDbContext(options);
        }

        [Fact]
        public async Task Register_ShouldCreateUser_WhenDataIsValid()
        {
            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new RegisterRequest
            {
                Login = "max",
                Password = "1234567",
                Email = "max@example.com"
            };

            var actionResult = await controller.Register(request);

            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var userDto = okResult.Value.Should().BeOfType<AuthUserDto>().Subject;

            userDto.Id.Should().NotBe(Guid.Empty);
            userDto.Login.Should().Be("max");
            userDto.Email.Should().Be("max@example.com");

            var savedUser = await context.Users.FirstOrDefaultAsync(x => x.Login == "max");

            savedUser.Should().NotBeNull();
            savedUser!.Login.Should().Be("max");
            savedUser.Email.Should().Be("max@example.com");
            savedUser.PasswordHash.Should().NotBeNullOrWhiteSpace();
            savedUser.PasswordHash.Should().NotBe("1234567");
        }

        [Fact]
        public async Task Register_ShouldTrimLoginAndEmail_WhenDataContainsSpaces()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new RegisterRequest
            {
                Login = "  max  ",
                Password = "1234567",
                Email = "  max@example.com  "
            };


            var actionResult = await controller.Register(request);


            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var userDto = okResult.Value.Should().BeOfType<AuthUserDto>().Subject;

            userDto.Login.Should().Be("max");
            userDto.Email.Should().Be("max@example.com");

            var savedUser = await context.Users.SingleAsync();
            savedUser.Login.Should().Be("max");
            savedUser.Email.Should().Be("max@example.com");
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenRequestIsNull()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);


            var actionResult = await controller.Register(null!);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Некорректные данные.");

            var usersCount = await context.Users.CountAsync();
            usersCount.Should().Be(0);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenLoginIsEmpty()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new RegisterRequest
            {
                Login = "   ",
                Password = "1234567",
                Email = "max@example.com"
            };


            var actionResult = await controller.Register(request);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Введите логин.");

            var usersCount = await context.Users.CountAsync();
            usersCount.Should().Be(0);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenPasswordIsEmpty()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new RegisterRequest
            {
                Login = "max",
                Password = "   ",
                Email = "max@example.com"
            };


            var actionResult = await controller.Register(request);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Введите пароль.");

            var usersCount = await context.Users.CountAsync();
            usersCount.Should().Be(0);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenPasswordLengthIsSix()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new RegisterRequest
            {
                Login = "max",
                Password = "123456",
                Email = "max@example.com"
            };


            var actionResult = await controller.Register(request);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Пароль должен содержать более 6 символов.");

            var usersCount = await context.Users.CountAsync();
            usersCount.Should().Be(0);
        }

        [Fact]
        public async Task Register_ShouldReturnConflict_WhenLoginAlreadyExists()
        {

            await using var context = CreateDbContext();

            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Login = "max",
                Email = "old@example.com",
                PasswordHash = "already_hashed_password",
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();

            var controller = new AccountApiController(context);

            var request = new RegisterRequest
            {
                Login = "max",
                Password = "1234567",
                Email = "new@example.com"
            };


            var actionResult = await controller.Register(request);


            var conflict = actionResult.Result.Should().BeOfType<ConflictObjectResult>().Subject;
            conflict.Value.Should().Be("Пользователь с таким логином уже существует.");

            var usersCount = await context.Users.CountAsync();
            usersCount.Should().Be(1);
        }

        [Fact]
        public async Task Register_ShouldReturnConflict_WhenEmailAlreadyExists()
        {

            await using var context = CreateDbContext();

            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Login = "old_user",
                Email = "max@example.com",
                PasswordHash = "already_hashed_password",
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();

            var controller = new AccountApiController(context);

            var request = new RegisterRequest
            {
                Login = "max",
                Password = "1234567",
                Email = "max@example.com"
            };


            var actionResult = await controller.Register(request);


            var conflict = actionResult.Result.Should().BeOfType<ConflictObjectResult>().Subject;
            conflict.Value.Should().Be("Пользователь с такой электронной почтой уже существует.");

            var usersCount = await context.Users.CountAsync();
            usersCount.Should().Be(1);
        }

        [Fact]
        public async Task Login_ShouldReturnUser_WhenLoginAndPasswordAreValid()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var registerRequest = new RegisterRequest
            {
                Login = "max",
                Password = "1234567",
                Email = "max@example.com"
            };

            await controller.Register(registerRequest);

            var loginRequest = new LoginRequest
            {
                LoginOrEmail = "max",
                Password = "1234567"
            };


            var actionResult = await controller.Login(loginRequest);


            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var userDto = okResult.Value.Should().BeOfType<AuthUserDto>().Subject;

            userDto.Id.Should().NotBe(Guid.Empty);
            userDto.Login.Should().Be("max");
            userDto.Email.Should().Be("max@example.com");
        }

        [Fact]
        public async Task Login_ShouldReturnUser_WhenEmailAndPasswordAreValid()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var registerRequest = new RegisterRequest
            {
                Login = "max",
                Password = "1234567",
                Email = "max@example.com"
            };

            await controller.Register(registerRequest);

            var loginRequest = new LoginRequest
            {
                LoginOrEmail = "max@example.com",
                Password = "1234567"
            };


            var actionResult = await controller.Login(loginRequest);


            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var userDto = okResult.Value.Should().BeOfType<AuthUserDto>().Subject;

            userDto.Login.Should().Be("max");
            userDto.Email.Should().Be("max@example.com");
        }

        [Fact]
        public async Task Login_ShouldReturnBadRequest_WhenRequestIsNull()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);


            var actionResult = await controller.Login(null!);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Некорректные данные.");
        }

        [Fact]
        public async Task Login_ShouldReturnBadRequest_WhenLoginOrEmailIsEmpty()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new LoginRequest
            {
                LoginOrEmail = "   ",
                Password = "1234567"
            };


            var actionResult = await controller.Login(request);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Введите логин или email.");
        }

        [Fact]
        public async Task Login_ShouldReturnBadRequest_WhenPasswordIsEmpty()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new LoginRequest
            {
                LoginOrEmail = "max",
                Password = "   "
            };


            var actionResult = await controller.Login(request);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Введите пароль.");
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WhenUserDoesNotExist()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var request = new LoginRequest
            {
                LoginOrEmail = "unknown",
                Password = "1234567"
            };


            var actionResult = await controller.Login(request);


            var unauthorized = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Неверный логин или пароль.");
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WhenPasswordIsInvalid()
        {

            await using var context = CreateDbContext();
            var controller = new AccountApiController(context);

            var registerRequest = new RegisterRequest
            {
                Login = "max",
                Password = "1234567",
                Email = "max@example.com"
            };

            await controller.Register(registerRequest);

            var loginRequest = new LoginRequest
            {
                LoginOrEmail = "max",
                Password = "wrong_password"
            };


            var actionResult = await controller.Login(loginRequest);


            var unauthorized = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Неверный логин или пароль.");
        }
    }
}