using AIFeedback.Data;                  // Для IAnalysisResultRepository и AnalysisResult
using AIFeedback.Models.DTOs;           // Для AiAnalysisResultDto
using AIFeedback.Services;              // Для IAiService
using AIFeedback.Services.Excel;        // Для IExcelParserService
using AIFeedback.Services.Report;       // Для IReportService
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace AIFeedback.Controllers
{
    public class AnalyzerController : Controller
    {
        private readonly IExcelParserService _excelParserService;
        private readonly IAiService _aiService;
        private readonly IAnalysisResultRepository _repository;
        private readonly IReportService _reportService;

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
        [RequestSizeLimit(104857600)] // Увеличиваем лимит запроса до 100 МБ для мульти-загрузки
        public async Task<IActionResult> ProcessFile(IFormFile uploadFile, List<IFormFile> historyFiles, bool isTrendEnabled, string providerName)
        {
            if (uploadFile == null || uploadFile.Length == 0)
            {
                return BadRequest("Основной файл не выбран");
            }

            //  ПАРСИНГ ОСНОВНОГО ФАЙЛА

            // ПЕРЕДАЕМ uploadFile.FileName В ПАРСЕР И ПРИНИМАЕМ ДАННЫЕ О ФОРМАТЕ ОБУЧЕНИЯ
            var (progName, listenerCount, numericAvgs, allComments, d1to3, d4to7, d8to10, matrixJson, distJson, fOffline, fMixed, fOnline, eCount, dCount) =
                await _excelParserService.ParseAsync(uploadFile.OpenReadStream(), uploadFile.FileName);

            double avgUtility = numericAvgs.GetValueOrDefault("Usefulness", 0);
            double avgPractice = numericAvgs.GetValueOrDefault("Practicality", 0);
            double avgAccessibility = numericAvgs.GetValueOrDefault("Accessibility", 0);
            double avgInteraction = numericAvgs.GetValueOrDefault("Interaction", 0);

            // Считаем точный процент вовлеченности
            int totalE = eCount + dCount;
            int engPct = totalE > 0 ? (int)Math.Round((double)eCount / totalE * 100) : 0;

            // Общая удовлетворенность текущего потока
            double overallSatisfaction = (avgUtility + avgPractice + avgAccessibility + avgInteraction) / 4.0;

            string rawComments = string.Join("\n", allComments);

            var trendLabels = new List<string>();
            var trendValues = new List<double>();

            if (isTrendEnabled && historyFiles != null && historyFiles.Count > 0)
            {
                // Словарь для группировки: Ключ - очищенное имя потока, Значение - список баллов
                var groupedHistory = new Dictionary<string, List<double>>();

                foreach (var file in historyFiles)
                {
                    if (file.Length > 0)
                    {
                        using var histStream = file.OpenReadStream();
                        double histScore = await _excelParserService.ParseHistoryFileAsync(histStream);

                        string rawName = Path.GetFileNameWithoutExtension(file.FileName);
                        string baseName = CleanFileName(rawName);

                        if (!groupedHistory.ContainsKey(baseName))
                        {
                            groupedHistory[baseName] = new List<double>();
                        }
                        groupedHistory[baseName].Add(histScore);
                    }
                }

                // Усредняем баллы для каждой группы и сортируем по имени (чтобы даты шли по порядку)
                var historyData = groupedHistory
                    .Select(kvp => (Name: kvp.Key, Score: kvp.Value.Average()))
                    .OrderBy(x => x.Name)
                    .ToList();

                trendLabels.AddRange(historyData.Select(x => x.Name));
                trendValues.AddRange(historyData.Select(x => x.Score));
            }

            // Добавляем результаты текущего файла в самый конец тренда
            trendLabels.Add("Текущий поток");
            trendValues.Add(Math.Round(overallSatisfaction, 1));

            // ПРОМПТЫ И ВЫЗОВ ИИ
            string systemPrompt = @"Ты — профессиональный AI-аналитик образовательных программ. Твоя задача проанализировать сырые отзывы и метрики, и выдать детальный профессиональный отчет СТРОГО в формате JSON.
Не используй markdown (никаких ```json). 
Опирайся на реальные цифры и комментарии. Структура JSON должна быть РОВНО такой:

{
  ""Sentiment"": {
    ""PositivePercent"": 70.0,
    ""NeutralPercent"": 20.0,
    ""NegativePercent"": 10.0
  },
  ""TopTopics"": [
    {
      ""Name"": ""Название темы"",
      ""MentionsCount"": 5,
      ""IsRelevant"": true
    }
  ],
  ""Conclusions"": [
    {
      ""Priority"": ""High"",
      ""Action"": ""Что нужно сделать"",
      ""DataProof"": ""Обоснование на основе цифр""
    }
  ],
  ""MetricsNotes"": {
    ""Usefulness"": ""Напиши текст примечания по полезности. Пример: 'X чел. (Y%) высоко оценили полезность программы. Наиболее актуальными для выполнения обязанностей являются: - тема 1 (N чел., M%); - тема 2...'"",
    ""Practicality"": ""Напиши текст примечания по практике. Пример: 'X чел. (Y%) высоко оценили практику. Однако N чел. указали на нехватку практики по темам: - тема 1...'"",
    ""Accessibility"": ""Напиши текст примечания по доступности и логике изложения. Пример: 'Замечаний к логике изложения нет.'"",
    ""Interaction"": ""Напиши текст примечания по работе команды."",
    ""Engagement"": ""Напиши подробный текст по вовлеченности (что могло бы повлиять на повышение интереса, почему люди отстранялись).""
  },
  ""UnnecessaryTopics"": ""Сгенерируй нумерованный список неактуальных тем (1, 2, 3...). Если их нет, напиши 'Неактуальных тем не выявлено.'"",
  ""TopicsToAdd"": ""Сгенерируй нумерованный список тем для добавления (1, 2, 3...). Если их нет, напиши 'Дополнений не зафиксировано.'"",
  ""Trajectory"": {
    ""Relevance"": ""Вывод о потребности в реализации. Пример: 'Программа актуальна и востребована, высокая потребность в дальнейшей реализации.'"",
    ""Selection"": ""Вывод о корректировке отбора."",
    ""Additions"": ""Вывод о дополнении программы (на основе отзывов)."",
    ""Hours"": ""Вывод об изменении количества часов."",
    ""Format"": ""Вывод об изменении формы обучения.""
  }
}";

            // Если текст отзывов длиннее 10000 символов, берем только начало
            if (rawComments.Length > 10000)
            {
                rawComments = rawComments.Substring(0, 10000) + "... [ДАННЫЕ ОБРЕЗАНЫ ИЗ-ЗА ЛИМИТОВ]";
            }

            string userPrompt = $"Слушателей: {listenerCount}. Средние баллы: Полезность {avgUtility}, Практика {avgPractice}, Доступность {avgAccessibility}, Взаимодействие {avgInteraction}. Вовлечены: {eCount} чел ({engPct}%), Отстранены: {dCount} чел.\nТекстовые отзывы:\n{rawComments}";

            AiAnalysisResultDto analysisResult = new AiAnalysisResultDto();

            try
            {
                analysisResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, providerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Предупреждение: ИИ-анализ пропущен: {ex.Message}");
                TempData["AiWarning"] = "Связь с нейросетью временно недоступна. Дашборд построен только на основе расчетов.";
            }

            //СОХРАНЕНИЕ В БАЗУ ДАННЫХ

            var analysisRecord = new AnalysisResult
            {
                SessionName = !string.IsNullOrEmpty(progName) ? progName : "Аналитическая справка",
                ProgramName = progName,
                ListenerCount = listenerCount,
                CreatedAt = DateTime.UtcNow,

                UsefulnessAvg = avgUtility,
                PracticalityAvg = avgPractice,
                AvailabilityAvg = avgAccessibility,
                InteractionAvg = avgInteraction,
                OverallSatisfaction = overallSatisfaction,
                EngagementYesPercent = engPct,

                Dist1to3 = d1to3,
                Dist4to7 = d4to7,
                Dist8to10 = d8to10,

                FormatOfflineCount = fOffline,
                FormatMixedCount = fMixed,
                FormatOnlineCount = fOnline,

                EngagedCount = eCount,
                DetachedCount = dCount,

                SentimentJson = JsonSerializer.Serialize(analysisResult.Sentiment),
                ThemesJson = JsonSerializer.Serialize(analysisResult.TopTopics),
                RecommendationsJson = JsonSerializer.Serialize(analysisResult.Conclusions),

                // Сохраняем новый расширенный ответ ии 
                AiInsightsJson = JsonSerializer.Serialize(analysisResult),

                TrendLabelsJson = JsonSerializer.Serialize(trendLabels),
                TrendValuesJson = JsonSerializer.Serialize(trendValues),
                CorrelationMatrixJson = matrixJson,
                ScoresDistributionJson = distJson
            };

            await _repository.AddAsync(analysisRecord);

            return RedirectToAction("Details", "Dashboard", new { id = analysisRecord.Id });
        }

        // Очистка имен файлов
        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "Неизвестная дата";

            var dateMatch = Regex.Match(fileName, @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?\s*[-–—]\s*\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?");

            if (dateMatch.Success)
            {
                return dateMatch.Value.Replace("_", ".").Replace("-", " — ").Replace("–", " — ").Replace("—", " — ");
            }

            var singleDateMatch = Regex.Match(fileName, @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?");
            if (singleDateMatch.Success)
            {
                return singleDateMatch.Value.Replace("_", ".");
            }

            string pattern = @"(?i)(_v\d+|-копия|\(\d+\)|_доп.*|_часть.*|_финал|\s+копия).*$";
            string cleanName = Regex.Replace(fileName, pattern, "");
            return cleanName.Trim(' ', '_', '-');
        }
    }
}