using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Api.Controllers;
using SmmAnalyzerPrototype.Api.Services;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Regualtion;

namespace SmmAnalyzerPrototype.Tests.Controllers
{
    public class RegulationApiControllerTests
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

                    entity.HasMany(c => c.RegulationDocuments)
                        .WithOne(r => r.Community)
                        .HasForeignKey(r => r.CommunityId);
                });

                modelBuilder.Entity<RegulationDocument>(entity =>
                {
                    entity.ToTable("regulation_documents");
                    entity.HasKey(r => r.Id);

                    entity.Property(r => r.Title)
                        .IsRequired()
                        .HasMaxLength(255);

                    entity.Property(r => r.Content)
                        .IsRequired();

                    entity.Property(r => r.Category)
                        .HasMaxLength(255);

                    entity.HasOne(r => r.Community)
                        .WithMany(c => c.RegulationDocuments)
                        .HasForeignKey(r => r.CommunityId);

                    entity.HasMany(r => r.Chunks)
                        .WithOne(c => c.Regulation)
                        .HasForeignKey(c => c.RegulationId);
                });

                modelBuilder.Entity<RegulationChunk>(entity =>
                {
                    entity.ToTable("regulation_chunks");
                    entity.HasKey(c => c.Id);

                    entity.Property(c => c.ChunkIndex)
                        .HasColumnName("chunk_index");

                    entity.Property(c => c.ChunkText)
                        .HasColumnName("chunk_text");

                    entity.Property(c => c.CreatedAt)
                        .HasColumnName("created_at");

                    entity.Ignore(c => c.Embedding);

                    entity.HasOne(c => c.Regulation)
                        .WithMany(r => r.Chunks)
                        .HasForeignKey(c => c.RegulationId);
                });
            }
        }

        private class FakeEmbeddingService : IEmbeddingService
        {
            public List<string> RequestedTexts { get; } = new();

            public Task<float[]> GetEmbeddingAsync(string text, bool isQuery = false)
            {
                RequestedTexts.Add(text);

                var embedding = Enumerable
                    .Repeat(0.1f, 1024)
                    .ToArray();

                return Task.FromResult(embedding);
            }
        }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"SmmAnalyzerRegulationTestDb_{Guid.NewGuid()}")
                .Options;

            return new TestAppDbContext(options);
        }

        private static RegulationApiController CreateController(AppDbContext context, IEmbeddingService embeddingService, Guid? userId = null)
        {
            var controller = new RegulationApiController(context, embeddingService);

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
            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService);

            var actionResult = await controller.GetAll(null);

            var unauthorized = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Пользователь не определен.");
        }

        [Fact]
        public async Task GetAll_ShouldReturnOnlyCurrentUserRegulations()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var currentCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var otherCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                Name = "Чужое сообщество"
            };

            context.Communities.AddRange(currentCommunity, otherCommunity);

            context.RegulationDocuments.AddRange(
                new RegulationDocument
                {
                    Id = Guid.NewGuid(),
                    CommunityId = currentCommunity.Id,
                    Community = currentCommunity,
                    Title = "Мой регламент 2",
                    Content = "Содержание регламента 2",
                    Category = "Контент"
                },
                new RegulationDocument
                {
                    Id = Guid.NewGuid(),
                    CommunityId = currentCommunity.Id,
                    Community = currentCommunity,
                    Title = "Мой регламент 1",
                    Content = "Содержание регламента 1",
                    Category = "Стиль"
                },
                new RegulationDocument
                {
                    Id = Guid.NewGuid(),
                    CommunityId = otherCommunity.Id,
                    Community = otherCommunity,
                    Title = "Чужой регламент",
                    Content = "Чужое содержание",
                    Category = "Чужая категория"
                });

            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var actionResult = await controller.GetAll(null);

            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var regulations = okResult.Value.Should().BeAssignableTo<List<RegulationDocumentDto>>().Subject;

            regulations.Should().HaveCount(2);
            regulations.Select(x => x.Title).Should().Contain("Мой регламент 1");
            regulations.Select(x => x.Title).Should().Contain("Мой регламент 2");
            regulations.Should().NotContain(x => x.Title == "Чужой регламент");
        }

        [Fact]
        public async Task GetAll_ShouldFilterByCommunityId_WhenCommunityBelongsToCurrentUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();

            var firstCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Первое сообщество"
            };

            var secondCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Второе сообщество"
            };

            context.Communities.AddRange(firstCommunity, secondCommunity);

            context.RegulationDocuments.AddRange(
                new RegulationDocument
                {
                    Id = Guid.NewGuid(),
                    CommunityId = firstCommunity.Id,
                    Community = firstCommunity,
                    Title = "Регламент первого сообщества",
                    Content = "Содержание"
                },
                new RegulationDocument
                {
                    Id = Guid.NewGuid(),
                    CommunityId = secondCommunity.Id,
                    Community = secondCommunity,
                    Title = "Регламент второго сообщества",
                    Content = "Содержание"
                });

            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var actionResult = await controller.GetAll(firstCommunity.Id);

            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var regulations = okResult.Value.Should().BeAssignableTo<List<RegulationDocumentDto>>().Subject;

            regulations.Should().HaveCount(1);
            regulations[0].Title.Should().Be("Регламент первого сообщества");
            regulations[0].CommunityId.Should().Be(firstCommunity.Id);
        }

        [Fact]
        public async Task GetById_ShouldReturnRegulation_WhenItBelongsToCurrentUserCommunity()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();

            var community = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var regulation = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                CommunityId = community.Id,
                Community = community,
                Title = "Правила публикаций",
                Content = "Публикации должны соответствовать стилю сообщества.",
                Category = "Контент"
            };

            regulation.Chunks.Add(new RegulationChunk
            {
                Id = Guid.NewGuid(),
                RegulationId = regulation.Id,
                Regulation = regulation,
                ChunkText = "Публикации должны соответствовать стилю сообщества.",
                ChunkIndex = 0,
                CreatedAt = DateTime.UtcNow
            });

            context.Communities.Add(community);
            context.RegulationDocuments.Add(regulation);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var actionResult = await controller.GetById(regulation.Id);

            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.Should().BeOfType<RegulationDocumentDto>().Subject;

            dto.Id.Should().Be(regulation.Id);
            dto.Title.Should().Be("Правила публикаций");
            dto.Content.Should().Be("Публикации должны соответствовать стилю сообщества.");
            dto.Category.Should().Be("Контент");
            dto.CommunityId.Should().Be(community.Id);
            dto.Chunks.Should().NotBeNull();
            dto.Chunks!.Should().HaveCount(1);
            dto.Chunks[0].Text.Should().Be("Публикации должны соответствовать стилю сообщества.");
            dto.Chunks[0].Index.Should().Be(0);
        }

        [Fact]
        public async Task GetById_ShouldReturnNotFound_WhenRegulationBelongsToAnotherUserCommunity()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var otherCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                Name = "Чужое сообщество"
            };

            var regulation = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                CommunityId = otherCommunity.Id,
                Community = otherCommunity,
                Title = "Чужой регламент",
                Content = "Чужое содержание"
            };

            context.Communities.Add(otherCommunity);
            context.RegulationDocuments.Add(regulation);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var actionResult = await controller.GetById(regulation.Id);

            actionResult.Result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Create_ShouldCreateRegulationWithChunks_WhenCommunityBelongsToCurrentUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();

            var community = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            context.Communities.Add(community);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var request = new CreateRegulationRequest
            {
                CommunityId = community.Id,
                Title = "  Регламент публикаций  ",
                Content = "Первый пункт регламента.\n\nВторой пункт регламента.",
                Category = "  Контент  "
            };

            var actionResult = await controller.Create(request);

            var createdResult = actionResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var dto = createdResult.Value.Should().BeOfType<RegulationDocumentDto>().Subject;

            dto.Id.Should().NotBe(Guid.Empty);
            dto.Title.Should().Be("  Регламент публикаций  ");
            dto.Content.Should().Be("Первый пункт регламента.\n\nВторой пункт регламента.");
            dto.Category.Should().Be("  Контент  ");
            dto.CommunityId.Should().Be(community.Id);
            dto.Chunks.Should().NotBeNull();
            dto.Chunks!.Should().HaveCount(2);

            var savedDocument = await context.RegulationDocuments
                .Include(x => x.Chunks)
                .SingleAsync();

            savedDocument.CommunityId.Should().Be(community.Id);
            savedDocument.Title.Should().Be("  Регламент публикаций  ");
            savedDocument.Chunks.Should().HaveCount(2);
            savedDocument.Chunks.Select(x => x.ChunkIndex).Should().BeEquivalentTo(new[] { 0, 1 });
            savedDocument.Chunks.Select(x => x.ChunkText).Should().Contain("Первый пункт регламента.");
            savedDocument.Chunks.Select(x => x.ChunkText).Should().Contain("Второй пункт регламента.");

            embeddingService.RequestedTexts.Should().HaveCount(2);
            embeddingService.RequestedTexts.Should().Contain("Первый пункт регламента.");
            embeddingService.RequestedTexts.Should().Contain("Второй пункт регламента.");
        }

        [Fact]
        public async Task Create_ShouldReturnUnauthorized_WhenUserHeaderIsMissing()
        {
            await using var context = CreateDbContext();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService);

            var request = new CreateRegulationRequest
            {
                CommunityId = Guid.NewGuid(),
                Title = "Регламент",
                Content = "Содержание",
                Category = "Контент"
            };

            var actionResult = await controller.Create(request);

            var unauthorized = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Пользователь не определен.");

            var documentsCount = await context.RegulationDocuments.CountAsync();
            documentsCount.Should().Be(0);
            embeddingService.RequestedTexts.Should().BeEmpty();
        }

        [Fact]
        public async Task Create_ShouldReturnBadRequest_WhenCommunityBelongsToAnotherUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var otherCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                Name = "Чужое сообщество"
            };

            context.Communities.Add(otherCommunity);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var request = new CreateRegulationRequest
            {
                CommunityId = otherCommunity.Id,
                Title = "Регламент",
                Content = "Содержание",
                Category = "Контент"
            };

            var actionResult = await controller.Create(request);

            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Сообщество не найдено или недоступно текущему пользователю.");

            var documentsCount = await context.RegulationDocuments.CountAsync();
            documentsCount.Should().Be(0);
            embeddingService.RequestedTexts.Should().BeEmpty();
        }

        [Fact]
        public async Task Update_ShouldUpdateRegulationAndReplaceChunks_WhenRegulationBelongsToCurrentUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();

            var community = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var regulation = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                CommunityId = community.Id,
                Community = community,
                Title = "Старый регламент",
                Content = "Старое содержание",
                Category = "Старая категория"
            };

            regulation.Chunks.Add(new RegulationChunk
            {
                Id = Guid.NewGuid(),
                RegulationId = regulation.Id,
                Regulation = regulation,
                ChunkText = "Старый чанк",
                ChunkIndex = 0,
                CreatedAt = DateTime.UtcNow
            });

            context.Communities.Add(community);
            context.RegulationDocuments.Add(regulation);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var request = new UpdateRegulationRequest
            {
                CommunityId = community.Id,
                Title = "Новый регламент",
                Content = "Первый новый пункт.\n\nВторой новый пункт.",
                Category = "Новая категория"
            };

            var result = await controller.Update(regulation.Id, request);

            result.Should().BeOfType<NoContentResult>();

            var updatedDocument = await context.RegulationDocuments
                .Include(x => x.Chunks)
                .SingleAsync(x => x.Id == regulation.Id);

            updatedDocument.Title.Should().Be("Новый регламент");
            updatedDocument.Content.Should().Be("Первый новый пункт.\n\nВторой новый пункт.");
            updatedDocument.Category.Should().Be("Новая категория");
            updatedDocument.CommunityId.Should().Be(community.Id);
            updatedDocument.Chunks.Should().HaveCount(2);
            updatedDocument.Chunks.Select(x => x.ChunkText).Should().Contain("Первый новый пункт.");
            updatedDocument.Chunks.Select(x => x.ChunkText).Should().Contain("Второй новый пункт.");
            updatedDocument.Chunks.Should().NotContain(x => x.ChunkText == "Старый чанк");

            embeddingService.RequestedTexts.Should().HaveCount(2);
            embeddingService.RequestedTexts.Should().Contain("Первый новый пункт.");
            embeddingService.RequestedTexts.Should().Contain("Второй новый пункт.");
        }

        [Fact]
        public async Task Update_ShouldReturnNotFound_WhenRegulationBelongsToAnotherUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var currentCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var otherCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                Name = "Чужое сообщество"
            };

            var regulation = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                CommunityId = otherCommunity.Id,
                Community = otherCommunity,
                Title = "Чужой регламент",
                Content = "Чужое содержание",
                Category = "Чужая категория"
            };

            context.Communities.AddRange(currentCommunity, otherCommunity);
            context.RegulationDocuments.Add(regulation);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var request = new UpdateRegulationRequest
            {
                CommunityId = currentCommunity.Id,
                Title = "Попытка изменить чужой регламент",
                Content = "Новое содержание",
                Category = "Новая категория"
            };

            var result = await controller.Update(regulation.Id, request);

            result.Should().BeOfType<NotFoundResult>();

            var unchangedDocument = await context.RegulationDocuments.SingleAsync(x => x.Id == regulation.Id);
            unchangedDocument.Title.Should().Be("Чужой регламент");
            unchangedDocument.Content.Should().Be("Чужое содержание");

            embeddingService.RequestedTexts.Should().BeEmpty();
        }

        [Fact]
        public async Task Update_ShouldReturnBadRequest_WhenTargetCommunityBelongsToAnotherUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var currentCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var otherCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                Name = "Чужое сообщество"
            };

            var regulation = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                CommunityId = currentCommunity.Id,
                Community = currentCommunity,
                Title = "Мой регламент",
                Content = "Содержание",
                Category = "Контент"
            };

            context.Communities.AddRange(currentCommunity, otherCommunity);
            context.RegulationDocuments.Add(regulation);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var request = new UpdateRegulationRequest
            {
                CommunityId = otherCommunity.Id,
                Title = "Попытка переноса",
                Content = "Новое содержание",
                Category = "Контент"
            };

            var result = await controller.Update(regulation.Id, request);

            var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Сообщество не найдено или недоступно текущему пользователю.");

            var unchangedDocument = await context.RegulationDocuments.SingleAsync(x => x.Id == regulation.Id);
            unchangedDocument.Title.Should().Be("Мой регламент");
            unchangedDocument.CommunityId.Should().Be(currentCommunity.Id);

            embeddingService.RequestedTexts.Should().BeEmpty();
        }

        [Fact]
        public async Task Delete_ShouldDeleteRegulation_WhenRegulationBelongsToCurrentUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();

            var community = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var regulation = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                CommunityId = community.Id,
                Community = community,
                Title = "Удаляемый регламент",
                Content = "Содержание",
                Category = "Контент"
            };

            context.Communities.Add(community);
            context.RegulationDocuments.Add(regulation);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var result = await controller.Delete(regulation.Id);

            result.Should().BeOfType<NoContentResult>();

            var exists = await context.RegulationDocuments.AnyAsync(x => x.Id == regulation.Id);
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task Delete_ShouldReturnNotFound_WhenRegulationBelongsToAnotherUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var otherCommunity = new Community
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId,
                Name = "Чужое сообщество"
            };

            var regulation = new RegulationDocument
            {
                Id = Guid.NewGuid(),
                CommunityId = otherCommunity.Id,
                Community = otherCommunity,
                Title = "Чужой регламент",
                Content = "Чужое содержание"
            };

            context.Communities.Add(otherCommunity);
            context.RegulationDocuments.Add(regulation);
            await context.SaveChangesAsync();

            var embeddingService = new FakeEmbeddingService();
            var controller = CreateController(context, embeddingService, currentUserId);

            var result = await controller.Delete(regulation.Id);

            result.Should().BeOfType<NotFoundResult>();

            var exists = await context.RegulationDocuments.AnyAsync(x => x.Id == regulation.Id);
            exists.Should().BeTrue();
        }
    }
}