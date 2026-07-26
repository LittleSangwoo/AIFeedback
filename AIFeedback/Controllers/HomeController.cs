using AIFeedback.Services.LLM.Providers;
using AIFeedback.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AIFeedback.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILLMProviderFactory _providerFactory;

        public HomeController(ILLMProviderFactory providerFactory)
        {
            _providerFactory = providerFactory;
        }

        public IActionResult Index()
        {
            // Получаем список доступных провайдеров (можно из фабрики, если она предоставляет список имён)
            // Просто передаём статический список для демонстрации, или читаем из файла.
            var viewModel = new UploadViewModel
            {
                AvailableProviders = new List<string> { "groq", "gigachatApi", "yandexgpt", "ollama local1" }
            };
            return View(viewModel);
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
