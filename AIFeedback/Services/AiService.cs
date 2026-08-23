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
                // Передаем providerName, переключения провайдер делает сам
                rawResponse = await _llmProvider.AnalyzeTextAsync(systemPrompt, userPrompt, providerName: providerName);

                // Проверяем, не промолчала ли сеть
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
            string compactContext = BuildCompactContext(reportContext);
            
            string systemPrompt = $@"Ты — профессиональный AI-аналитик. 
Пользователь задает вопрос по отчету, который ты сгенерировал ранее.
Вот краткое содержание отчета (без служебной разметки):
---
{compactContext}
---
Твоя задача: Ответить на вопрос пользователя точно, вежливо и строго по делу, опираясь на предоставленный отчет. Если информации в отчете нет, используй свои знания, но укажи, что этого нет в исходных данных.";

            try
            {
                // Температуру ставим 0.5 — чтобы ИИ не был совсем роботом, но и не фантазировал
                return await _llmProvider.AnalyzeTextAsync(systemPrompt, question, 0.5, providerName);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "AskQuestionAsync: провайдер вернул rate limit.");
                return "⚠️ Провайдер ИИ временно ограничивает частоту запросов (превышен лимит токенов в минуту). " +
                       "Подождите около минуты и задайте вопрос ещё раз — это не ошибка приложения, а ограничение самого провайдера.";
            }
        }

        private string BuildCompactContext(string reportContextJson)
        {
            if (string.IsNullOrWhiteSpace(reportContextJson)) return "Данные анализа отсутствуют.";

            try
            {
                var dto = JsonSerializer.Deserialize<AiAnalysisResultDto>(reportContextJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dto == null) return reportContextJson;

                var sb = new System.Text.StringBuilder();

                sb.AppendLine($"Тональность отзывов: позитив {dto.Sentiment.PositivePercent}%, " +
                               $"нейтрально {dto.Sentiment.NeutralPercent}%, негатив {dto.Sentiment.NegativePercent}%.");
                sb.AppendLine();

                sb.AppendLine("Примечания по критериям:");
                sb.AppendLine($"— Полезность: {dto.MetricsNotes.Usefulness}");
                sb.AppendLine($"— Практика: {dto.MetricsNotes.Practicality}");
                sb.AppendLine($"— Доступность: {dto.MetricsNotes.Accessibility}");
                sb.AppendLine($"— Взаимодействие: {dto.MetricsNotes.Interaction}");
                sb.AppendLine($"— Вовлечённость: {dto.MetricsNotes.Engagement}");
                sb.AppendLine();

                sb.AppendLine("Ключевые выводы:");
                foreach (var c in dto.Conclusions)
                {
                    sb.AppendLine($"— [{c.Priority}] {c.Action} (обоснование: {c.DataProof})");
                    if (c.SupportingQuotes != null && c.SupportingQuotes.Count > 0)
                    {
                        var firstQuote = c.SupportingQuotes[0];
                        sb.AppendLine($"    Цитата: «{firstQuote.Text}»");
                    }
                }
                sb.AppendLine();

                sb.AppendLine($"Неактуальные темы: {dto.UnnecessaryTopics}");
                sb.AppendLine($"Темы для добавления: {dto.TopicsToAdd}");
                sb.AppendLine();

                sb.AppendLine("Траектория изменения программы:");
                sb.AppendLine($"— Актуальность: {dto.Trajectory.Relevance}");
                sb.AppendLine($"— Отбор слушателей: {dto.Trajectory.Selection}");
                sb.AppendLine($"— Дополнения: {dto.Trajectory.Additions}");
                sb.AppendLine($"— Количество часов: {dto.Trajectory.Hours}");
                sb.AppendLine($"— Формат обучения: {dto.Trajectory.Format}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BuildCompactContext: не удалось разобрать AiInsightsJson, отправляю как есть.");
                return reportContextJson;
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
    }
}
