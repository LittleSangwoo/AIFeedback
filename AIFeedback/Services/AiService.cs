using AIFeedback.Models.DTOs;
using AIFeedback.Services.LLM;
using AIFeedback.Services.LLM.Providers;
using System.Text.Json;

namespace AIFeedback.Services
{
    public class AiService : IAiService
    {
        private readonly ILLMProvider _llmProvider;
        private readonly ILogger<AiService> _logger;

        public AiService(ILLMProvider llmProvider, ILogger<AiService> logger)
        {
            _llmProvider = llmProvider;
            _logger = logger;
        }

        public async Task<AiAnalysisResultDto> AnalyzeFeedbackAsync(string systemPrompt, string userPrompt, string providerName = null)
        {
            try
            {
                // Вызываем провайдер. Если метод в ILLMProvider не принимает providerName,
                // просто убери этот параметр из вызова ниже.
                var jsonResponse = await _llmProvider.AnalyzeTextAsync(systemPrompt, userPrompt);

                jsonResponse = CleanJson(jsonResponse);

                var result = JsonSerializer.Deserialize<AiAnalysisResultDto>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при парсинге JSON от LLM.");
                throw new ApplicationException("Сбой при ИИ-анализе текста. Проверьте формат ответа модели.", ex);
            }
        }

        private string CleanJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";
            var json = input.Trim();
            if (json.StartsWith("```json")) json = json.Substring(7);
            if (json.StartsWith("```")) json = json.Substring(3);
            if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3);
            return json.Trim();
        }
    }
}
