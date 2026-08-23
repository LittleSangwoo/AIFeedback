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
        private const int MaxCommentsCharsForAi = 20000;
        [HttpPost]
        [RequestSizeLimit(104857600)] // Увеличиваем лимит запроса до 100 МБ для мульти-загрузки
        public async Task<IActionResult> ProcessFile(IFormFile uploadFile, List<IFormFile> historyFiles, bool isTrendEnabled, string providerName)
        {
            if (uploadFile == null || uploadFile.Length == 0)
            {
                return BadRequest("Основной файл не выбран");
            }

            // ==========================================
            // 1. ПАРСИНГ ОСНОВНОГО ФАЙЛА
            // ==========================================
            var parsed = await _excelParserService.ParseAsync(uploadFile.OpenReadStream(), uploadFile.FileName);

            if (!parsed.ParseSuccess)
            {
                TempData["ExcelWarning"] = "Не удалось корректно распознать структуру файла: возможно, заголовки столбцов " +
                    "отличаются от ожидаемого шаблона (шкалы «по 10», вопрос об отстранённости, вопрос о формате обучения), " +
                    "либо файл пуст. Показатели ниже могут быть неполными или равны нулю — рекомендуем сверить формат файла " +
                    "с образцом и загрузить его повторно.";
            }

            if (parsed.DuplicateRowsRemoved > 0)
            {
                TempData["DuplicateInfo"] = $"При разборе файла обнаружено и исключено из расчётов {parsed.DuplicateRowsRemoved} " +
                    $"дублирующихся анкет — они не повлияли на итоговую статистику.";
            }

            double avgUtility = parsed.NumericAverages.GetValueOrDefault("Usefulness", 0);
            double avgPractice = parsed.NumericAverages.GetValueOrDefault("Practicality", 0);
            double avgAccessibility = parsed.NumericAverages.GetValueOrDefault("Accessibility", 0);
            double avgInteraction = parsed.NumericAverages.GetValueOrDefault("Interaction", 0);

            // Считаем точный процент вовлеченности
            int totalE = parsed.EngagedCount + parsed.DetachedCount;
            int engPct = totalE > 0 ? (int)Math.Round((double)parsed.EngagedCount / totalE * 100) : 0;

            // Общая удовлетворенность текущего потока
            double overallSatisfaction = (avgUtility + avgPractice + avgAccessibility + avgInteraction) / 4.0;

            string rawComments = BuildSampledCommentsText(parsed.AllComments, MaxCommentsCharsForAi);

            // ==========================================
            // 2. ОБРАБОТКА ИСТОРИЧЕСКИХ ФАЙЛОВ (ТРЕНД С ГРУППИРОВКОЙ)
            // ==========================================
            var trendLabels = new List<string>();
            var trendValues = new List<double>();

            if (isTrendEnabled && historyFiles != null && historyFiles.Count > 0)
            {
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

                var historyData = groupedHistory
                    .Select(kvp => (Name: kvp.Key, Score: kvp.Value.Average()))
                    .OrderBy(x => x.Name)
                    .ToList();

                trendLabels.AddRange(historyData.Select(x => x.Name));
                trendValues.AddRange(historyData.Select(x => x.Score));
            }

            trendLabels.Add("Текущий поток");
            trendValues.Add(Math.Round(overallSatisfaction, 1));

            // ==========================================
            // 3. ПРОМПТЫ И ВЫЗОВ ИИ
            // ==========================================
            string systemPrompt = @"Ты — профессиональный AI-аналитик образовательных программ. Твоя задача проанализировать сырые отзывы и метрики, и выдать детальный профессиональный отчет СТРОГО в формате JSON.
Не используй markdown (никаких ```json). 
Опирайся на реальные цифры и комментарии. Сформируй от 3 до 7 объектов в разделе Conclusions (не меньше 3 и не больше 7), 
отсортированных по важности (High — сначала). Для каждого вывода в SupportingQuotes укажи 1–3 ДОСЛОВНЫЕ цитаты 
из предоставленных комментариев слушателей — не придумывай и не перефразируй цитаты, бери только реально 
встречающиеся в тексте формулировки. MentionsCount — примерное количество похожих по смыслу комментариев.
Структура JSON должна быть РОВНО такой:

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
      ""DataProof"": ""Обоснование на основе цифр"",
     ""SupportingQuotes"": [
      { ""Text"": ""Дословная цитата из комментария слушателя"", ""MentionsCount"": 3 }
     ]
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

            //if (rawComments.Length > 10000)
            //{
            //    rawComments = rawComments.Substring(0, 10000) + "... [ДАННЫЕ ОБРЕЗАНЫ ИЗ-ЗА ЛИМИТОВ]";
            //}

            string userPrompt = $"Слушателей: {parsed.ListenerCount}. Средние баллы: Полезность {avgUtility}, Практика {avgPractice}, Доступность {avgAccessibility}, Взаимодействие {avgInteraction}. Вовлечены: {parsed.EngagedCount} чел ({engPct}%), Отстранены: {parsed.DetachedCount} чел.\nТекстовые отзывы:\n{rawComments}";

            AiAnalysisResultDto analysisResult = new AiAnalysisResultDto();

            try
            {
                analysisResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, providerName);
                if (analysisResult.Conclusions != null && analysisResult.Conclusions.Count > 7)
               {
                   int Weight(string priority) => (priority ?? "").ToLower() switch
                   {
                       "high" or "высокий" => 3,
                       "medium" or "средний" => 2,
                       "low" or "низкий" => 1,
                       _ => 0
                   };

                   analysisResult.Conclusions = analysisResult.Conclusions
                       .OrderByDescending(c => Weight(c.Priority))
                       .Take(7)
                       .ToList();
               }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Предупреждение: ИИ-анализ пропущен: {ex.Message}");
                TempData["AiWarning"] = "Связь с нейросетью временно недоступна. Дашборд построен только на основе расчетов.";
            }

            // ==========================================
            // 4. СОХРАНЕНИЕ В БАЗУ ДАННЫХ
            // ==========================================
            var analysisRecord = new AnalysisResult
            {
                SessionName = !string.IsNullOrEmpty(parsed.ProgramName) ? parsed.ProgramName : "Аналитическая справка",
                ProgramName = parsed.ProgramName,
                ListenerCount = parsed.ListenerCount,
                CreatedAt = DateTime.UtcNow,

                UsefulnessAvg = avgUtility,
                PracticalityAvg = avgPractice,
                AvailabilityAvg = avgAccessibility,
                InteractionAvg = avgInteraction,
                OverallSatisfaction = overallSatisfaction,
                EngagementYesPercent = engPct,

                UsefulnessMedian = parsed.NumericMedians.GetValueOrDefault("Usefulness", 0),
                PracticalityMedian = parsed.NumericMedians.GetValueOrDefault("Practicality", 0),
                AvailabilityMedian = parsed.NumericMedians.GetValueOrDefault("Accessibility", 0),
                InteractionMedian = parsed.NumericMedians.GetValueOrDefault("Interaction", 0),

                UsefulnessStdDev = parsed.NumericStdDeviations.GetValueOrDefault("Usefulness", 0),
                PracticalityStdDev = parsed.NumericStdDeviations.GetValueOrDefault("Practicality", 0),
                AvailabilityStdDev = parsed.NumericStdDeviations.GetValueOrDefault("Accessibility", 0),
                InteractionStdDev = parsed.NumericStdDeviations.GetValueOrDefault("Interaction", 0),

                DuplicateRowsRemoved = parsed.DuplicateRowsRemoved,

                Dist1to3 = parsed.Dist1to3,
                Dist4to7 = parsed.Dist4to7,
                Dist8to10 = parsed.Dist8to10,

                FormatOfflineCount = parsed.FormatOffline,
                FormatMixedCount = parsed.FormatMixed,
                FormatOnlineCount = parsed.FormatOnline,

                EngagedCount = parsed.EngagedCount,
                DetachedCount = parsed.DetachedCount,

                SentimentJson = JsonSerializer.Serialize(analysisResult.Sentiment),
                ThemesJson = JsonSerializer.Serialize(analysisResult.TopTopics),
                RecommendationsJson = JsonSerializer.Serialize(analysisResult.Conclusions),

                AiInsightsJson = JsonSerializer.Serialize(analysisResult),

                TrendLabelsJson = JsonSerializer.Serialize(trendLabels),
                TrendValuesJson = JsonSerializer.Serialize(trendValues),
                CorrelationMatrixJson = parsed.CorrelationMatrixJson,
                ScoresDistributionJson = parsed.ScoresDistributionJson
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
        private string BuildSampledCommentsText(List<string> comments, int maxChars)
        {
            if (comments == null || comments.Count == 0) return string.Empty;

            string joinedAll = string.Join("\n", comments);
            if (joinedAll.Length <= maxChars) return joinedAll;

            // Комментариев слишком много для одного запроса к ИИ. Вместо обрезки хвоста
            // (что теряет данные последних анкет) берём равномерную выборку по всему списку —
            // итоговый текст отражает весь поток, а не только начало файла.
            double avgLen = Math.Max(1.0, (double)joinedAll.Length / comments.Count);
            int approxCount = Math.Max(1, (int)(maxChars / avgLen));
            double stride = (double)comments.Count / approxCount;

            var sb = new System.Text.StringBuilder();
            var usedIndexes = new HashSet<int>();

            for (double idx = 0; idx < comments.Count; idx += stride)
            {
                int i = (int)idx;
                if (!usedIndexes.Add(i)) continue;
                if (sb.Length >= maxChars) break;
                sb.AppendLine(comments[i]);
            }

            sb.Append($"\n... [Показана репрезентативная выборка из {usedIndexes.Count} из {comments.Count} комментариев из-за ограничения размера запроса к ИИ-провайдеру]");
            return sb.ToString();
        }
    }
}