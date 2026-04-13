using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmmAnalyzerPrototype.Data.Models;

namespace SmmAnalyzerPrototype.Data.Data
{
    public static class DbInitializer
    {
        public static async Task SeedTestUserAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.Database.MigrateAsync();

            var existingUser = await context.Users
                .FirstOrDefaultAsync(x => x.Login == "test_user");

            if (existingUser != null)
                return;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Login = "test_user",
                Password = "test_password",
                Email = "test_user@example.com"
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }
    }
}