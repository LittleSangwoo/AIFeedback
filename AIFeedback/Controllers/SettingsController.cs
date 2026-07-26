using Microsoft.AspNetCore.Mvc;
using AIFeedback.Services;
using AIFeedback.Models;

namespace AIFeedback.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ILlmSettingsService _settingsService;

        public SettingsController(ILlmSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        // GET: /Settings
        public IActionResult Index()
        {
            var config = _settingsService.GetConfiguration();
            return View(config);
        }

        // POST: /Settings/Save
        [HttpPost]
        public IActionResult Save(LlmConfiguration model)
        {
            if (ModelState.IsValid)
            {
                _settingsService.SaveConfiguration(model);
                TempData["SuccessMessage"] = "Настройки успешно сохранены.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Ошибка при сохранении настроек.";
            return View("Index", model);
        }

        // POST: /Settings/SetProvider
        [HttpPost]
        public IActionResult SetActiveProvider(string providerId)
        {
            _settingsService.SetActiveProvider(providerId);
            return Ok();
        }
    }
}
