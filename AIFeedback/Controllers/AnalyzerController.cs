using AIFeedback.Data;                  // Для IAnalysisResultRepository и AnalysisResult
using AIFeedback.Models.DTOs;           // Для AiAnalysisResultDto
using AIFeedback.Services;              // Для IAiService
using AIFeedback.Services.Excel;        // Для IExcelParserService
using AIFeedback.Services.Report;       // Для IReportService
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AIFeedback.Controllers
{
    public class AnalyzerController : Controller
    {
        // 1. Объявляем правильные зависимости
        private readonly IExcelParserService _excelParserService;
        private readonly IAiService _aiService;
        private readonly IAnalysisResultRepository _repository;
        private readonly IReportService _reportService;

        // 2. Внедряем зависимости через конструктор (DI)
        public AnalyzerController(
            IExcelParserService excelParserService,
            IAiService aiService,
            IAnalysisResultRepository repository,
            IReportService reportService)
        {
            _excelParserService = excelParserService;
            _aiService = aiService;
            _repository = repository;
            _reportService = reportService;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessFile(IFormFile uploadFile, string providerName)
        {
            if (uploadFile == null || uploadFile.Length == 0)
            {
                return BadRequest("Файл не выбран");
            }

            // 1. Парсинг Excel
            var parsedData = await _excelParserService.ParseAsync(uploadFile.OpenReadStream());

            // ИСПРАВЛЕНИЕ 1: Ищем значения по АНГЛИЙСКИМ ключам из парсера
            double avgUtility = parsedData.NumericAverages.GetValueOrDefault("Usefulness", 0);
            double avgPractice = parsedData.NumericAverages.GetValueOrDefault("Practicality", 0);
            double avgAccessibility = parsedData.NumericAverages.GetValueOrDefault("Accessibility", 0);
            double avgInteraction = parsedData.NumericAverages.GetValueOrDefault("Interaction", 0);

            // --- ВЫТАСКИВАЕМ ВОВЛЕЧЕННОСТЬ ---
            double avgEngagement = parsedData.NumericAverages.GetValueOrDefault("Engagement", 0);

            // Считаем общую удовлетворенность (среднее из 4 критериев)
            double overallSatisfaction = (avgUtility + avgPractice + avgAccessibility + avgInteraction) / 4.0;

            string rawComments = string.Join("\n", parsedData.AllComments);

            // 2. Промпты для LLM
            string systemPrompt = @"You are a data analyst. You MUST output your analysis STRICTLY as a valid JSON object. 
DO NOT output any markdown (like ```json). DO NOT output any conversational text before or after the JSON.
Use EXACTLY this JSON structure, filling in the text values in Russian:
{
  ""Sentiment"": {
    ""PositivePercent"": 70.0,
    ""NeutralPercent"": 20.0,
    ""NegativePercent"": 10.0
  },
  ""TopTopics"": [
    {
      ""Name"": ""Название проблемы или темы"",
      ""MentionsCount"": 5,
      ""IsRelevant"": true
    }
  ],
  ""Conclusions"": [
    {
      ""Priority"": ""High"",
      ""Action"": ""Конкретная рекомендация"",
      ""DataProof"": ""Обоснование на основе цифр""
    }
  ]
}";
            string userPrompt = $"Балл полезности: {avgUtility}\nОтветы:\n{rawComments}";

            // ИСПРАВЛЕНИЕ 1: Метод УЖЕ возвращает готовый DTO, десериализация в контроллере больше не нужна!
            AiAnalysisResultDto analysisResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, providerName);
            //// 3. Ты: Десериализуешь JSON в DTO
            //var analysisResult = JsonSerializer.Deserialize<AiAnalysisResultDto>(aiResultJson, new JsonSerializerOptions
            //{
            //    PropertyNameCaseInsensitive = true
            //});

            try
            {
                analysisResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, "groq");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Предупреждение: ИИ-анализ пропущен: {ex.Message}");
            }

            // 4. ИСПРАВЛЕНИЕ 2: Сохраняем ВСЕ метрики в БД
            var analysisRecord = new AnalysisResult
            {
                SessionName = !string.IsNullOrEmpty(parsedData.ProgramName) ? parsedData.ProgramName : "Анализ анкет КУ СПб",
                ProgramName = parsedData.ProgramName,
                ListenerCount = parsedData.ListenerCount,
                CreatedAt = System.DateTime.UtcNow,

                // Записываем все 4 критерия и общий балл:
                UsefulnessAvg = avgUtility,
                PracticalityAvg = avgPractice,
                AvailabilityAvg = avgAccessibility,
                InteractionAvg = avgInteraction,
                OverallSatisfaction = overallSatisfaction,

                // --- ПЕРЕДАЕМ В БД РЕАЛЬНЫЕ 91% или 97% ВМЕСТО НУЛЯ ---
                EngagementYesPercent = (int)avgEngagement,

                // Распределение
                Dist1to3 = parsedData.Dist1to3,
                Dist4to7 = parsedData.Dist4to7,
                Dist8to10 = parsedData.Dist8to10,

                SentimentJson = JsonSerializer.Serialize(analysisResult.Sentiment),
                ThemesJson = JsonSerializer.Serialize(analysisResult.TopTopics),
                RecommendationsJson = JsonSerializer.Serialize(analysisResult.Conclusions),
                AiInsightsJson = JsonSerializer.Serialize(analysisResult)
            };

            await _repository.AddAsync(analysisRecord);

            return RedirectToAction("Details", "Dashboard", new { id = analysisRecord.Id });
        }
    }
}