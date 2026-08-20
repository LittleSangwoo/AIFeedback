using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AIFeedback.Models;
using System;
using System.Collections.Generic;

namespace AIFeedback.Controllers
{
    // Убрали [ApiController] и [Route] с уровня класса, 
    // чтобы стандартная маршрутизация страниц (Home, Settings и т.д.) работала корректно.
    public class SettingsController : Controller // Наследуемся от полноценного Controller!
    {
        private readonly string _filePath = "llm_providers.json";

        // 1. Метод для отдачи HTML-страницы (Сработает по адресу /Settings)
        public IActionResult Index()
        {
            return View();
        }

        // --- 2. Ниже идут API-методы. Прописываем маршруты прямо для них ---

        [HttpGet("api/settings/providers")]
        public IActionResult GetProviders()
        {
            if (!System.IO.File.Exists(_filePath)) return Ok(new List<LlmProviderConfig>());
            var json = System.IO.File.ReadAllText(_filePath);
            var providers = JsonSerializer.Deserialize<List<LlmProviderConfig>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(providers ?? new List<LlmProviderConfig>());
        }

        [HttpPost("api/settings/providers")]
        public IActionResult SaveProvider([FromBody] LlmProviderConfig newProvider)
        {
            var providers = GetProvidersList();

            if (string.IsNullOrEmpty(newProvider.Id))
            {
                newProvider.Id = Guid.NewGuid().ToString();
                providers.Add(newProvider);
            }
            else
            {
                var index = providers.FindIndex(p => p.Id == newProvider.Id);
                if (index >= 0) providers[index] = newProvider;
            }

            System.IO.File.WriteAllText(_filePath, JsonSerializer.Serialize(providers, new JsonSerializerOptions { WriteIndented = true }));
            return Ok();
        }

        [HttpDelete("api/settings/providers/{id}")]
        public IActionResult DeleteProvider(string id)
        {
            var providers = GetProvidersList();
            providers.RemoveAll(p => p.Id == id);
            System.IO.File.WriteAllText(_filePath, JsonSerializer.Serialize(providers, new JsonSerializerOptions { WriteIndented = true }));
            return Ok();
        }

        private List<LlmProviderConfig> GetProvidersList()
        {
            if (!System.IO.File.Exists(_filePath)) return new List<LlmProviderConfig>();
            var json = System.IO.File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<LlmProviderConfig>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<LlmProviderConfig>();
        }
    }
}