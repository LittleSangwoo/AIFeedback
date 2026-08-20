using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIFeedback.Models;

namespace AIFeedback.Services.LLM
{
    public class DynamicLlmProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILlmSettingsService _settingsService;
        private string _gigaChatToken;
        private DateTime _gigaChatTokenExpiresAt;

        public DynamicLlmProvider(HttpClient httpClient, ILlmSettingsService settingsService)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
        }
        // ДОБАВЬ ВОТ ЭТУ СТРОКУ:
        public string ProviderName => _settingsService.GetActiveProvider()?.Name ?? "DynamicProvider";

        public async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0, string providerName = null)
        {
            // читаем текущие настройки (можно кэшировать для скорости)
            string json = await System.IO.File.ReadAllTextAsync("llm_providers.json");


            var allProviders = JsonSerializer.Deserialize<List<LlmProviderConfig>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new List<LlmProviderConfig>();

            // Ищем провайдера, которого выбрал пользователь, или берем дефолтного
            var providerConfig = allProviders.FirstOrDefault(p => !string.IsNullOrEmpty(providerName) && p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))
                              
                              ?? allProviders.FirstOrDefault();

            if (providerConfig == null) throw new InvalidOperationException("Конфигурация нейросетей пуста.");

            //  Формируем универсальный запрос в формате OpenAI (подходит для Ollama, Groq, кастомных API)
            var payload = new
            {
                model = providerConfig.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = temperature,
                max_tokens = 2500,
                stream = false
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, providerConfig.BaseUrl) { Content = requestContent };

            //  Гибкая маршрутизация авторизации
            bool isYandex = providerConfig.Name.Contains("yandex", StringComparison.OrdinalIgnoreCase) ||
                            providerConfig.BaseUrl.Contains("yandex");
            bool isGigaChat = providerConfig.Name.Contains("giga", StringComparison.OrdinalIgnoreCase);

            if (isGigaChat)
            {
                var token = await GetGigaChatTokenAsync(providerConfig.ApiKey);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else if (isYandex)
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", providerConfig.ApiKey);
                if (!string.IsNullOrEmpty(providerConfig.Scope))
                {
                    requestMessage.Headers.Add("x-folder-id", providerConfig.Scope);
                }
            }
            else if (!string.IsNullOrEmpty(providerConfig.ApiKey))
            {
                // Стандартный подход для всех остальных (Ollama работает без ключа, поэтому условие не сработает)
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
            }

            // Отправка и обработка
            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Ошибка API {providerConfig.Name} ({response.StatusCode}): {errorBody}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            // Универсальный парсинг ответа
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

        // Вспомогательный метод для GigaChat
        private async Task<string> GetGigaChatTokenAsync(string authKey)
        {
            if (!string.IsNullOrEmpty(_gigaChatToken) && DateTime.UtcNow < _gigaChatTokenExpiresAt) return _gigaChatToken;

            var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authKey);
            request.Headers.Add("RqUID", Guid.NewGuid().ToString());
            request.Content = new StringContent("scope=GIGACHAT_API_PERS", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            _gigaChatToken = document.RootElement.GetProperty("access_token").GetString();
            _gigaChatTokenExpiresAt = DateTime.UtcNow.AddMinutes(25);
            return _gigaChatToken;
        }
    }
}