using AIFeedback.Data;
using AIFeedback.Models.DTOs;
using AIFeedback.Services;
using AIFeedback.Services.Report;
using AIFeedback.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIFeedback.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IAnalysisResultRepository _repository;
        private readonly IReportService _reportService;
        private readonly ApplicationDbContext _context;
        private readonly IAiService _aiService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IAnalysisResultRepository repository, IReportService reportService, ApplicationDbContext context, IAiService aiService, ILogger<DashboardController> logger)
        {
            _repository = repository;
            _reportService = reportService;
            _context = context;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var results = await _repository.GetAllAsync();
            var viewModels = results.Select(r => new DashboardViewModel
            {
                Id = r.Id,
                ProgramName = r.ProgramName,
                ListenerCount = r.ListenerCount,
                CreatedAt = r.CreatedAt,
                UsefulnessAvg = r.UsefulnessAvg,
                AvailabilityAvg = r.AvailabilityAvg,
                PracticalityAvg = r.PracticalityAvg,
                InteractionAvg = r.InteractionAvg,
                EngagementYesPercent = r.EngagementYesPercent,
                OverallSatisfaction = r.OverallSatisfaction
            }).ToList();
            return View(viewModels);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadExcel(int id)
        {
            var result = await _repository.GetByIdAsync(id);
            if (result == null) return NotFound("Отчет не найден");

            

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Аналитическая справка");

            ws.Column("A").Width = 42;
            for (int i = 2; i <= 11; i++) ws.Column(i).Width = 4;
            ws.Column("L").Width = 14;
            ws.Column("M").Width = 55;

            ws.Cell("A1").Value = "АНАЛИТИЧЕСКАЯ СПРАВКА ПО ИТОГАМ РЕАЛИЗАЦИИ ПРОГРАММЫ ПОВЫШЕНИЯ КВАЛИФИКАЦИИ";
            ws.Range("A1:M1").Merge().Style.Font.SetBold().Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            void ApplyHeaderStyle(IXLRange range)
            {
                range.Merge();
                range.Style.Font.SetBold();
                range.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E7E6E6"));
                range.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                range.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // Определяем доминирующую форму обучения
            string dominantFormat = "Определяется на основе анкет";
            int maxFormat = Math.Max(result.FormatOfflineCount, Math.Max(result.FormatMixedCount, result.FormatOnlineCount));
            if (maxFormat > 0)
            {
                if (maxFormat == result.FormatOfflineCount) dominantFormat = "Очная в аудиториях КУ";
                else if (maxFormat == result.FormatMixedCount) dominantFormat = "Смешанное обучение (очно и дистанционно)";
                else dominantFormat = "С применением дистанционных образовательных технологий";
            }

            // Достаем период обучения из имени файла
            string period = "Не указан";
            var dateMatch = System.Text.RegularExpressions.Regex.Match(result.ProgramName ?? "", @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?\s*[-–—]\s*\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?");
            if (dateMatch.Success) period = dateMatch.Value;
            else { var sm = System.Text.RegularExpressions.Regex.Match(result.ProgramName ?? "", @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?"); if (sm.Success) period = sm.Value; }

            ws.Cell("A3").Value = "Общая информация о программе";
            ApplyHeaderStyle(ws.Range("A3:M3"));

            ws.Cell("A4").Value = "Наименование программы"; ws.Range("B4:M4").Merge().Value = result.ProgramName;
            ws.Cell("A5").Value = "Период обучения"; ws.Range("B5:M5").Merge().Value = period;
            ws.Cell("A6").Value = "Форма обучения"; ws.Range("B6:M6").Merge().Value = dominantFormat;
            ws.Cell("A7").Value = "Количество слушателей, принявших участие в опросе"; ws.Range("B7:M7").Merge().Value = $"{result.ListenerCount} слушателей";
            ws.Cell("A8").Value = "Преподаватели программы"; ws.Range("B8:M8").Merge().Value = "Экспертный состав КУ";

            ws.Range("A4:A8").Style.Font.SetBold();
            ws.Range("A4:M8").Style.Border.OutsideBorder = XLBorderStyleValues.Thin; ws.Range("A4:M8").Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int r = 10;
            ws.Cell(r, 1).Value = "Ключевые показатели по программе"; ApplyHeaderStyle(ws.Range(r, 1, r, 13)); r++;
            ws.Cell(r, 1).Value = "Ключевые показатели"; ws.Cell(r, 2).Value = "Баллы по шкале/кол-во оценок (1 - 10)"; ws.Range(r, 2, r, 11).Merge();
            ws.Cell(r, 12).Value = "Средний балл"; ws.Cell(r, 13).Value = "Примечание";
            ws.Range(r, 1, r, 13).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Range(r, 1, r, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; ws.Range(r, 1, r, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin; r++;

            for (int i = 1; i <= 10; i++) { ws.Cell(r, i + 1).Value = i; ws.Cell(r, i + 1).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center); }
            ws.Range(r, 1, r, 13).Style.Border.BottomBorder = XLBorderStyleValues.Thin; r++;

            Dictionary<string, int[]> distMap = null;
            try { if (!string.IsNullOrWhiteSpace(result.ScoresDistributionJson)) distMap = JsonSerializer.Deserialize<Dictionary<string, int[]>>(result.ScoresDistributionJson); } catch { }

            // Десериализуем расширенную модель ИИ
            AiAnalysisResultDto aiData = new AiAnalysisResultDto();
            try { aiData = JsonSerializer.Deserialize<AiAnalysisResultDto>(string.IsNullOrWhiteSpace(result.AiInsightsJson) ? "{}" : result.AiInsightsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { }

            void DrawCriteriaRow(string name, string key, double avgScore, int rowNum, string aiNote)
            {
                ws.Cell(rowNum, 1).Value = name; ws.Cell(rowNum, 1).Style.Font.SetBold();
                for (int i = 0; i < 10; i++) 
                { 
                    int val = 0; 
                    if (distMap != null && distMap.ContainsKey(key) && distMap[key].Length > i) val = distMap[key][i]; 
                    ws.Cell(rowNum, i + 2).Value = val == 0 ? "" : val.ToString(); 
                    ws.Cell(rowNum, i + 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center); 
                }
                ws.Cell(rowNum, 12).Value = Math.Round(avgScore, 1); ws.Cell(rowNum, 12).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Cell(rowNum, 13).Value = aiNote ?? "Оценка стабильна. Замечаний не выявлено."; ws.Cell(rowNum, 13).Style.Alignment.SetWrapText(true);
            }

            DrawCriteriaRow("Полезность программы", "Usefulness", result.UsefulnessAvg, r++, aiData?.MetricsNotes?.Usefulness);
            DrawCriteriaRow("Практико-ориентированность", "Practicality", result.PracticalityAvg, r++, aiData?.MetricsNotes?.Practicality);
            DrawCriteriaRow("Доступность материалов", "Accessibility", result.AvailabilityAvg, r++, aiData?.MetricsNotes?.Accessibility);
            DrawCriteriaRow("Взаимодействие с командой КУ", "Interaction", result.InteractionAvg, r++, aiData?.MetricsNotes?.Interaction);

            ws.Cell(r, 1).Value = "Вовлеченность в образовательный процесс"; ws.Cell(r, 1).Style.Font.SetBold();
            ws.Cell(r, 2).Value = "Чувствовалась ли отстранённость? (Да / Нет)"; ws.Range(r, 2, r, 7).Merge();
            ws.Cell(r, 8).Value = "Уровень вовлеченности"; ws.Range(r, 8, r, 12).Merge();
            ws.Cell(r, 13).Value = aiData?.MetricsNotes?.Engagement ?? ""; ws.Cell(r, 13).Style.Alignment.SetWrapText(true); r++;

            ws.Cell(r, 2).Value = "-"; ws.Range(r, 2, r, 4).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 5).Value = "-"; ws.Range(r, 5, r, 7).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            
            int totalEng = result.EngagedCount + result.DetachedCount;
            string engPercent = totalEng > 0 ? $"{Math.Round((double)result.EngagedCount / totalEng * 100)}%" : "Н/Д";
            ws.Cell(r, 8).Value = engPercent; ws.Range(r, 8, r, 12).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center); r++;

            ws.Range(10, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; ws.Range(10, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin; ws.Range(10, 1, r - 1, 13).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top); r++;

            ws.Cell(r, 1).Value = "Дополнительная статистика по критериям"; ApplyHeaderStyle(ws.Range(r, 1, r, 13)); r++;

            ws.Cell(r, 1).Value = "Критерий"; ws.Range(r, 1, r, 4).Merge();
            ws.Cell(r, 5).Value = "Среднее"; ws.Range(r, 5, r, 7).Merge();
            ws.Cell(r, 8).Value = "Медиана"; ws.Range(r, 8, r, 10).Merge();
            ws.Cell(r, 11).Value = "Стандартное отклонение"; ws.Range(r, 11, r, 13).Merge();
            ws.Range(r, 1, r, 13).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Range(r, 1, r, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; ws.Range(r, 1, r, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin; r++;

            int statsStartRow = r;
            void DrawStatsRow(string name, double avg, double median, double stdDev)
            {
                ws.Cell(r, 1).Value = name; ws.Range(r, 1, r, 4).Merge();
                ws.Cell(r, 5).Value = Math.Round(avg, 1); ws.Range(r, 5, r, 7).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Cell(r, 8).Value = Math.Round(median, 1); ws.Range(r, 8, r, 10).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Cell(r, 11).Value = Math.Round(stdDev, 1); ws.Range(r, 11, r, 13).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                r++;
            }

            DrawStatsRow("Полезность программы", result.UsefulnessAvg, result.UsefulnessMedian, result.UsefulnessStdDev);
            DrawStatsRow("Практико-ориентированность", result.PracticalityAvg, result.PracticalityMedian, result.PracticalityStdDev);
            DrawStatsRow("Доступность материалов", result.AvailabilityAvg, result.AvailabilityMedian, result.AvailabilityStdDev);
            DrawStatsRow("Взаимодействие с командой КУ", result.InteractionAvg, result.InteractionMedian, result.InteractionStdDev);

            ws.Range(statsStartRow, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(statsStartRow, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            r++;

            if (result.DuplicateRowsRemoved > 0)
            {
                ws.Cell(r, 1).Value = $"Примечание: при разборе исходного файла обнаружено и исключено из расчётов {result.DuplicateRowsRemoved} дублирующихся анкет.";
                ws.Range(r, 1, r, 13).Merge().Style.Font.SetItalic().Font.FontColor = XLColor.FromHtml("#6c757d");
                r++;
            }
            r++;

            ws.Cell(r, 1).Value = "Предложения слушателей"; ApplyHeaderStyle(ws.Range(r, 1, r, 13)); r++;
            ws.Cell(r, 1).Value = "Темы, которые оказались неактуальны"; ws.Range(r, 1, r, 6).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 7).Value = "Темы, которыми можно дополнить программу"; ws.Range(r, 7, r, 13).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center); r++;

            ws.Cell(r, 1).Value = aiData?.UnnecessaryTopics ?? "Неактуальных тем не выявлено."; ws.Range(r, 1, r, 6).Merge().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top).Alignment.SetWrapText(true);
            ws.Cell(r, 7).Value = aiData?.TopicsToAdd ?? "Дополнений не зафиксировано."; ws.Range(r, 7, r, 13).Merge().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top).Alignment.SetWrapText(true); ws.Row(r).Height = 65; r++;

            ws.Range(r - 3, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; ws.Range(r - 3, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin; r++;

            ws.Cell(r, 1).Value = "Предпочтительная форма обучения"; ApplyHeaderStyle(ws.Range(r, 1, r, 13)); r++;
            ws.Cell(r, 1).Value = "Очное обучение в аудиториях КУ"; ws.Range(r, 1, r, 4).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 5).Value = "Смешанное обучение: частично очно, частично дистанционно"; ws.Range(r, 5, r, 9).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
            ws.Cell(r, 10).Value = "Обучение с применением дистанционных образовательных технологий"; ws.Range(r, 10, r, 13).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true); r++;

            int totalFormatVotes = result.FormatOfflineCount + result.FormatMixedCount + result.FormatOnlineCount;
            string offlineText = totalFormatVotes > 0 ? $"{Math.Round((double)result.FormatOfflineCount / totalFormatVotes * 100)}% ({result.FormatOfflineCount} чел.)" : "Н/Д";
            string mixedText = totalFormatVotes > 0 ? $"{Math.Round((double)result.FormatMixedCount / totalFormatVotes * 100)}% ({result.FormatMixedCount} чел.)" : "Н/Д";
            string onlineText = totalFormatVotes > 0 ? $"{Math.Round((double)result.FormatOnlineCount / totalFormatVotes * 100)}% ({result.FormatOnlineCount} чел.)" : "Н/Д";

            ws.Cell(r, 1).Value = offlineText; ws.Range(r, 1, r, 4).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Fill.SetBackgroundColor(XLColor.FromHtml("#D9EAD3"));
            ws.Cell(r, 5).Value = mixedText; ws.Range(r, 5, r, 9).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Fill.SetBackgroundColor(XLColor.FromHtml("#FFF2CC"));
            ws.Cell(r, 10).Value = onlineText; ws.Range(r, 10, r, 13).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Fill.SetBackgroundColor(XLColor.FromHtml("#F4CCCC")); r++;
            ws.Range(r - 3, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; ws.Range(r - 3, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin; r++;

            ws.Cell(r, 1).Value = "Траектория изменения программы по результатам итогового опроса слушателей"; ApplyHeaderStyle(ws.Range(r, 1, r, 13)); r++;

            void DrawTrajectoryRow(string title, string text) { ws.Cell(r, 1).Value = title; ws.Range(r, 1, r, 4).Merge().Style.Font.SetBold(); ws.Cell(r, 5).Value = text; ws.Range(r, 5, r, 13).Merge().Style.Alignment.SetWrapText(true); r++; }
            
            DrawTrajectoryRow("Потребность в дальнейшей реализации программы", aiData?.Trajectory?.Relevance ?? "");
            DrawTrajectoryRow("Корректировка отбора слушателей", aiData?.Trajectory?.Selection ?? "");
            DrawTrajectoryRow("Дополнение программы учебными вопросами", aiData?.Trajectory?.Additions ?? "");
            DrawTrajectoryRow("Изменение количества часов в программе", aiData?.Trajectory?.Hours ?? "");
            DrawTrajectoryRow("Изменение формы обучения", aiData?.Trajectory?.Format ?? "");

            ws.Range(r - 6, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; ws.Range(r - 6, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin; ws.Range(r - 6, 1, r - 1, 13).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            string safeFileName = string.IsNullOrWhiteSpace(result.ProgramName) ? "Analytics" : result.ProgramName.Replace(" ", "_");
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Аналитическая_справка_{safeFileName}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> DownloadWord(int id)
        {
            try
            {
                var result = await _repository.GetByIdAsync(id);
                if (result == null) return NotFound("Отчет не найден");

                var aiDto = JsonSerializer.Deserialize<AiAnalysisResultDto>(
                    string.IsNullOrWhiteSpace(result.AiInsightsJson) ? "{}" : result.AiInsightsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AiAnalysisResultDto();

                Dictionary<string, int[]> distMap = null;
                try { distMap = JsonSerializer.Deserialize<Dictionary<string, int[]>>(string.IsNullOrWhiteSpace(result.ScoresDistributionJson) ? "{}" : result.ScoresDistributionJson); } catch { }

                var wordData = new WordReportData
                {
                    ProgramName = result.ProgramName ?? "Без_названия", ListenerCount = result.ListenerCount, CreatedAt = result.CreatedAt,
                    UsefulnessAvg = result.UsefulnessAvg, PracticalityAvg = result.PracticalityAvg, AvailabilityAvg = result.AvailabilityAvg, InteractionAvg = result.InteractionAvg,
                    EngagementYesPercent = result.EngagementYesPercent, OverallSatisfaction = result.OverallSatisfaction,
                    UsefulnessMedian = result.UsefulnessMedian,
                    PracticalityMedian = result.PracticalityMedian,
                    AvailabilityMedian = result.AvailabilityMedian,
                    InteractionMedian = result.InteractionMedian,
                    UsefulnessStdDev = result.UsefulnessStdDev,
                    PracticalityStdDev = result.PracticalityStdDev,
                    AvailabilityStdDev = result.AvailabilityStdDev,
                    InteractionStdDev = result.InteractionStdDev,
                    DuplicateRowsRemoved = result.DuplicateRowsRemoved,
                    ScoresDistribution = distMap ?? new Dictionary<string, int[]>(),
                    FormatOfflineCount = result.FormatOfflineCount, FormatMixedCount = result.FormatMixedCount, FormatOnlineCount = result.FormatOnlineCount,
                    EngagedCount = result.EngagedCount, DetachedCount = result.DetachedCount, 
                    AiAnalysis = aiDto // Передаем все распарсенные текстовые данные
                };

                Stream reportStream = await _reportService.GenerateWordReportAsync(wordData);
                string safeFileName = string.IsNullOrWhiteSpace(result.ProgramName) ? "Analytics" : result.ProgramName.Replace(" ", "_");

                return File(reportStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Аналитическая_справка_{safeFileName}.docx");
            }
            catch (Exception ex)
            {
                return Content($"Критическая ошибка при генерации Word: {ex.Message} \n {ex.StackTrace}");
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var result = await _repository.GetByIdAsync(id);
            if (result == null) return NotFound();

            var viewModel = new DashboardViewModel
            {
                Id = result.Id, ProgramName = result.ProgramName, ListenerCount = result.ListenerCount, CreatedAt = result.CreatedAt,
                UsefulnessAvg = result.UsefulnessAvg, AvailabilityAvg = result.AvailabilityAvg, PracticalityAvg = result.PracticalityAvg, InteractionAvg = result.InteractionAvg,
                EngagementYesPercent = result.EngagementYesPercent, OverallSatisfaction = result.OverallSatisfaction, CorrelationMatrixJson = result.CorrelationMatrixJson,
                Dist1to3 = result.Dist1to3, Dist4to7 = result.Dist4to7, Dist8to10 = result.Dist8to10,
                UsefulnessMedian = result.UsefulnessMedian,
                PracticalityMedian = result.PracticalityMedian,
                AvailabilityMedian = result.AvailabilityMedian,
                InteractionMedian = result.InteractionMedian,
                UsefulnessStdDev = result.UsefulnessStdDev,
                PracticalityStdDev = result.PracticalityStdDev,
                AvailabilityStdDev = result.AvailabilityStdDev,
                InteractionStdDev = result.InteractionStdDev,
                DuplicateRowsRemoved = result.DuplicateRowsRemoved,
                TrendLabels = !string.IsNullOrEmpty(result.TrendLabelsJson) ? JsonSerializer.Deserialize<List<string>>(result.TrendLabelsJson) : new List<string>(),
                TrendValues = !string.IsNullOrEmpty(result.TrendValuesJson) ? JsonSerializer.Deserialize<List<double>>(result.TrendValuesJson) : new List<double>(),
                AiAnalysis = JsonSerializer.Deserialize<AiAnalysisResultDto>(result.AiInsightsJson ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AiAnalysisResultDto()
            };
            return View(viewModel);
        }

        public class ChatRequestDto
        {
            public int ResultId { get; set; }
            public string Message { get; set; }
            public string ProviderName { get; set; } // поле для провайдера
        }

        [HttpPost]
        public async Task<IActionResult> AskAi([FromBody] ChatRequestDto request)
        {
            try
            {
                var result = await _context.AnalysisResults.FindAsync(request.ResultId);
                if (result == null) return NotFound(new { error = "Отчет не найден" });

                _logger.LogInformation("AskAi: ResultId={ResultId}, ProviderName из БД='{ProviderName}'", request.ResultId, result.ProviderName ?? "(null)");

                // Передаем ProviderName третьим параметром
                string answer = await _aiService.AskQuestionAsync(result.AiInsightsJson, request.Message, result.ProviderName);
                return Ok(new { answer });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}