using SmmAnalyzerPrototype.Models;

namespace SmmAnalyzerPrototype.Data
{
    public static class SeedData
    {
        public static void Initialize(AppDbContext context)
        {
            if (context.Communities.Any()) return;

            var admin = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Login = "admin",
                Role = UserRole.Administrator
            };

            var manager = new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Login = "manager",
                Role = UserRole.ContentManager
            };

            context.Users.AddRange(admin, manager);

            var community = new Community
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "IT-новости",
                TargetAudience = "18–30 лет, студенты, IT-специалисты",
                StyleProfile = StyleProfile.Informal,
                ContentGoals = "Повышение вовлечённости"
            };

            context.Communities.Add(community);

            var reg = new RegulationDocument
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Title = "Регламент 2025",
                Content = "Запрещено: политика, дискриминация, медицинские рекомендации",
                Category = "Контент-безопасность",
                CommunityId = community.Id,
                CreatedBy = admin.Id
            };

            context.RegulationDocuments.Add(reg);

            context.SaveChanges();
        }
    }
}
