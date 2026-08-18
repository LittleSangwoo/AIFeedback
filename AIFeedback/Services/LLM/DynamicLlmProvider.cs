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
            var allProviders = _settingsService.GetAllProviders();

            var providerConfig = ResolveProvider(allProviders, providerName);

            if (providerConfig == null)
            {
                throw new InvalidOperationException("В файле llm_providers.json нет ни одной записи провайдеров!");
            }

            var payload = new
            {
                model = providerConfig.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = temperature,
                stream = false
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, providerConfig.BaseUrl)
            {
                Content = requestContent
            };

            if (IsGigaChatProvider(providerConfig))
            {
                var token = await GetGigaChatTokenAsync(providerConfig.ApiKey, providerConfig.Scope);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else if (!string.IsNullOrEmpty(providerConfig.ApiKey) && providerConfig.ApiKey != "-")
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
            }

            if (IsYandexProvider(providerConfig) && !string.IsNullOrEmpty(providerConfig.Scope))
            {
                requestMessage.Headers.Add("x-folder-id", providerConfig.Scope);

                
            }

            Console.WriteLine($"Sending request to: {providerConfig.BaseUrl} (provider: {providerConfig.Name})");

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Error {response.StatusCode} ({providerConfig.Name}). Проверьте правильность API ключа в llm_providers.json. Текст ответа: {errorText}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

        private static LlmProviderConfig ResolveProvider(List<LlmProviderConfig> allProviders, string providerName)
        {
            if (allProviders.Count == 0)
                return null;

            if (string.IsNullOrWhiteSpace(providerName))
                return allProviders.FirstOrDefault();

            var normalized = providerName.Trim();

            var exactMatch = allProviders.FirstOrDefault(p =>
                p.Name != null && p.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            if (normalized.Equals("gigachat", StringComparison.OrdinalIgnoreCase))
            {
                return allProviders.FirstOrDefault(p =>
                    p.Name != null && p.Name.Contains("gigachat", StringComparison.OrdinalIgnoreCase));
            }

            throw new InvalidOperationException(
                $"Провайдер '{providerName}' не найден в llm_providers.json. Доступные: {string.Join(", ", allProviders.Select(p => p.Name))}");
        }

        private static bool IsGigaChatProvider(LlmProviderConfig config) =>
            string.Equals(config.AuthType, "GigaChat", StringComparison.OrdinalIgnoreCase)
            || (config.Name?.Contains("gigachat", StringComparison.OrdinalIgnoreCase) ?? false);

        private static bool IsYandexProvider(LlmProviderConfig config) =>
            config.Name?.Contains("yandex", StringComparison.OrdinalIgnoreCase) == true
            || config.BaseUrl?.Contains("yandex", StringComparison.OrdinalIgnoreCase) == true;

        private async Task<string> GetGigaChatTokenAsync(string clientId, string secret)
        {
            if (!string.IsNullOrEmpty(_gigaChatToken) && DateTime.UtcNow < _gigaChatTokenExpiresAt)
            {
                return _gigaChatToken;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Headers.Add("RqUID", Guid.NewGuid().ToString());
            request.Content = new StringContent("scope=GIGACHAT_API_PERS", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"GigaChat Auth Error {response.StatusCode}. Проверьте Client ID и Secret в llm_providers.json. Текст: {errorText}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            _gigaChatToken = document.RootElement.GetProperty("access_token").GetString();
            _gigaChatTokenExpiresAt = DateTime.UtcNow.AddMinutes(25);

            return _gigaChatToken;
        }
    }
}