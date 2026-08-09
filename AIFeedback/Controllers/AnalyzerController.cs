using AIFeedback.Data;                  // Для IAnalysisResultRepository и AnalysisResult
using AIFeedback.Models.DTOs;           // Для AiAnalysisResultDto
// Подключаем наши пространства имен:
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
        private readonly IAnalysisResultRepository _repository; // ИСПРАВЛЕНО
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
        public async Task<IActionResult> ProcessFile(IFormFile uploadFile)
        {
            if (uploadFile == null || uploadFile.Length == 0)
            {
                return BadRequest("Файл не выбран");
            }

            // 1. Напарница: Парсинг Excel и Математика
            // Ожидаем, что напарница вернет объект с нужными данными (подстрой под ее метод)
            var parsedData = await _excelParserService.ParseAsync(uploadFile.OpenReadStream());

            double avgUtility = parsedData.NumericAverages.GetValueOrDefault("Полезность", 0);
            double avgPractice = parsedData.NumericAverages.GetValueOrDefault("Практико-ориентированность", 0);
            string rawComments = string.Join("\n", parsedData.AllComments);

            // 2. Ты: Формируешь промпты и обращаешься к LLM
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
            AiAnalysisResultDto analysisResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, "groq");
            //// 3. Ты: Десериализуешь JSON в DTO
            //var analysisResult = JsonSerializer.Deserialize<AiAnalysisResultDto>(aiResultJson, new JsonSerializerOptions
            //{
            //    PropertyNameCaseInsensitive = true
            //});

            // 4. Ты: Сохраняешь результаты в БД (используем сущность AnalysisResult)
            var analysisRecord = new AnalysisResult
            {// Убедись, что здесь указано непустое значение!
             // Убедись, что здесь указано непустое значение!
                SessionName = !string.IsNullOrEmpty(parsedData.ProgramName) ? parsedData.ProgramName : "Анализ анкет КУ СПб",
                CreatedAt = System.DateTime.UtcNow,
                UsefulnessAvg = avgUtility,
                PracticalityAvg = avgPractice,

                // ИСПРАВЛЕНИЕ 2: Используем правильные имена свойств из твоего DTO (TopTopics и Conclusions)
                SentimentJson = JsonSerializer.Serialize(analysisResult.Sentiment),
                ThemesJson = JsonSerializer.Serialize(analysisResult.TopTopics),
                RecommendationsJson = JsonSerializer.Serialize(analysisResult.Conclusions),

                // Если в твоей модели AnalysisResult есть отдельное поле для единого JSON (AiInsightsJson):
                AiInsightsJson = JsonSerializer.Serialize(analysisResult)
            };

            // Сохраняем через репозиторий (уточни точное название метода добавления у напарницы, например AddAsync)
            await _repository.AddAsync(analysisRecord);

            // 5. Напарница: Генерация Word (вызов ее сервиса)
            // _reportService.GenerateWordDoc(...);

            // 6. Редирект на дашборд с отрисовкой твоих графиков
            return RedirectToAction("Details", "Dashboard", new { id = analysisRecord.Id });
        }
    }
}