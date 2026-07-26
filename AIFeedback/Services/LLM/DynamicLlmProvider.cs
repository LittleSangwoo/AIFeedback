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

        public async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0)
        {
            var providerConfig = _settingsService.GetActiveProvider();

            if (providerConfig == null)
                throw new InvalidOperationException("Активный LLM провайдер не настроен.");

            // Формируем универсальный payload в формате OpenAI
            var payload = new
            {
                model = providerConfig.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = temperature,
                stream = false,
                response_format = new { type = "json_object" } // Требуем JSON
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, providerConfig.BaseUrl)
            {
                Content = requestContent
            };

            // Разруливаем авторизацию
            if (providerConfig.AuthType.Equals("GigaChat", StringComparison.OrdinalIgnoreCase))
            {
                var token = await GetGigaChatTokenAsync(providerConfig.ApiKey);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else if (!string.IsNullOrEmpty(providerConfig.ApiKey))
            {
                // Стандартный подход для Groq, OpenAI и т.д. (Ollama просто проигнорирует Bearer)
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
            }

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            // Парсим стандартный ответ (choices[0].message.content)
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

        // Вспомогательный метод для получения токена GigaChat (он живет 30 минут)
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

            // Примечание: Для GigaChat нужно отключить проверку сертификата или установить Минцифры
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            _gigaChatToken = document.RootElement.GetProperty("access_token").GetString();
            _gigaChatTokenExpiresAt = DateTime.UtcNow.AddMinutes(25); // Запас 5 минут

            return _gigaChatToken;
        }
    }
}
