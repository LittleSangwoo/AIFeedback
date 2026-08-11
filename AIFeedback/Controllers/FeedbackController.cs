using AIFeedback.Data;
using AIFeedback.Models.DTOs;
using AIFeedback.Services;
using AIFeedback.Services.Excel;
using AIFeedback.Services.Report;
using AIFeedback.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AIFeedback.Controllers
{
    //    public class FeedbackController : Controller
    //    {
    //        private readonly IExcelParserService _excelParser;
    //        private readonly IAiService _aiService;
    //        private readonly IReportService _reportService; // генерация Word/PDF
    //        private readonly IAnalysisResultRepository _repository;

    //        public FeedbackController(IExcelParserService excelParser,
    //                                  IAiService aiService,
    //                                  IReportService reportService,
    //                                  IAnalysisResultRepository repository)
    //        {
    //            _excelParser = excelParser;
    //            _aiService = aiService;
    //            _reportService = reportService;
    //            _repository = repository;
    //        }

    //        // GET: /Feedback/Process?provider=groq&fileName=...
    //        public IActionResult Process(string provider, string fileName)
    //        {
    //            // Показываем страницу ожидания
    //            var model = new ProcessingViewModel
    //            {
    //                Provider = provider,
    //                FileName = fileName,
    //                IsProcessing = true
    //            };
    //            return View(model);
    //        }

    //        [HttpPost]
    //        public async Task<IActionResult> ProcessFile(string provider, IFormFile file)
    //        {
    //            // Этот метод вызывается через AJAX или форму с файлом.
    //            // Но для простоты мы обработаем синхронно в Process.

    //            // 1. Парсинг Excel
    //            using var stream = file.OpenReadStream();
    //            var parsedData = await _excelParser.ParseAsync(stream);

    //            // 2. Формируем промпты для ИИ
    //            var systemPrompt = @"
    //Ты — аналитик образовательных программ Корпоративного университета Санкт-Петербурга.
    //Проанализируй комментарии слушателей и верни JSON-объект с полями:
    //- Sentiment: { PositivePercent, NeutralPercent, NegativePercent }
    //- TopTopics: список объектов { Name, MentionCount } (топ-7 тем)
    //- UnrelevantTopics: список строк (темы, которые слушатели считают неактуальными)
    //- Conclusions: список объектов { Text, Recommendation, Priority } (3-7 выводов с рекомендациями)
    //";
    //            var userPrompt = $"Комментарии слушателей:\n{string.Join("\n", parsedData.Comments)}";

    //            // 3. Запуск ИИ-аналитики
    //            var aiResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, provider);

    //            // 4. Формируем ViewModel
    //            var viewModel = BuildResultViewModel(parsedData, aiResult);

    //            // 5. Сохраняем результат в БД (через репозиторий)
    //            // ...

    //            // 6. Генерируем отчёт Word (асинхронно, сохраняем в wwwroot/reports/)
    //            await _reportService.GenerateWordReportAsync(viewModel, $"wwwroot/reports/{parsedData.ProgramName}_{DateTime.Now:yyyyMMddHHmm}.docx");

    //            // 7. Возвращаем дашборд
    //            return View("Result", viewModel);
    //        }

    //        private ResultViewModel BuildResultViewModel(ExcelParsedData parsed, AiAnalysisResultDto aiResult)
    //        {
    //            // Здесь агрегируем все данные из парсинга и ИИ.
    //            // Для краткости опускаем детали, так как структура понятна.
    //            var vm = new ResultViewModel
    //            {
    //                ProgramName = parsed.ProgramName,
    //                Period = parsed.Period,
    //                TrainingFormat = parsed.TrainingFormat,
    //                ListenerCount = parsed.ListenerCount,
    //                Teachers = parsed.Teachers,
    //                Averages = parsed.Averages, // словарь
    //                OverallSatisfaction = parsed.OverallSatisfaction,
    //                Distribution = parsed.Distribution,
    //                AiAnalysis = aiResult,
    //                EngagementYesPercent = parsed.EngagementYesPercent,
    //                EngagementReasons = parsed.EngagementReasons,
    //                IrrelevantTopics = aiResult.UnrelevantTopics,
    //                SuggestedTopics = parsed.SuggestedTopics,
    //                PreferredFormats = parsed.PreferredFormats,
    //                Conclusions = aiResult.Conclusions
    //            };
    //            return vm;
    //        }
    //    }

    //public class FeedbackController : Controller
    //{
    //    private readonly IExcelParserService _excelParser;
    //    private readonly IAiService _aiService;
    //    private readonly IAnalysisResultRepository _repository;

    //    public FeedbackController(IExcelParserService excelParser, IAiService aiService, IAnalysisResultRepository repository)
    //    {
    //        _excelParser = excelParser;
    //        _aiService = aiService;
    //        _repository = repository;
    //    }

    //    [HttpGet]
    //    public IActionResult Process()
    //    {
    //        // Просто отображаем страницу ожидания (если переход с главной)
    //        return View(new ProcessingViewModel { IsComplete = false, StatusMessage = "Загрузка..." });
    //    }

    //    [HttpPost]
    //    public async Task<IActionResult> Process(IFormFile excelFile, string? provider = null)
    //    {
    //        if (excelFile == null || excelFile.Length == 0)
    //            return BadRequest("Файл не загружен");

    //        using var stream = excelFile.OpenReadStream();

    //        // 1. Парсим Excel (заглушка)
    //        var (programName, listenerCount, numericAverages, allComments) = await _excelParser.ParseAsync(stream);

    //        // 2. Вызываем ИИ-аналитику
    //        // Формируем промпты (здесь можно использовать шаблоны из ТЗ)
    //        var systemPrompt = "Ты — аналитик образовательных программ. Проанализируй комментарии слушателей.";
    //        var userPrompt = $"Комментарии:\n{string.Join("\n", allComments)}";
    //        var aiResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, provider);

    //        // 3. Сохраняем результат в БД
    //        var analysisResult = new Domain.Entities.AnalysisResult
    //        {
    //            CreatedAt = DateTime.UtcNow,
    //            ProgramName = programName,
    //            ListenerCount = listenerCount,
    //            UsefulnessAvg = numericAverages["Usefulness"],
    //            AvailabilityAvg = numericAverages["Availability"],
    //            PracticalityAvg = numericAverages["Practicality"],
    //            InteractionAvg = numericAverages["Interaction"],
    //            EngagementYesPercent = numericAverages["EngagementYesPercent"],
    //            OverallSatisfaction = (numericAverages["Usefulness"] + numericAverages["Availability"] + numericAverages["Practicality"] + numericAverages["Interaction"]) / 4.0,
    //            ThemesJson = JsonSerializer.Serialize(aiResult.TopTopics),
    //            SentimentJson = JsonSerializer.Serialize(aiResult.Sentiment),
    //            ProblemsJson = JsonSerializer.Serialize(new { Problems = new List<string>() }), // заглушка
    //            QuotesJson = JsonSerializer.Serialize(new List<string>()),
    //            RecommendationsJson = JsonSerializer.Serialize(aiResult.Conclusions.Select(c => c.Recommendation).ToList())
    //        };
    //        await _repository.AddAsync(analysisResult);

    //        // 4. Показываем страницу с ID анализа для перехода на дашборд
    //        var model = new ProcessingViewModel
    //        {
    //            AnalysisId = analysisResult.Id,
    //            ProgramName = programName,
    //            IsComplete = true,
    //            StatusMessage = "Обработка завершена!",
    //            StartedAt = DateTime.UtcNow,
    //            CompletedAt = DateTime.UtcNow
    //        };
    //        return View(model);
    //    }
    //}


    public class FeedbackController : Controller
    {
        private readonly IExcelParserService _excelParser;
        private readonly IAiService _aiService;
        private readonly IAnalysisResultRepository _repository;

        public FeedbackController(IExcelParserService excelParser, IAiService aiService, IAnalysisResultRepository repository)
        {
            _excelParser = excelParser;
            _aiService = aiService;
            _repository = repository;
        }

        [HttpGet]
        public IActionResult Process()
        {
            // Отображаем страницу ожидания
            return View(new ProcessingViewModel { IsComplete = false, StatusMessage = "Ожидание загрузки..." });
        }

        [HttpPost]
        public async Task<IActionResult> Process(IFormFile excelFile, string? provider = null)
        {
            if (excelFile == null || excelFile.Length == 0)
                return BadRequest("Файл не загружен");

            using var stream = excelFile.OpenReadStream();

            // 1. Надежный парсинг Excel (получаем данные + счетчики для диаграммы)
            var (programName, listenerCount, numericAverages, allComments, dist1to3, dist4to7, dist8to10) = await _excelParser.ParseAsync(stream);

            // 2. Базовый объект для сохранения (ему не нужен ИИ, чтобы работать!)
            var analysisResult = new Data.AnalysisResult
            {
                CreatedAt = DateTime.UtcNow,
                ProgramName = programName,
                ListenerCount = listenerCount,
                UsefulnessAvg = numericAverages.GetValueOrDefault("Usefulness", 0),
                AvailabilityAvg = numericAverages.GetValueOrDefault("Accessibility", 0),
                PracticalityAvg = numericAverages.GetValueOrDefault("Practicality", 0),
                InteractionAvg = numericAverages.GetValueOrDefault("Interaction", 0),
                EngagementYesPercent = 0,
                OverallSatisfaction = (numericAverages.GetValueOrDefault("Usefulness", 0) +
                                       numericAverages.GetValueOrDefault("Practicality", 0)) / 2.0,

                // Сохраняем счетчики для круговой диаграммы
                Dist1to3 = dist1to3,
                Dist4to7 = dist4to7,
                Dist8to10 = dist8to10,

                // Пустые заглушки для ИИ по умолчанию
                ThemesJson = "[]",
                SentimentJson = "{}",
                ProblemsJson = "[]",
                QuotesJson = "[]",
                RecommendationsJson = "[]"
            };

            // 3. Пытаемся запустить ИИ-аналитику опционально (если упадет — дашборд всё равно откроется!)
            try
            {
                if (!string.IsNullOrEmpty(provider))
                {
                    var systemPrompt = "Ты — аналитик образовательных программ. Проанализируй комментарии слушателей.";
                    var userPrompt = $"Комментарии:\n{string.Join("\n", allComments)}";
                    var aiResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, provider);

                    if (aiResult != null)
                    {
                        analysisResult.ThemesJson = JsonSerializer.Serialize(aiResult.TopTopics);
                        analysisResult.SentimentJson = JsonSerializer.Serialize(aiResult.Sentiment);
                        analysisResult.RecommendationsJson = JsonSerializer.Serialize(aiResult.Conclusions.Select(c => c.Recommendation).ToList());
                    }
                }
            }
            catch (Exception ex)
            {
                // ИИ отвалился, но мы это просто логируем и идем дальше, не ломая пользователю загрузку!
                Console.WriteLine($"Предупреждение: ИИ-анализ пропущен из-за ошибки: {ex.Message}");
            }

            // 4. Сохраняем результат в БД
            await _repository.AddAsync(analysisResult);

            // 5. Переход на страницу с результатом (дашборд откроется в любом случае!)
            var model = new ProcessingViewModel
            {
                AnalysisId = analysisResult.Id,
                ProgramName = programName,
                IsComplete = true,
                StatusMessage = "Обработка завершена!",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            return View(model);
        }
    }
}
