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

            // ==========================================
            // 1. ПАРСИНГ ОСНОВНОГО ФАЙЛА
            // ==========================================

            // ПЕРЕДАЕМ uploadFile.FileName В ПАРСЕР И ПРИНИМАЕМ ДАННЫЕ О ФОРМАТЕ ОБУЧЕНИЯ
            var (progName, listenerCount, numericAvgs, allComments, d1to3, d4to7, d8to10, matrixJson, distJson, fOffline, fMixed, fOnline) = await _excelParserService.ParseAsync(uploadFile.OpenReadStream(), uploadFile.FileName);

            double avgUtility = numericAvgs.GetValueOrDefault("Usefulness", 0);
            double avgPractice = numericAvgs.GetValueOrDefault("Practicality", 0);
            double avgAccessibility = numericAvgs.GetValueOrDefault("Accessibility", 0);
            double avgInteraction = numericAvgs.GetValueOrDefault("Interaction", 0);
            double avgEngagement = numericAvgs.GetValueOrDefault("Engagement", 0);

            // Общая удовлетворенность текущего потока
            double overallSatisfaction = (avgUtility + avgPractice + avgAccessibility + avgInteraction) / 4.0;

            string rawComments = string.Join("\n", allComments);

            // ==========================================
            // 2. ОБРАБОТКА ИСТОРИЧЕСКИХ ФАЙЛОВ (ТРЕНД С ГРУППИРОВКОЙ)
            // ==========================================
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
                        // Вызываем наш быстрый метод (только цифры)
                        double histScore = await _excelParserService.ParseHistoryFileAsync(histStream);

                        // Получаем имя без расширения и отрезаем мусорные приписки
                        string rawName = Path.GetFileNameWithoutExtension(file.FileName);
                        string baseName = CleanFileName(rawName);

                        // Добавляем балл в группу этого потока
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

            // Добавляем результаты текущего (основного) файла в самый конец тренда
            trendLabels.Add("Текущий поток");
            trendValues.Add(Math.Round(overallSatisfaction, 1));

            // ==========================================
            // 3. ПРОМПТЫ И ВЫЗОВ ИИ
            // ==========================================
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

            AiAnalysisResultDto analysisResult = new AiAnalysisResultDto
            {
                Sentiment = new SentimentStats { PositivePercent = 0, NeutralPercent = 0, NegativePercent = 0 },
                TopTopics = new List<Topic>(),
                Conclusions = new List<Conclusion>()
            };

            try
            {
                analysisResult = await _aiService.AnalyzeFeedbackAsync(systemPrompt, userPrompt, providerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Предупреждение: ИИ-анализ пропущен: {ex.Message}");
                TempData["AiWarning"] = "Связь с нейросетью временно недоступна (ошибка API-ключа). Дашборд построен только на основе математических расчетов.";
            }

            // ==========================================
            // 4. СОХРАНЕНИЕ В БАЗУ ДАННЫХ
            // ==========================================
            var analysisRecord = new AnalysisResult
            {
                // ИСПРАВЛЕНИЕ ХАРДКОДА: Используем progName вместо "Анализ анкет КУ"
                SessionName = !string.IsNullOrEmpty(progName) ? progName : "Аналитическая справка",
                ProgramName = progName,
                ListenerCount = listenerCount,
                CreatedAt = DateTime.UtcNow,

                UsefulnessAvg = avgUtility,
                PracticalityAvg = avgPractice,
                AvailabilityAvg = avgAccessibility,
                InteractionAvg = avgInteraction,
                OverallSatisfaction = overallSatisfaction,
                EngagementYesPercent = (int)avgEngagement,

                Dist1to3 = d1to3,
                Dist4to7 = d4to7,
                Dist8to10 = d8to10,

                // ИСПРАВЛЕНИЕ: СОХРАНЯЕМ ФОРМАТЫ ОБУЧЕНИЯ
                FormatOfflineCount = fOffline,
                FormatMixedCount = fMixed,
                FormatOnlineCount = fOnline,

                SentimentJson = JsonSerializer.Serialize(analysisResult.Sentiment),
                ThemesJson = JsonSerializer.Serialize(analysisResult.TopTopics),
                RecommendationsJson = JsonSerializer.Serialize(analysisResult.Conclusions),
                AiInsightsJson = JsonSerializer.Serialize(analysisResult),

                // Сохраняем сериализованные данные тренда
                TrendLabelsJson = JsonSerializer.Serialize(trendLabels),
                TrendValuesJson = JsonSerializer.Serialize(trendValues),

                // СОХРАНЯЕМ МАТРИЦУ КОРРЕЛЯЦИЙ В БД
                CorrelationMatrixJson = matrixJson,

                // ОБНОВЛЕНИЕ: СОХРАНЯЕМ РАСПРЕДЕЛЕНИЕ ОЦЕНОК В БД
                ScoresDistributionJson = distJson
            };

            await _repository.AddAsync(analysisRecord);

            return RedirectToAction("Details", "Dashboard", new { id = analysisRecord.Id });
        }

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Очистка имен файлов
        // ==========================================
        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "Неизвестная дата";

            // Ищем полный диапазон дат в формате ДД.ММ-ДД.ММ или ДД.ММ.ГГГГ-ДД.ММ.ГГГГ
            var dateMatch = Regex.Match(fileName, @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?\s*[-–—]\s*\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?");

            if (dateMatch.Success)
            {
                // Возвращаем найденный диапазон, красиво заменяя разделители на тире с пробелами
                return dateMatch.Value.Replace("_", ".").Replace("-", " — ").Replace("–", " — ").Replace("—", " — ");
            }

            // Если диапазона дат нет, пробуем найти хотя бы одну дату
            var singleDateMatch = Regex.Match(fileName, @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?");
            if (singleDateMatch.Success)
            {
                return singleDateMatch.Value.Replace("_", ".");
            }

            // Если дат вообще нет — чистим от стандартного мусора
            string pattern = @"(?i)(_v\d+|-копия|\(\d+\)|_доп.*|_часть.*|_финал|\s+копия).*$";
            string cleanName = Regex.Replace(fileName, pattern, "");
            return cleanName.Trim(' ', '_', '-');
        }
    }
}