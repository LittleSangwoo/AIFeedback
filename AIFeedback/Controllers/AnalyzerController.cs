using Microsoft.AspNetCore.Mvc;

namespace AIFeedback.Controllers
{
    //public class AnalyzerController : Controller
    //{
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
    //}
}