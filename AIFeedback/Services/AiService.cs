using AIFeedback.Models.DTOs;
using AIFeedback.Services.LLM.Providers;
using System.Text.Json;

namespace AIFeedback.Services
{
    public class AiService : IAiService
    {
        private readonly ILLMProviderFactory _providerFactory;
        private readonly ILogger<AiService> _logger;

        public AiService(ILLMProviderFactory providerFactory, ILogger<AiService> logger)
        {
            _providerFactory = providerFactory;
            _logger = logger;
        }

        public async Task<AiAnalysisResultDto> AnalyzeFeedbackAsync(string systemPrompt, string userPrompt, string providerName = null)
        {
            var provider = _providerFactory.GetProvider(providerName);
            var response = await provider.AnalyzeTextAsync(systemPrompt, userPrompt, 0.0);

            try
            {
                var result = JsonSerializer.Deserialize<AiAnalysisResultDto>(response);
                return result ?? new AiAnalysisResultDto();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Ошибка десериализации ответа от LLM. Ответ: {Response}", response);
                throw new InvalidOperationException("Не удалось распарсить ответ от LLM", ex);
            }
        }
    }
}
