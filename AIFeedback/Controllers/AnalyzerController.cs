using AIFeedback.Data;
using AIFeedback.Services;
using AIFeedback.Services.Excel;
using AIFeedback.Services.Report;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace AIFeedback.Controllers
{
    public class AnalyzerController : Controller
    {
        private readonly IExcelParserService _excelParser;
        private readonly IAiService _aiService;
        private readonly IReportService _reportService;
        private readonly ApplicationDbContext _db;

        public AnalyzerController(IExcelParserService excelParser, IAiService aiService, IReportService reportService, ApplicationDbContext db)
        {
            _excelParser = excelParser;
            _aiService = aiService;
            _reportService = reportService;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessUpload(IFormFile uploadFile)
        {
            if (uploadFile == null || uploadFile.Length == 0)
                return BadRequest("Файл не выбран.");

            var sw = Stopwatch.StartNew();

            /// 1. Получаем кортеж от парсера Разработчика 2
            var parsedData = await _excelParser.ParseAsync(uploadFile.OpenReadStream());

            // Безопасно достаем средние баллы из словаря (ключи могут немного отличаться, проверь с напарницей)
            double avgUtility = parsedData.NumericAverages.GetValueOrDefault("Полезность", 0);
            double avgPractice = parsedData.NumericAverages.GetValueOrDefault("Практико-ориентированность", 0);
            double avgAccess = parsedData.NumericAverages.GetValueOrDefault("Доступность", 0);
            double avgEngagement = parsedData.NumericAverages.GetValueOrDefault("Взаимодействие с КУ", 0);

            // Агрегируем все 19 открытых вопросов в один текст для ИИ
            string rawComments = string.Join("\n", parsedData.AllComments);

            // 2. Формируем промпты для ИИ
            string systemPrompt = @"Ты — главный аналитик Корпоративного университета Санкт-Петербурга. 
Твоя задача — проанализировать отзывы госслужащих и выдать результат СТРОГО в формате JSON.
Критерии анализа:
1. Выдели топ-5-7 тем (themes), подсчитай примерное количество упоминаний. Отметь неактуальные темы (isRelevant: false).
2. Сделай сентимент-анализ (sentiment) в процентах (в сумме 100%).
3. Извлеки ключевые проблемы (problems), укажи частоту и приведи 1-2 репрезентативные цитаты (quotes).
4. Сгенерируй 3-7 рекомендаций (recommendations). Каждая рекомендация ДОЛЖНА содержать поле 'dataProof' с обоснованием на основе цифр.";

            string userPrompt = $"Средний балл полезности: {avgUtility}\n" +
                                $"Средний балл практико-ориентированности: {avgPractice}\n\n" +
                                $"Ответы слушателей:\n{rawComments}";

            // 3. Вызываем ИИ (указываем groq, как самый частый)
            var aiResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, "groq");

            // 3. Формируем финальную модель для БД и Дашборда
            var analysisRecord = new AnalysisResult
            {
                SessionName = parsedData.ProgramName,
                DateProcessed = DateTime.UtcNow,
                AvgUtility = avgUtility,
                AvgPractice = avgPractice,
                AvgAccessibility = avgAccess,
                AvgEngagement = avgEngagement,
                AiInsightsJson = JsonSerializer.Serialize(aiResult),
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };

            _db.AnalysisResults.Add(analysisRecord);
            await _db.SaveChangesAsync();

            // Перенаправляем на дашборд
            return RedirectToAction("Index", "Dashboard", new { id = analysisRecord.Id });
        }

        //    // 1. Объявляем зависимости
        //    private readonly IExcelParserService _excelParserService;
        //    private readonly IAiService _aiService;
        //    private readonly IStatsRepository _repository;
        //    private readonly IReportService _reportService;

        //    // 2. Внедряем зависимости через конструктор (DI)
        //    public AnalyzerController(
        //        IExcelParserService excelParserService,
        //        IAiService aiService,
        //        IStatsRepository repository,
        //        IReportService reportService)
        //    {
        //        _excelParserService = excelParserService;
        //        _aiService = aiService;
        //        _repository = repository;
        //        _reportService = reportService;
        //    }

        //    [HttpPost]
        //    public async Task<IActionResult> ProcessFile(IFormFile uploadFile)
        //    {
        //        if (uploadFile == null || uploadFile.Length == 0)
        //        {
        //            return BadRequest("Файл не выбран");
        //        }

        //        // 1. Напарница: Парсинг Excel и Математика
        //        // Пока напарница не напишет сервис, можешь возвращать фейковые данные
        //        var mathStats = _excelParserService.CalculateStats(uploadFile);

        //        // Получаем сырые комментарии (напарница должна добавить этот метод в парсер)
        //        var rawComments = _excelParserService.ExtractComments(uploadFile);

        //        // 2. Ты: Формируешь промпт и обращаешься к LLM
        //        var aiResultJson = await _aiService.GetComprehensiveAnalysisAsync(mathStats, rawComments);

        //        // 3. Ты: Десериализуешь JSON в DTO
        //        var analysisResult = JsonSerializer.Deserialize<AiAnalysisResultDto>(aiResultJson, new JsonSerializerOptions
        //        {
        //            PropertyNameCaseInsensitive = true // Полезно, чтобы не падать из-за регистра букв
        //        });

        //        // 4. Ты: Сохраняешь результаты в БД
        //        await _repository.SaveSessionAsync(mathStats, analysisResult);

        //        // 5. Напарница: Генерация Word
        //        var reportPath = _reportService.GenerateWordDoc(mathStats, analysisResult);

        //        // 6. Подготавливаем данные для View
        //        var viewModel = new AnalysisResultViewModel
        //        {
        //            MathStats = mathStats,
        //            AiAnalysis = analysisResult,
        //            ReportDownloadUrl = reportPath
        //        };

        //        return View("AnalysisResult", viewModel);
        //    }
    }
}