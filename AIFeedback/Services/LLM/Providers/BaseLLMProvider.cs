using AIFeedback.Services.LLM;

namespace AIFeedback.Services.LLM.Providers
{
    public abstract class BaseLLMProvider : ILLMProvider
    {
        protected readonly HttpClient _httpClient;
        protected readonly ILogger _logger;

        // Виртуальное свойство — наследники могут переопределить
        public virtual string ProviderName => GetType().Name;

        protected BaseLLMProvider(HttpClient httpClient, ILogger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public abstract Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0);

        protected virtual async Task<string> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return json;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при вызове LLM");
                throw;
            }
        }

        protected abstract string ExtractContentFromResponse(string jsonResponse);
    }
}
