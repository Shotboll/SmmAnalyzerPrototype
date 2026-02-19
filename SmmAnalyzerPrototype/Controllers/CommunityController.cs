using Microsoft.AspNetCore.Mvc;
using SmmAnalyzerPrototype.Models;

namespace SmmAnalyzerPrototype.Controllers
{
    public class CommunityController : Controller
    {
        public IActionResult Index()
        {
            var communities = new List<Community>
            {
                new()
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "IT-новости",
                    TargetAudience = "18–30 лет, студенты, junior-разработчики",
                    StyleProfile = StyleProfile.Informal,
                    ContentGoals = "Повышение вовлечённости и привлечение новой аудитории"
                },
                new()
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Образование в цифровую эпоху",
                    TargetAudience = "Преподаватели, школьники 10–11 кл., родители",
                    StyleProfile = StyleProfile.Neutral,
                    ContentGoals = "Повышение осведомлённости о цифровых компетенциях"
                }
            };
            return View(communities);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(CommunityCreateModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            TempData["Message"] = "Сообщество «" + model.Name + "» добавлено.";
            return RedirectToAction("Index");
        }
    }
}
