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
                // Передаем providerName, а всю магию переключения провайдер делает сам
                rawResponse = await _llmProvider.AnalyzeTextAsync(systemPrompt, userPrompt, 0.3, providerName);

                // ЗАЩИТА: Проверяем, не промолчала ли сеть
                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    throw new InvalidOperationException("Провайдер ИИ вернул пустой ответ. Возможно, сработал фильтр цензуры или не хватило токенов.");
                }

                string cleanedJson = CleanJson(rawResponse);


                var result = JsonSerializer.Deserialize<AiAnalysisResultDto>(
                    cleanedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null) throw new InvalidOperationException("ИИ вернул пустой результат, парсинг JSON не удался.");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка вызова LLM. Сырой ответ был: '{RawResponse}'", rawResponse);
                throw;
            }
        }

        public async Task<string> AskQuestionAsync(string reportContext, string question, string providerName = null)
        {
            string systemPrompt = $@"Ты — профессиональный AI-аналитик. 
Пользователь задает вопрос по отчету, который ты сгенерировал ранее.
Вот текст отчета:
---
{reportContext}
---
Твоя задача: Ответить на вопрос пользователя точно, вежливо и строго по делу, опираясь на предоставленный отчет. Если информации в отчете нет, используй свои знания, но укажи, что этого нет в исходных данных.";

            // Температуру ставим 0.5 — чтобы ИИ не был совсем роботом, но и не фантазировал
            return await _llmProvider.AnalyzeTextAsync(systemPrompt, question, 0.5, providerName);
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
