using AIFeedback.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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

        // РЕШЕНИЕ ОШИБКИ: Добавлено требуемое свойство ProviderName
        public string ProviderName => _settingsService.GetActiveProvider()?.Name ?? "DynamicProvider";

        // НОВЫЙ ОБЪЕДИНЕННЫЙ МЕТОД
        public async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0, string providerName = null)
        {
            // 1. Читаем и парсим файл конфигурации
            string json = await System.IO.File.ReadAllTextAsync("llm_providers.json");

            //// Парсим список конфигураций
            //var configs = JsonSerializer.Deserialize<List<LlmConfiguration>>(json, new JsonSerializerOptions
            //{
            //    PropertyNameCaseInsensitive = true
            //});

            //// Вытаскиваем все списки провайдеров в один плоский список
            //var allProviders = configs?
            //    .Where(c => c.Providers != null)
            //    .SelectMany(c => c.Providers)
            //    .ToList() ?? new List<LlmProviderConfig>(); // Убедись, что LlmProviderConfig существует в проекте

            // Парсим сразу в список ПРОВАЙДЕРОВ (а не конфигураций)
            var allProviders = JsonSerializer.Deserialize<List<LlmProviderConfig>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<LlmProviderConfig>();

            // 2. Ищем провайдера: по имени с главной страницы -> активного -> первого попавшегося
            var providerConfig = allProviders.FirstOrDefault(p => p.Name != null && p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))

                              ?? allProviders.FirstOrDefault();

            // 3. Бросаем ошибку только если JSON полностью пустой
            if (providerConfig == null)
            {
                throw new InvalidOperationException("В файле llm_providers.json нет ни одной записи провайдеров!");
            }

            // =======================================================
            // СТАРЫЙ КОД ОТПРАВКИ ЗАПРОСА (С ИСПОЛЬЗОВАНИЕМ НАЙДЕННОГО providerConfig)
            // =======================================================

            // Формируем универсальный payload в формате OpenAI
            var payload = new
            {
                model = providerConfig.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = temperature, // Подставляется температура из контракта
                stream = false
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, providerConfig.BaseUrl)
            {
                Content = requestContent
            };

            // string.Equals безопасно обработает null и просто вернет false
            if (string.Equals(providerConfig.AuthType, "GigaChat", StringComparison.OrdinalIgnoreCase))
            {
                var token = await GetGigaChatTokenAsync(providerConfig.ApiKey);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else if (!string.IsNullOrEmpty(providerConfig.ApiKey))
            {
                // Стандартный подход для Groq, OpenAI и т.д.
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
            }

            // Логируем URL перед отправкой
            Console.WriteLine($"Sending request to: {providerConfig.BaseUrl}");

            var response = await _httpClient.SendAsync(requestMessage);

            // ИСПРАВЛЕНИЕ: Вместо жесткого падения пробрасываем понятную ошибку в контроллер
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Error {response.StatusCode}. Проверьте правильность API ключа в llm_providers.json. Текст ответа: {errorText}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            // Парсим стандартный ответ (choices[0].message.content)
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

        // Вспомогательный метод для получения токена GigaChat
        private async Task<string> GetGigaChatTokenAsync(string authKey)
        {
            if (!string.IsNullOrEmpty(_gigaChatToken) && DateTime.UtcNow < _gigaChatTokenExpiresAt)
            {
                return _gigaChatToken;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authKey);
            request.Headers.Add("RqUID", Guid.NewGuid().ToString());
            request.Content = new StringContent("scope=GIGACHAT_API_PERS", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.SendAsync(request);

            // ИСПРАВЛЕНИЕ: Мягкий перехват ошибки для GigaChat
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"GigaChat Auth Error {response.StatusCode}. Проверьте авторизационные данные. Текст: {errorText}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            _gigaChatToken = document.RootElement.GetProperty("access_token").GetString();
            _gigaChatTokenExpiresAt = DateTime.UtcNow.AddMinutes(25); // Запас 5 минут

            return _gigaChatToken;
        }
    }
}