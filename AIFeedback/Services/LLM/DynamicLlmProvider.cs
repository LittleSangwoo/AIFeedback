using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIFeedback.Models;
using Microsoft.Extensions.Logging;

namespace AIFeedback.Services.LLM
{
    public class DynamicLlmProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILlmSettingsService _settingsService;
        private readonly ILogger<DynamicLlmProvider> _logger;
        private string _gigaChatToken;
        private DateTime _gigaChatTokenExpiresAt;

        public DynamicLlmProvider(HttpClient httpClient, ILlmSettingsService settingsService, ILogger<DynamicLlmProvider> logger)
        {
            _httpClient = httpClient;
            _settingsService = settingsService;
            _logger = logger;
        }

        public string ProviderName => _settingsService.GetActiveProvider()?.Name ?? "DynamicProvider";

        public async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0, string providerName = null)
        {
            string fullPath = Path.GetFullPath("llm_providers.json");
            _logger.LogInformation("DynamicLlmProvider: читаю '{Path}', запрошен providerName='{ProviderName}'", fullPath, providerName ?? "(не указан)");

            string json = await System.IO.File.ReadAllTextAsync(fullPath);
            var allProviders = JsonSerializer.Deserialize<List<LlmProviderConfig>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new List<LlmProviderConfig>();

            _logger.LogInformation("DynamicLlmProvider: загружено {Count} провайдеров: {Names}",
                allProviders.Count, string.Join(", ", allProviders.Select(p => $"'{p.Name}'")));

            LlmProviderConfig providerConfig;

            if (!string.IsNullOrEmpty(providerName))
            {
                // Провайдер запрошен явно — ищем ТОЛЬКО его. Никакого тихого отката на другого провайдера:
                // если явно выбранный провайдер не найден, это ошибка конфигурации, и она должна быть видна.
                providerConfig = allProviders.FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

                if (providerConfig == null)
                {
                    _logger.LogWarning("DynamicLlmProvider: провайдер '{ProviderName}' НЕ найден среди {Count} загруженных.", providerName, allProviders.Count);
                    throw new InvalidOperationException(
                        $"Провайдер ИИ «{providerName}» не найден в конфигурации (llm_providers.json). " +
                        $"Проверьте раздел «Шлюзы ИИ» — возможно, провайдер был переименован или удалён.");
                }
            }
            else
            {
                // Провайдер не указан явно — используем активного по умолчанию
                var activeProviderName = _settingsService.GetActiveProvider()?.Name;
                providerConfig = allProviders.FirstOrDefault(p => p.Name == activeProviderName)
                                  ?? allProviders.FirstOrDefault();

                if (providerConfig == null)
                    throw new InvalidOperationException("Конфигурация нейросетей пуста.");
            }

            _logger.LogInformation("DynamicLlmProvider: выбран провайдер '{Name}', BaseUrl='{BaseUrl}'", providerConfig.Name, providerConfig.BaseUrl);

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
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);
            }

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("DynamicLlmProvider: ошибка API {Name} ({Status}): {Body}", providerConfig.Name, response.StatusCode, errorBody);
                throw new HttpRequestException($"Ошибка API {providerConfig.Name} ({response.StatusCode}): {errorBody}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }

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