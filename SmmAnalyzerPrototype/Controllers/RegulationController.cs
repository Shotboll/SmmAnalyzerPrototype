using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmmAnalyzerPrototype.Models;

namespace SmmAnalyzerPrototype.Controllers
{
    public class RegulationController : Controller
    {
        public IActionResult Index()
        {
            var regulations = new List<RegulationDocument>
            {
                new()
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Title = "Регламент 2025: IT-сообщество",
                    Category = "Контент-безопасность",
                    Content = "Запрещено: упоминание политики, дискриминационные высказывания, медицинские рекомендации без лицензии, прямая реклама нелицензированных курсов.",
                    CommunityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedBy = Guid.Parse("00000000-0000-0000-0000-000000000001")
                },
                new()
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Title = "Этический кодекс для образовательного контента",
                    Category = "Этика",
                    Content = "Запрещено: высмеивание учеников, навязывание единственно верного мнения, использование страха.",
                    CommunityId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    CreatedBy = Guid.Parse("00000000-0000-0000-0000-000000000001")
                }
            };
            return View(regulations);
        }

        public IActionResult Create()
        {
            // Передаём список сообществ в выпадающий список
            var communities = new List<Community>
            {
                new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "IT-новости" },
                new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Образование" }
            };
            ViewBag.Communities = communities.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View();
        }

        [HttpPost]
        public IActionResult Create(RegulationCreateModel model)
        {
            if (!ModelState.IsValid)
            {
                // Восстанавливаем список при ошибке
                var communities = new List<Community>
                {
                    new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "IT-новости" },
                    new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Образование" }
                };
                ViewBag.Communities = communities.Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList();
                return View(model);
            }

            TempData["Message"] = "Регламент «" + model.Title + "» добавлен.";
            return RedirectToAction("Index");
        }
    }
}
