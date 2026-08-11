using AIFeedback.Models;
using AIFeedback.Services.LLM.Providers;
using AIFeedback.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AIFeedback.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILLMProviderFactory _providerFactory;

        public HomeController(ILLMProviderFactory providerFactory)
        {
            _providerFactory = providerFactory;
        }

        //public IActionResult Index()
        //{
        //    // Получаем список доступных провайдеров (можно из фабрики, если она предоставляет список имён)
        //    // Просто передаём статический список для демонстрации, или читаем из файла.
        //    var viewModel = new UploadViewModel
        //    {
        //        AvailableProviders = new List<string> { "groq", "gigachatApi", "yandexgpt", "ollama local1" }
        //    };
        //    return View(viewModel);
        //}

        public async Task<IActionResult> Index()
        {
            var providerNames = new List<string>();

            try
            {
                // Читаем провайдеров напрямую из файла
                if (System.IO.File.Exists("llm_providers.json"))
                {
                    string json = await System.IO.File.ReadAllTextAsync("llm_providers.json");
                    var configs = JsonSerializer.Deserialize<List<LlmConfiguration>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (configs != null)
                    {
                        // ВАЖНО: Укажи здесь то свойство, которое в твоем json отвечает за имя (Name, Provider, Model и т.д.)
                        providerNames = configs
    .Where(c => c.Providers != null)      // Отбрасываем пустые конфигурации
    .SelectMany(c => c.Providers)         // Объединяем все списки провайдеров в один плоский список
    .Select(p => p.Name ?? "Unknown")     // Достаем имя каждого провайдера
    .ToList();
                    }
                }
            }
            catch
            {
                // Если файл битый, не даем странице упасть
                providerNames.Add("groq");
            }

            // Передаем список на фронтенд
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
