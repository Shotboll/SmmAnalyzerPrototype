using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmmAnalyzerPrototype.Api.Controllers;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;

namespace SmmAnalyzerPrototype.Tests.Controllers
{
    public class CommunityApiControllerTests
    {
        private class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
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
                });

                modelBuilder.Entity<Community>(entity =>
                {
                    entity.ToTable("communities");
                    entity.HasKey(c => c.Id);

                    entity.Property(c => c.Name)
                        .IsRequired()
                        .HasMaxLength(255);

                    entity.Property(c => c.TargetAudience)
                        .HasMaxLength(255);

                    entity.Property(c => c.StyleProfile)
                        .HasMaxLength(255);

                    entity.Ignore(c => c.User);
                    entity.Ignore(c => c.Posts);
                    entity.Ignore(c => c.CommunityPosts);
                    entity.Ignore(c => c.RegulationDocuments);

                    entity.HasIndex(c => c.UserId);
                });
            }
        }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"SmmAnalyzerCommunityTestDb_{Guid.NewGuid()}")
                .Options;

            return new TestAppDbContext(options);
        }

        private static CommunityApiController CreateController(AppDbContext context, Guid? userId = null)
        {
            var controller = new CommunityApiController(
                context,
                vkService: null!,
                logger: NullLogger<CommunityApiController>.Instance);

            var httpContext = new DefaultHttpContext();

            if (userId.HasValue)
                httpContext.Request.Headers["X-User-Id"] = userId.Value.ToString();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        [Fact]
        public async Task GetAll_ShouldReturnUnauthorized_WhenUserHeaderIsMissing()
        {

            await using var context = CreateDbContext();
            var controller = CreateController(context);


            var actionResult = await controller.GetAll();


            var unauthorized = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Пользователь не определен.");
        }

        [Fact]
        public async Task GetAll_ShouldReturnOnlyCurrentUserCommunities()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            context.Communities.AddRange(
                new Community
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserId,
                    Name = "Мое сообщество 2",
                    TargetAudience = "Студенты",
                    StyleProfile = "Дружелюбный"
                },
                new Community
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserId,
                    Name = "Мое сообщество 1",
                    TargetAudience = "Абитуриенты",
                    StyleProfile = "Информационный"
                },
                new Community
                {
                    Id = Guid.NewGuid(),
                    UserId = otherUserId,
                    Name = "Чужое сообщество",
                    TargetAudience = "Другая аудитория",
                    StyleProfile = "Другой стиль"
                });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);


            var actionResult = await controller.GetAll();


            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var communities = okResult.Value.Should().BeAssignableTo<List<CommunityDto>>().Subject;

            communities.Should().HaveCount(2);
            communities.Select(x => x.Name).Should().Equal("Мое сообщество 1", "Мое сообщество 2");
            communities.Should().NotContain(x => x.Name == "Чужое сообщество");
        }

        [Fact]
        public async Task GetById_ShouldReturnCommunity_WhenCommunityBelongsToCurrentUser()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var communityId = Guid.NewGuid();

            context.Communities.Add(new Community
            {
                Id = communityId,
                UserId = currentUserId,
                Name = "Мое сообщество",
                TargetAudience = "Студенты",
                StyleProfile = "Дружелюбный",
                VkInput = "club123"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);


            var actionResult = await controller.GetById(communityId);


            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var community = okResult.Value.Should().BeOfType<CommunityDto>().Subject;

            community.Id.Should().Be(communityId);
            community.Name.Should().Be("Мое сообщество");
            community.TargetAudience.Should().Be("Студенты");
            community.StyleProfile.Should().Be("Дружелюбный");
            community.VkInput.Should().Be("club123");
        }

        [Fact]
        public async Task GetById_ShouldReturnNotFound_WhenCommunityBelongsToAnotherUser()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var communityId = Guid.NewGuid();

            context.Communities.Add(new Community
            {
                Id = communityId,
                UserId = otherUserId,
                Name = "Чужое сообщество"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);


            var actionResult = await controller.GetById(communityId);


            var notFound = actionResult.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.Value.Should().Be("Сообщество не найдено.");
        }

        [Fact]
        public async Task Create_ShouldAttachCommunityToCurrentUser_WhenDataIsValid()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var controller = CreateController(context, currentUserId);

            var request = new CreateCommunityRequest
            {
                Name = "  Новое сообщество  ",
                TargetAudience = "  Молодая аудитория  ",
                StyleProfile = "  Неформальный стиль  ",
                VkInput = null
            };


            var actionResult = await controller.Create(request);


            var createdResult = actionResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var communityDto = createdResult.Value.Should().BeOfType<CommunityDto>().Subject;

            communityDto.Id.Should().NotBe(Guid.Empty);
            communityDto.Name.Should().Be("Новое сообщество");
            communityDto.TargetAudience.Should().Be("Молодая аудитория");
            communityDto.StyleProfile.Should().Be("Неформальный стиль");

            var savedCommunity = await context.Communities.SingleAsync();

            savedCommunity.UserId.Should().Be(currentUserId);
            savedCommunity.Name.Should().Be("Новое сообщество");
            savedCommunity.TargetAudience.Should().Be("Молодая аудитория");
            savedCommunity.StyleProfile.Should().Be("Неформальный стиль");
        }

        [Fact]
        public async Task Create_ShouldReturnUnauthorized_WhenUserHeaderIsMissing()
        {

            await using var context = CreateDbContext();
            var controller = CreateController(context);

            var request = new CreateCommunityRequest
            {
                Name = "Новое сообщество"
            };


            var actionResult = await controller.Create(request);


            var unauthorized = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Пользователь не определен.");

            var communitiesCount = await context.Communities.CountAsync();
            communitiesCount.Should().Be(0);
        }

        [Fact]
        public async Task Create_ShouldReturnBadRequest_WhenNameIsEmpty()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var controller = CreateController(context, currentUserId);

            var request = new CreateCommunityRequest
            {
                Name = "   ",
                TargetAudience = "Аудитория",
                StyleProfile = "Стиль"
            };


            var actionResult = await controller.Create(request);


            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Название сообщества не может быть пустым.");

            var communitiesCount = await context.Communities.CountAsync();
            communitiesCount.Should().Be(0);
        }

        [Fact]
        public async Task Update_ShouldUpdateCommunity_WhenCommunityBelongsToCurrentUser()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var communityId = Guid.NewGuid();

            context.Communities.Add(new Community
            {
                Id = communityId,
                UserId = currentUserId,
                Name = "Старое название",
                TargetAudience = "Старая аудитория",
                StyleProfile = "Старый стиль",
                VkInput = "old"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var request = new UpdateCommunityRequest
            {
                Name = "  Новое название  ",
                TargetAudience = "  Новая аудитория  ",
                StyleProfile = "  Новый стиль  ",
                VkInput = null
            };


            var result = await controller.Update(communityId, request);


            result.Should().BeOfType<NoContentResult>();

            var updatedCommunity = await context.Communities.SingleAsync(x => x.Id == communityId);

            updatedCommunity.Name.Should().Be("Новое название");
            updatedCommunity.TargetAudience.Should().Be("Новая аудитория");
            updatedCommunity.StyleProfile.Should().Be("Новый стиль");
            updatedCommunity.VkInput.Should().BeNull();
            updatedCommunity.VkGroupId.Should().BeNull();
            updatedCommunity.VkScreenName.Should().BeNull();
            updatedCommunity.VkUrl.Should().BeNull();
            updatedCommunity.VkPostsSyncedAt.Should().BeNull();
        }

        [Fact]
        public async Task Update_ShouldReturnNotFound_WhenCommunityBelongsToAnotherUser()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var communityId = Guid.NewGuid();

            context.Communities.Add(new Community
            {
                Id = communityId,
                UserId = otherUserId,
                Name = "Чужое сообщество",
                TargetAudience = "Чужая аудитория",
                StyleProfile = "Чужой стиль"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var request = new UpdateCommunityRequest
            {
                Name = "Попытка изменения",
                TargetAudience = "Новая аудитория",
                StyleProfile = "Новый стиль"
            };


            var result = await controller.Update(communityId, request);


            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.Value.Should().Be("Сообщество не найдено.");

            var community = await context.Communities.SingleAsync(x => x.Id == communityId);

            community.Name.Should().Be("Чужое сообщество");
            community.TargetAudience.Should().Be("Чужая аудитория");
            community.StyleProfile.Should().Be("Чужой стиль");
        }

        [Fact]
        public async Task Update_ShouldReturnBadRequest_WhenNameIsEmpty()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var communityId = Guid.NewGuid();

            context.Communities.Add(new Community
            {
                Id = communityId,
                UserId = currentUserId,
                Name = "Сообщество"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var request = new UpdateCommunityRequest
            {
                Name = "   "
            };


            var result = await controller.Update(communityId, request);


            var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Название сообщества не может быть пустым.");

            var community = await context.Communities.SingleAsync(x => x.Id == communityId);
            community.Name.Should().Be("Сообщество");
        }

        [Fact]
        public async Task Delete_ShouldDeleteCommunity_WhenCommunityBelongsToCurrentUser()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var communityId = Guid.NewGuid();

            context.Communities.Add(new Community
            {
                Id = communityId,
                UserId = currentUserId,
                Name = "Удаляемое сообщество"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);


            var result = await controller.Delete(communityId);


            result.Should().BeOfType<NoContentResult>();

            var communityExists = await context.Communities.AnyAsync(x => x.Id == communityId);
            communityExists.Should().BeFalse();
        }

        [Fact]
        public async Task Delete_ShouldReturnNotFound_WhenCommunityBelongsToAnotherUser()
        {

            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var communityId = Guid.NewGuid();

            context.Communities.Add(new Community
            {
                Id = communityId,
                UserId = otherUserId,
                Name = "Чужое сообщество"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);


            var result = await controller.Delete(communityId);


            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.Value.Should().Be("Сообщество не найдено.");

            var communityExists = await context.Communities.AnyAsync(x => x.Id == communityId);
            communityExists.Should().BeTrue();
        }
    }
}