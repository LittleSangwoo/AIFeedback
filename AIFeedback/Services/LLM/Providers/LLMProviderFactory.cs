using System.Text.Json;

namespace AIFeedback.Services.LLM.Providers
{
    public interface ILLMProviderFactory
    {
        ILLMProvider GetProvider(string providerName = null);
    }

    public class LLMProviderFactory : ILLMProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LLMProviderFactory> _logger;
        private readonly Dictionary<string, Func<ILLMProvider>> _providerCreators;

        public LLMProviderFactory(IServiceProvider serviceProvider,
                                  IConfiguration configuration,
                                  ILogger<LLMProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _providerCreators = new Dictionary<string, Func<ILLMProvider>>();
            InitializeCreators();
        }

        private void InitializeCreators()
        {
            // 1. Загружаем все провайдеры из llm_providers.json
            var providersFile = "llm_providers.json";
            if (!File.Exists(providersFile))
            {
                _logger.LogWarning("Файл llm_providers.json не найден, будут использованы только встроенные провайдеры.");
                return;
            }

            var json = File.ReadAllText(providersFile);
            var providers = JsonSerializer.Deserialize<List<ProviderConfig>>(json);
            if (providers == null) return;

            foreach (var config in providers)
            {
                // Ключом будет Name (или Id) – используем Name для удобства
                var key = config.Name?.ToLowerInvariant();
                if (string.IsNullOrEmpty(key)) continue;

                if (_providerCreators.ContainsKey(key))
                {
                    _logger.LogWarning("Провайдер с именем {Name} уже зарегистрирован, пропускаем.", config.Name);
                    continue;
                }

                // Определяем, какой класс провайдера использовать
                _providerCreators[key] = config.Name?.ToLowerInvariant() switch
                {
                    "gigachatapi" => () => CreateGigaChatProvider(config),
                    "yandexgpt" => () => CreateYandexGPTProvider(config),
                    var name when config.IsLocal => () => CreateOllamaProvider(config),
                    _ => () => CreateOpenAiProvider(config)
                };
            }

            // 2. Добавляем возможность выбрать провайдера по умолчанию из appsettings
            // (опционально, если не задано, берём первый из списка)
        }

        private ILLMProvider CreateGigaChatProvider(ProviderConfig config)
        {
            // Для GigaChat используем отдельный класс с OAuth
            // Нужно получить опции из IOptions или создать их вручную из конфига
            var options = new GigaChatOptions
            {
                AuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth", // можно вынести в конфиг
                ApiUrl = config.ApiUrl,
                ClientId = config.ApiKey, // предположим, что ApiKey хранит ClientId
                Secret = config.Scope ?? "" // Scope может содержать секрет
            };
            // Создаём экземпляр через DI с явным указанием опций
            var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = _serviceProvider.GetRequiredService<ILogger<GigaChatProvider>>();
            // Для передачи опций используем IOptions, но можно и напрямую
            // В реальном коде лучше использовать IOptions<T>, но для простоты создадим свой OptionsWrapper
            var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
            return new GigaChatProvider(httpClient, logger, optionsWrapper);
        }

        private ILLMProvider CreateYandexGPTProvider(ProviderConfig config)
        {
            // Аналогично для YandexGPT
            var options = new YandexGPTOptions
            {
                ApiUrl = config.ApiUrl,
                IamToken = config.ApiKey, // или IAM-токен
                FolderId = config.Scope ?? ""
            };
            var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = _serviceProvider.GetRequiredService<ILogger<YandexGPTProvider>>();
            var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
            return new YandexGPTProvider(httpClient, logger, optionsWrapper);
        }

        private ILLMProvider CreateOllamaProvider(ProviderConfig config)
        {
            var options = new OllamaOptions
            {
                ApiUrl = config.ApiUrl,
                Model = config.ModelName
            };
            var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = _serviceProvider.GetRequiredService<ILogger<OllamaProvider>>();
            var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
            return new OllamaProvider(httpClient, logger, optionsWrapper);
        }

        private ILLMProvider CreateOpenAiProvider(ProviderConfig config)
        {
            var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = _serviceProvider.GetRequiredService<ILogger<OpenAiCompatibleProvider>>();
            return new OpenAiCompatibleProvider(httpClient, logger, config.ApiUrl, config.ApiKey, config.ModelName, config.Name);
        }

        public ILLMProvider GetProvider(string providerName = null)
        {
            if (string.IsNullOrEmpty(providerName))
                providerName = _configuration["LLMProviders:Default"] ?? "gigachatapi";

            providerName = providerName.ToLowerInvariant();
            if (_providerCreators.TryGetValue(providerName, out var creator))
                return creator();

            throw new NotSupportedException($"Провайдер '{providerName}' не поддерживается.");
        }

        private class ProviderConfig
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public bool IsLocal { get; set; }
            public string ApiUrl { get; set; }
            public string ModelName { get; set; }
            public string ApiKey { get; set; }
            public string Scope { get; set; }
        }
    }
}
