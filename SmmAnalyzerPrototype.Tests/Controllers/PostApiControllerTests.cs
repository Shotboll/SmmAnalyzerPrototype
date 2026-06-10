using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmmAnalyzerPrototype.Api.Controllers;
using SmmAnalyzerPrototype.Data.Data;
using SmmAnalyzerPrototype.Data.Enums;
using SmmAnalyzerPrototype.Data.Models;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace SmmAnalyzerPrototype.Tests.Controllers
{
    public class PostApiControllerTests
    {
        private class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
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
                    entity.Ignore(c => c.CommunityPosts);
                    entity.Ignore(c => c.RegulationDocuments);

                    entity.HasMany(c => c.Posts)
                        .WithOne(p => p.Community)
                        .HasForeignKey(p => p.CommunityId);
                });

                modelBuilder.Entity<Post>(entity =>
                {
                    entity.ToTable("posts");
                    entity.HasKey(p => p.Id);

                    entity.Property(p => p.Text)
                        .IsRequired();

                    entity.Property(p => p.CreatedAt)
                        .HasColumnName("created_at");

                    entity.Property(p => p.UpdatedAt)
                        .HasColumnName("updated_at");

                    entity.Property(p => p.Status)
                        .HasMaxLength(50);

                    entity.HasOne(p => p.Community)
                        .WithMany(c => c.Posts)
                        .HasForeignKey(p => p.CommunityId);

                    entity.HasOne(p => p.AnalysisResult)
                        .WithOne(a => a.Post)
                        .HasForeignKey<AnalysisResult>(a => a.PostId);
                });

                modelBuilder.Entity<AnalysisResult>(entity =>
                {
                    entity.ToTable("analysis_results");
                    entity.HasKey(a => a.PostId);

                    entity.Property(a => a.PostId)
                        .HasColumnName("post_id");

                    entity.Property(a => a.GrammarCheckedAt)
                        .HasColumnName("grammar_checked_at");

                    entity.Property(a => a.StyleCheckedAt)
                        .HasColumnName("style_checked_at");

                    entity.Property(a => a.RegulationCheckedAt)
                        .HasColumnName("regulation_checked_at");

                    entity.Property(a => a.ForecastCheckedAt)
                        .HasColumnName("forecast_checked_at");

                    entity.Property(a => a.RecommendationsCheckedAt)
                        .HasColumnName("recommendations_checked_at");

                    entity.Property(a => a.StyleAssessment)
                        .HasColumnName("style_assessment");

                    entity.Property(a => a.StyleSummary)
                        .HasColumnName("style_summary");

                    entity.Property(a => a.StyleStrengthsJson)
                        .HasColumnName("style_strengths_json");

                    entity.Property(a => a.StyleIssuesJson)
                        .HasColumnName("style_issues_json");

                    entity.Property(a => a.StyleRecommendationsJson)
                        .HasColumnName("style_recommendations_json");

                    entity.Property(a => a.HasRegulationViolations)
                        .HasColumnName("has_regulation_violations");

                    entity.Property(a => a.RegulationComment)
                        .HasColumnName("regulation_comment");

                    entity.Property(a => a.EngagementForecastJson)
                        .HasColumnName("engagement_forecast");

                    entity.Property(a => a.RecommendationsJson)
                        .HasColumnName("recommendations_json");

                    entity.Property(a => a.UpdatedAt)
                        .HasColumnName("updated_at");

                    entity.HasMany(a => a.GrammarErrors)
                        .WithOne(g => g.AnalysisResult)
                        .HasForeignKey(g => g.AnalysisResultId);

                    entity.HasMany(a => a.ProhibitedTopicMatches)
                        .WithOne(p => p.AnalysisResult)
                        .HasForeignKey(p => p.AnalysisResultId);
                });

                modelBuilder.Entity<GrammarError>(entity =>
                {
                    entity.ToTable("grammar_errors");
                    entity.HasKey(g => g.Id);

                    entity.Property(g => g.ErrorType)
                        .HasColumnName("error_type");

                    entity.Property(g => g.IsSuspicious)
                        .HasColumnName("is_suspicious");
                });

                modelBuilder.Entity<ProhibitedTopicMatch>(entity =>
                {
                    entity.ToTable("prohibited_topic_matches");
                    entity.HasKey(p => p.Id);

                    entity.Property(p => p.RegulationRef)
                        .HasColumnName("regulation_ref");
                });
            }
        }

        private static AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"SmmAnalyzerPostTestDb_{Guid.NewGuid()}")
                .Options;

            return new TestAppDbContext(options);
        }

        private static PostApiController CreateController(AppDbContext context, Guid? userId = null)
        {
            var controller = new PostApiController(
                context,
                llmService: null!,
                languageToolService: null!,
                grammarFilterService: null!,
                vkService: null!,
                logger: NullLogger<PostApiController>.Instance);

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
        public async Task GetAll_ShouldReturnOnlyPostsFromCurrentUserCommunities()
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

            var myPost = new Post
            {
                Id = Guid.NewGuid(),
                CommunityId = currentCommunity.Id,
                Community = currentCommunity,
                Text = "Мой пост",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                Status = PostStatus.Draft,
            };

            var otherPost = new Post
            {
                Id = Guid.NewGuid(),
                CommunityId = otherCommunity.Id,
                Community = otherCommunity,
                Text = "Чужой пост",
                CreatedAt = DateTime.UtcNow,
                Status = PostStatus.Draft,
            };

            context.Communities.AddRange(currentCommunity, otherCommunity);
            context.Posts.AddRange(myPost, otherPost);
            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var actionResult = await controller.GetAll();

            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var posts = okResult.Value.Should().BeAssignableTo<List<PostListItemDto>>().Subject;

            posts.Should().HaveCount(1);
            posts[0].Id.Should().Be(myPost.Id);
            posts[0].TextPreview.Should().Be("Мой пост");
            posts[0].CommunityName.Should().Be("Мое сообщество");
        }

        [Fact]
        public async Task GetById_ShouldReturnPost_WhenPostBelongsToCurrentUserCommunity()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();

            var community = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var post = new Post
            {
                Id = Guid.NewGuid(),
                CommunityId = community.Id,
                Community = community,
                Text = "Текст публикации",
                CreatedAt = DateTime.UtcNow,
                Status = PostStatus.Draft,
            };

            context.Communities.Add(community);
            context.Posts.Add(post);
            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var actionResult = await controller.GetById(post.Id);

            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.Should().BeOfType<PostDetailsDto>().Subject;

            dto.Id.Should().Be(post.Id);
            dto.Text.Should().Be("Текст публикации");
            dto.CommunityId.Should().Be(community.Id);
            dto.CommunityName.Should().Be("Мое сообщество");
        }

        [Fact]
        public async Task GetById_ShouldReturnNotFound_WhenPostBelongsToAnotherUserCommunity()
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

            var post = new Post
            {
                Id = Guid.NewGuid(),
                CommunityId = otherCommunity.Id,
                Community = otherCommunity,
                Text = "Чужой пост",
                CreatedAt = DateTime.UtcNow,
                Status = PostStatus.Draft,
            };

            context.Communities.Add(otherCommunity);
            context.Posts.Add(post);
            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var actionResult = await controller.GetById(post.Id);

            actionResult.Result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Create_ShouldCreatePost_WhenCommunityBelongsToCurrentUser()
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

            var controller = CreateController(context, currentUserId);

            var request = new CreatePostRequest
            {
                CommunityId = community.Id,
                Text = "  Новый текст публикации  "
            };

            var actionResult = await controller.Create(request);

            var createdResult = actionResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var dto = createdResult.Value.Should().BeOfType<PostDetailsDto>().Subject;

            dto.Id.Should().NotBe(Guid.Empty);
            dto.Text.Should().Be("Новый текст публикации");
            dto.CommunityId.Should().Be(community.Id);
            dto.CommunityName.Should().Be("Мое сообщество");
            dto.Status.Should().Be(PostStatus.Draft);

            var savedPost = await context.Posts.SingleAsync();
            savedPost.Text.Should().Be("Новый текст публикации");
            savedPost.CommunityId.Should().Be(community.Id);
            savedPost.Status.Should().Be(PostStatus.Draft);

            var analysisResult = await context.AnalysisResults.SingleAsync();
            analysisResult.PostId.Should().Be(savedPost.Id);
        }

        [Fact]
        public async Task Create_ShouldReturnUnauthorized_WhenUserHeaderIsMissing()
        {
            await using var context = CreateDbContext();
            var controller = CreateController(context);

            var request = new CreatePostRequest
            {
                CommunityId = Guid.NewGuid(),
                Text = "Текст"
            };

            var actionResult = await controller.Create(request);

            var unauthorized = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorized.Value.Should().Be("Пользователь не определен.");

            var postsCount = await context.Posts.CountAsync();
            postsCount.Should().Be(0);
        }

        [Fact]
        public async Task Create_ShouldReturnBadRequest_WhenTextIsEmpty()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();
            var controller = CreateController(context, currentUserId);

            var request = new CreatePostRequest
            {
                CommunityId = Guid.NewGuid(),
                Text = "   "
            };

            var actionResult = await controller.Create(request);

            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Некорректные данные публикации.");

            var postsCount = await context.Posts.CountAsync();
            postsCount.Should().Be(0);
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

            var controller = CreateController(context, currentUserId);

            var request = new CreatePostRequest
            {
                CommunityId = otherCommunity.Id,
                Text = "Текст публикации"
            };

            var actionResult = await controller.Create(request);

            var badRequest = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequest.Value.Should().Be("Сообщество не найдено или недоступно текущему пользователю.");

            var postsCount = await context.Posts.CountAsync();
            postsCount.Should().Be(0);
        }

        [Fact]
        public async Task Update_ShouldUpdatePostAndResetAnalysis_WhenPostBelongsToCurrentUser()
        {
            await using var context = CreateDbContext();

            var currentUserId = Guid.NewGuid();

            var community = new Community
            {
                Id = Guid.NewGuid(),
                UserId = currentUserId,
                Name = "Мое сообщество"
            };

            var post = new Post
            {
                Id = Guid.NewGuid(),
                CommunityId = community.Id,
                Community = community,
                Text = "Старый текст",
                CreatedAt = DateTime.UtcNow,
                Status = PostStatus.Analyzed
            };

            var analysis = new AnalysisResult
            {
                PostId = post.Id,
                Post = post,
                GrammarCheckedAt = DateTime.UtcNow,
                StyleCheckedAt = DateTime.UtcNow,
                RegulationCheckedAt = DateTime.UtcNow,
                ForecastCheckedAt = DateTime.UtcNow,
                RecommendationsCheckedAt = DateTime.UtcNow,
                StyleAssessment = "Подходит",
                StyleSummary = "Старый результат",
                StyleStrengthsJson = "[\"сильная сторона\"]",
                StyleIssuesJson = "[\"проблема\"]",
                StyleRecommendationsJson = "[\"совет\"]",
                HasRegulationViolations = true,
                RegulationComment = "Есть нарушения",
                EngagementForecastJson = "{}",
                RecommendationsJson = "{}",
                UpdatedAt = DateTime.UtcNow
            };

            context.Communities.Add(community);
            context.Posts.Add(post);
            context.AnalysisResults.Add(analysis);
            context.GrammarErrors.Add(new GrammarError
            {
                Id = Guid.NewGuid(),
                AnalysisResultId = post.Id,
                Fragment = "ошибка",
                Position = 0,
                IsSuspicious = false
            });
            context.ProhibitedTopicMatches.Add(new ProhibitedTopicMatch
            {
                Id = Guid.NewGuid(),
                AnalysisResultId = post.Id,
                Topic = "Нарушение"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var request = new UpdatePostRequest
            {
                CommunityId = community.Id,
                Text = "Новый текст"
            };

            var result = await controller.Update(post.Id, request);

            result.Should().BeOfType<NoContentResult>();

            var updatedPost = await context.Posts
                .Include(p => p.AnalysisResult)
                .SingleAsync(x => x.Id == post.Id);

            updatedPost.Text.Should().Be("Новый текст");
            updatedPost.Status.Should().Be(PostStatus.Draft);
            updatedPost.UpdatedAt.Should().NotBeNull();

            updatedPost.AnalysisResult.Should().NotBeNull();
            updatedPost.AnalysisResult!.GrammarCheckedAt.Should().BeNull();
            updatedPost.AnalysisResult.StyleCheckedAt.Should().BeNull();
            updatedPost.AnalysisResult.RegulationCheckedAt.Should().BeNull();
            updatedPost.AnalysisResult.ForecastCheckedAt.Should().BeNull();
            updatedPost.AnalysisResult.RecommendationsCheckedAt.Should().BeNull();
            updatedPost.AnalysisResult.StyleAssessment.Should().BeNull();
            updatedPost.AnalysisResult.StyleSummary.Should().BeNull();
            updatedPost.AnalysisResult.HasRegulationViolations.Should().BeNull();
            updatedPost.AnalysisResult.RegulationComment.Should().BeNull();
            updatedPost.AnalysisResult.EngagementForecastJson.Should().BeNull();
            updatedPost.AnalysisResult.RecommendationsJson.Should().BeNull();

            var grammarErrorsCount = await context.GrammarErrors.CountAsync();
            var regulationMatchesCount = await context.ProhibitedTopicMatches.CountAsync();

            grammarErrorsCount.Should().Be(0);
            regulationMatchesCount.Should().Be(0);
        }

        [Fact]
        public async Task Update_ShouldReturnNotFound_WhenPostBelongsToAnotherUser()
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

            var post = new Post
            {
                Id = Guid.NewGuid(),
                CommunityId = otherCommunity.Id,
                Community = otherCommunity,
                Text = "Чужой текст",
                CreatedAt = DateTime.UtcNow,
                Status = PostStatus.Draft
            };

            context.Communities.AddRange(currentCommunity, otherCommunity);
            context.Posts.Add(post);
            await context.SaveChangesAsync();

            var controller = CreateController(context, currentUserId);

            var request = new UpdatePostRequest
            {
                CommunityId = currentCommunity.Id,
                Text = "Попытка изменить чужой текст"
            };

            var result = await controller.Update(post.Id, request);

            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.Value.Should().Be("Пост не найден.");

            var unchangedPost = await context.Posts.SingleAsync(x => x.Id == post.Id);
            unchangedPost.Text.Should().Be("Чужой текст");
            unchangedPost.CommunityId.Should().Be(otherCommunity.Id);
        }
    }
}