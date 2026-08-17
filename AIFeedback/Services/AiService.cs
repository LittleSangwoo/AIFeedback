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
            string rawResponse = string.Empty;
            try
            {
                rawResponse = await _llmProvider.AnalyzeTextAsync(systemPrompt, userPrompt);

                //if (string.IsNullOrWhiteSpace(rawResponse))
                //{
                //    _logger.LogWarning("LLM вернула пустой ответ. Переходим на безопасный фолбэк-режим.");
                //    return GetFallbackAnalysisResult();
                //}

                // --- ДОБАВЛЕНО ДЛЯ ДЕБАГА ---
                _logger.LogWarning("===== СЫРОЙ ОТВЕТ ОТ ИИ =====");
                _logger.LogWarning(rawResponse);
                _logger.LogWarning("=============================");

                string cleanedJson = CleanJson(rawResponse);

                var result = JsonSerializer.Deserialize<AiAnalysisResultDto>(
                    cleanedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null) throw new InvalidOperationException("ИИ вернул пустой результат, парсинг JSON не удался.");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка вызова LLM. Активирован защитный режим. Сырой ответ был: '{RawResponse}'", rawResponse);
                // Защитный механизм для устойчивости демо на чемпионате (Критерий 3.3)
                // УДАЛИ ИЛИ ЗАКОММЕНТИРУЙ: return GetFallbackAnalysisResult();

                throw; // Честно пробрасываем ошибку дальше
            }
        }

        private string CleanJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";
            var json = input.Trim();

            int firstBrace = json.IndexOf('{');
            int lastBrace = json.LastIndexOf('}');

            if (firstBrace != -1 && lastBrace != -1 && lastBrace > firstBrace)
            {
                return json.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return json;
        }

        // Запасной детерминированный результат для демонстрации, если у нейросети нет связи
        //private AiAnalysisResultDto GetFallbackAnalysisResult()
        //{
        //    return new AiAnalysisResultDto
        //    {
        //        Sentiment = new SentimentStats
        //        {
        //            PositivePercent = 82.5,
        //            NeutralPercent = 12.0,
        //            NegativePercent = 5.5
        //        },
        //        TopTopics = new List<Topic>
        //        {
        //            new Topic { Name = "Практическая применимость материалов", MentionsCount = 18, IsRelevant = true },
        //            new Topic { Name = "Высокая квалификация преподавателей", MentionsCount = 14, IsRelevant = true },
        //            new Topic { Name = "Запросы на дополнительные кейсы по СПб", MentionsCount = 8, IsRelevant = true },
        //            new Topic { Name = "Теоретический понятийный аппарат", MentionsCount = 3, IsRelevant = false }
        //        },
        //        Conclusions = new List<Conclusion>
        //        {
        //            new Conclusion
        //            {
        //                Priority = "High",
        //                Action = "Увеличить долю практических разборов ситуаций и кейсов Санкт-Петербурга.",
        //                DataProof = "34% слушателей в открытых ответах указали на необходимость региональной привязки кейсов."
        //            },
        //            new Conclusion
        //            {
        //                Priority = "Medium",
        //                Action = "Сократить вводный лекционный блок с юридическими определениями в очном формате.",
        //                DataProof = "Блок теории отмечен как неактуальный 4.3% респондентов, предлагается вынести его в СДО."
        //            }
        //        }
        //    };
        //}
    }
}
