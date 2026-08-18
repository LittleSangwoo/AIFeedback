using AIFeedback.Models;
using System.Text.Json;
using System.Linq;

namespace AIFeedback.Services
{
    // Тот самый интерфейс, на который ругается студия
    public interface ILlmSettingsService
    {
        LlmConfiguration GetConfiguration();
        void SaveConfiguration(LlmConfiguration config);
        LlmProviderConfig GetActiveProvider();
        void SetActiveProvider(string providerId);
        List<LlmProviderConfig> GetAllProviders();
    }

    // Реализация сервиса для работы с JSON-конфигом
    public class LlmSettingsService : ILlmSettingsService
    {
        private readonly string _configFilePath = "llm_providers.json";

        public LlmConfiguration GetConfiguration()
        {
            if (!File.Exists(_configFilePath))
            {
                // Если файла еще нет, возвращаем пустую структуру
                return new LlmConfiguration();
            }

            string json =  File.ReadAllText("llm_providers.json");

            if (string.IsNullOrWhiteSpace(json))
            {
                return new LlmConfiguration();
            }

            // 1. Сначала парсим JSON в список (потому что в файле квадратные скобки [ ] )
            var configs = JsonSerializer.Deserialize<List<LlmConfiguration>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // 2. Возвращаем ПЕРВЫЙ элемент из списка. Если список пустой или null — возвращаем пустую конфигурацию.
            return configs?.FirstOrDefault() ?? new LlmConfiguration();
        }

        public void SaveConfiguration(LlmConfiguration config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true // Для красивого форматирования в файле
            });
            File.WriteAllText(_configFilePath, json);
        }

        public List<LlmProviderConfig> GetAllProviders()
        {
            if (!File.Exists(_configFilePath))
                return new List<LlmProviderConfig>();

            string json = File.ReadAllText(_configFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<LlmProviderConfig>();

            return JsonSerializer.Deserialize<List<LlmProviderConfig>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<LlmProviderConfig>();
        }

        public LlmProviderConfig GetActiveProvider()
        {
            var providers = GetAllProviders();
            if (providers.Count == 0)
                return null;

            var config = GetConfiguration();
            return providers.FirstOrDefault(p => p.Id == config.ActiveProviderId)
                   ?? providers.FirstOrDefault();
        }

        public void SetActiveProvider(string providerId)
        {
            var config = GetConfiguration();

            // Проверяем, существует ли такой провайдер, прежде чем делать его активным
            if (config.Providers.Any(p => p.Id == providerId))
            {
                config.ActiveProviderId = providerId;
                SaveConfiguration(config);
            }
        }
    }
}
