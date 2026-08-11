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

            // --- НАЧАЛО ИЗМЕНЕНИЙ (ЗАЩИТНЫЙ БЛОК ДЛЯ ИИ) ---

            // Создаем пустой объект на случай, если ИИ упадет
            AiAnalysisResultDto analysisResult = new AiAnalysisResultDto
            {
                Sentiment = new SentimentStats { PositivePercent = 0, NeutralPercent = 0, NegativePercent = 0 },
                TopTopics = new List<Topic>(),
                Conclusions = new List<Conclusion>()
            };

            try
            {
                // Пытаемся получить ответ от ИИ
                analysisResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, "groq");
            }
            catch (Exception ex)
            {
                // Если ИИ выдал ошибку (например, из-за настроек провайдера), 
                // мы просто запишем это в лог, но НЕ дадим сайту упасть!
                Console.WriteLine($"Предупреждение: ИИ-анализ пропущен: {ex.Message}");
            }

            // --- КОНЕЦ ИЗМЕНЕНИЙ ---

            // 4. Ты: Сохраняешь результаты в БД (это теперь выполнится ВСЕГДА)
            var analysisRecord = new AnalysisResult
            {
                SessionName = !string.IsNullOrEmpty(parsedData.ProgramName) ? parsedData.ProgramName : "Анализ анкет КУ СПб",
                ProgramName = parsedData.ProgramName,
                ListenerCount = parsedData.ListenerCount,
                CreatedAt = System.DateTime.UtcNow,
                UsefulnessAvg = avgUtility,
                PracticalityAvg = avgPractice,

                // --- НОВЫЕ СТРОЧКИ ДЛЯ КРУГОВОЙ ДИАГРАММЫ ---
                Dist1to3 = parsedData.Dist1to3,
                Dist4to7 = parsedData.Dist4to7,
                Dist8to10 = parsedData.Dist8to10,
                // --------------------------------------------

                SentimentJson = JsonSerializer.Serialize(analysisResult.Sentiment),
                ThemesJson = JsonSerializer.Serialize(analysisResult.TopTopics),
                RecommendationsJson = JsonSerializer.Serialize(analysisResult.Conclusions),
                AiInsightsJson = JsonSerializer.Serialize(analysisResult)
            };

            // Сохраняем через репозиторий
            await _repository.AddAsync(analysisRecord);

            // 6. Редирект на дашборд с отрисовкой твоего графика
            return RedirectToAction("Details", "Dashboard", new { id = analysisRecord.Id });
        }
    }
}