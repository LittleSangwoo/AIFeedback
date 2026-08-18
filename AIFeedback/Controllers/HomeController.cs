using AIFeedback.Models;
using AIFeedback.Services;
using AIFeedback.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AIFeedback.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILlmSettingsService _settingsService;

        public HomeController(ILlmSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public IActionResult Index()
        {
            var providerNames = _settingsService.GetAllProviders()
                .Select(p => p.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (providerNames.Count == 0)
            {
                providerNames = new List<string> { "groq", "gigachatApi", "yandexgpt" };
            }

            ViewBag.Providers = providerNames;

            return View();
        }

        [HttpPost]
        public IActionResult Upload(UploadViewModel model)
        {
            if (model.ExcelFile == null || model.ExcelFile.Length == 0)
            {
                ModelState.AddModelError("ExcelFile", "Пожалуйста, выберите файл.");
                return View("Index", model);
            }

            // Перенаправляем на обработку
            return RedirectToAction("Process", "Feedback", new
            {
                provider = model.SelectedProvider,
                fileName = model.ExcelFile.FileName
            });
        }
    }
}
