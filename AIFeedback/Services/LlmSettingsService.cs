using AIFeedback.Models;
using System.Text.Json;

namespace AIFeedback.Services
{
    // Тот самый интерфейс, на который ругается студия
    public interface ILlmSettingsService
    {
        LlmConfiguration GetConfiguration();
        void SaveConfiguration(LlmConfiguration config);
        LlmProviderConfig GetActiveProvider();
        void SetActiveProvider(string providerId);
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

            var json = File.ReadAllText(_configFilePath);
            return JsonSerializer.Deserialize<LlmConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LlmConfiguration();
        }

        public void SaveConfiguration(LlmConfiguration config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true // Для красивого форматирования в файле
            });
            File.WriteAllText(_configFilePath, json);
        }

        public LlmProviderConfig GetActiveProvider()
        {
            var config = GetConfiguration();

            // Ищем провайдера по Id, который указан как активный
            return config.Providers.FirstOrDefault(p => p.Id == config.ActiveProviderId);
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
