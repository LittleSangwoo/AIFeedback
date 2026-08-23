using AIFeedback.Models;
using System.Text.Json;
using System.Linq;

namespace AIFeedback.Services
{
    public interface ILlmSettingsService
    {
        LlmConfiguration GetConfiguration();
        void SaveConfiguration(LlmConfiguration config);
        LlmProviderConfig GetActiveProvider();
        void SetActiveProvider(string providerId);
        List<LlmProviderConfig> GetAllProviders();
    }

    public class LlmSettingsService : ILlmSettingsService
    {
        private readonly string _providersFilePath = "llm_providers.json";

        // ОТДЕЛЬНЫЙ файл для хранения id активного провайдера.
        // llm_providers.json остаётся ПЛОСКИМ МАССИВОМ LlmProviderConfig —
        // именно такой формат ожидает SettingsController, поэтому его структуру менять нельзя.
        private readonly string _activeProviderFilePath = "llm_active_provider.json";

        private class ActiveProviderState
        {
            public string ActiveProviderId { get; set; } = string.Empty;
        }

        public List<LlmProviderConfig> GetAllProviders()
        {
            if (!File.Exists(_providersFilePath))
                return new List<LlmProviderConfig>();

            string json = File.ReadAllText(_providersFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<LlmProviderConfig>();

            return JsonSerializer.Deserialize<List<LlmProviderConfig>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<LlmProviderConfig>();
        }

        private string GetActiveProviderId()
        {
            if (!File.Exists(_activeProviderFilePath)) return string.Empty;

            string json = File.ReadAllText(_activeProviderFilePath);
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;

            try
            {
                var state = JsonSerializer.Deserialize<ActiveProviderState>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return state?.ActiveProviderId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void SaveActiveProviderId(string providerId)
        {
            var state = new ActiveProviderState { ActiveProviderId = providerId ?? string.Empty };
            File.WriteAllText(_activeProviderFilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }

        public LlmConfiguration GetConfiguration()
        {
            return new LlmConfiguration
            {
                Providers = GetAllProviders(),
                ActiveProviderId = GetActiveProviderId()
            };
        }

        public void SaveConfiguration(LlmConfiguration config)
        {
            if (config == null) return;

            // Список провайдеров сохраняем в ТОМ ЖЕ формате, что и SettingsController —
            // плоский массив, чтобы не расходиться со страницей "Шлюзы ИИ".
            if (config.Providers != null)
            {
                File.WriteAllText(_providersFilePath, JsonSerializer.Serialize(config.Providers, new JsonSerializerOptions { WriteIndented = true }));
            }

            SaveActiveProviderId(config.ActiveProviderId);
        }

        public LlmProviderConfig GetActiveProvider()
        {
            var providers = GetAllProviders();
            if (providers.Count == 0)
                return null;

            var activeId = GetActiveProviderId();

            return providers.FirstOrDefault(p => !string.IsNullOrEmpty(activeId) && p.Id == activeId)
                   ?? providers.FirstOrDefault();
        }

        public void SetActiveProvider(string providerId)
        {
            var providers = GetAllProviders();

            if (providers.Any(p => p.Id == providerId))
            {
                SaveActiveProviderId(providerId);
            }
        }
    }
}