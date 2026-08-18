using AIFeedback.Data;
using AIFeedback.Models.DTOs;
using AIFeedback.Services.Report;
using AIFeedback.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
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

        public DashboardController(IAnalysisResultRepository repository, IReportService reportService)
        {
            _repository = repository;
            _reportService = reportService;
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

            ws.Cell("A3").Value = "Общая информация о программе";
            ApplyHeaderStyle(ws.Range("A3:M3"));

            ws.Cell("A4").Value = "Наименование программы";
            ws.Range("B4:M4").Merge().Value = result.ProgramName;
            ws.Cell("A5").Value = "Период обучения";
            ws.Range("B5:M5").Merge().Value = result.CreatedAt.ToString("dd.MM.yyyy");
            ws.Cell("A6").Value = "Форма обучения";
            ws.Range("B6:M6").Merge().Value = "Очная с применением дистанционных образовательных технологий";
            ws.Cell("A7").Value = "Количество слушателей, принявших участие в опросе";
            ws.Range("B7:M7").Merge().Value = $"{result.ListenerCount} слушателей";
            ws.Cell("A8").Value = "Преподаватели программы";
            ws.Range("B8:M8").Merge().Value = "Экспертный состав КУ";

            ws.Range("A4:A8").Style.Font.SetBold();
            ws.Range("A4:M8").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range("A4:M8").Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int r = 10;
            ws.Cell(r, 1).Value = "Ключевые показатели по программе";
            ApplyHeaderStyle(ws.Range(r, 1, r, 13));
            r++;

            ws.Cell(r, 1).Value = "Ключевые показатели";
            ws.Cell(r, 2).Value = "Баллы по шкале/кол-во оценок (1 - 10)";
            ws.Range(r, 2, r, 11).Merge();
            ws.Cell(r, 12).Value = "Средний балл";
            ws.Cell(r, 13).Value = "Примечание";
            ws.Range(r, 1, r, 13).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Range(r, 1, r, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(r, 1, r, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            r++;

            for (int i = 1; i <= 10; i++)
            {
                ws.Cell(r, i + 1).Value = i;
                ws.Cell(r, i + 1).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            }
            ws.Range(r, 1, r, 13).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            r++;

            // ИСПРАВЛЕНИЕ ДЛЯ EXCEL: Десериализация распределения оценок
            Dictionary<string, int[]> distMap = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(result.ScoresDistributionJson))
                {
                    distMap = JsonSerializer.Deserialize<Dictionary<string, int[]>>(result.ScoresDistributionJson);
                }
            }
            catch { }

            AiAnalysisResultDto aiData = null;
            try { aiData = JsonSerializer.Deserialize<AiAnalysisResultDto>(result.AiInsightsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { }

            void DrawCriteriaRow(string name, string key, double avgScore, int rowNum, string keyword)
            {
                ws.Cell(rowNum, 1).Value = name;
                ws.Cell(rowNum, 1).Style.Font.SetBold();

                // Заполняем ячейки 1-10 реальными данными из distMap
                for (int i = 0; i < 10; i++)
                {
                    int val = 0;
                    if (distMap != null && distMap.ContainsKey(key) && distMap[key].Length > i)
                    {
                        val = distMap[key][i];
                    }
                    ws.Cell(rowNum, i + 2).Value = val;
                    ws.Cell(rowNum, i + 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }

                ws.Cell(rowNum, 12).Value = Math.Round(avgScore, 1);
                ws.Cell(rowNum, 12).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                string note = $"Средний балл по критерию: {Math.Round(avgScore, 1)}. Оценка стабильна.";
                var insight = aiData?.Conclusions?.FirstOrDefault(c => (c.Action ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase) || (c.DataProof ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase));
                if (insight != null) note = $"{insight.Action}. {insight.DataProof}";

                ws.Cell(rowNum, 13).Value = note;
                ws.Cell(rowNum, 13).Style.Alignment.SetWrapText(true);
            }

            DrawCriteriaRow("Полезность программы", "Usefulness", result.UsefulnessAvg, r++, "полезн");
            DrawCriteriaRow("Практико-ориентированность", "Practicality", result.PracticalityAvg, r++, "практик");
            DrawCriteriaRow("Доступность материалов", "Accessibility", result.AvailabilityAvg, r++, "доступн");
            DrawCriteriaRow("Взаимодействие с командой КУ", "Interaction", result.InteractionAvg, r++, "взаимодейств");

            ws.Cell(r, 1).Value = "Вовлеченность в образовательный процесс";
            ws.Cell(r, 1).Style.Font.SetBold();
            ws.Cell(r, 2).Value = "Чувствовалась ли отстранённость? (Да / Нет)";
            ws.Range(r, 2, r, 7).Merge();
            ws.Cell(r, 8).Value = "Уровень вовлеченности";
            ws.Range(r, 8, r, 12).Merge();
            ws.Cell(r, 13).Value = $"{result.EngagementYesPercent}% слушателей были полностью вовлечены в процесс обучения.";
            ws.Cell(r, 13).Style.Alignment.SetWrapText(true);
            r++;

            ws.Cell(r, 2).Value = "-"; ws.Range(r, 2, r, 4).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 5).Value = "-"; ws.Range(r, 5, r, 7).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 8).Value = $"{result.EngagementYesPercent}%"; ws.Range(r, 8, r, 12).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            r++;

            ws.Range(10, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(10, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Range(10, 1, r - 1, 13).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);

            r++;
            ws.Cell(r, 1).Value = "Предложения слушателей";
            ApplyHeaderStyle(ws.Range(r, 1, r, 13));
            r++;

            ws.Cell(r, 1).Value = "Темы, которые оказались неактуальны";
            ws.Range(r, 1, r, 6).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 7).Value = "Темы, которыми можно дополнить программу";
            ws.Range(r, 7, r, 13).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            r++;

            string topicsToAdd = "Жалоб и предложений по дополнению тем не зафиксировано.";
            if (aiData?.TopTopics != null && aiData.TopTopics.Any())
            {
                topicsToAdd = string.Join("\n", aiData.TopTopics.Select((t, index) => $"{index + 1}. {t.Name} (упоминаний: {t.MentionsCount})"));
            }

            ws.Cell(r, 1).Value = "Неактуальных тем не выявлено.";
            ws.Range(r, 1, r, 6).Merge().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top).Alignment.SetWrapText(true);
            ws.Cell(r, 7).Value = topicsToAdd;
            ws.Range(r, 7, r, 13).Merge().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top).Alignment.SetWrapText(true);
            ws.Row(r).Height = 65;
            r++;

            ws.Range(r - 3, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(r - 3, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // БЛОК: ПРЕДПОЧТИТЕЛЬНАЯ ФОРМА ОБУЧЕНИЯ (Новое!)
            r++;
            ws.Cell(r, 1).Value = "Предпочтительная форма обучения";
            ApplyHeaderStyle(ws.Range(r, 1, r, 13));
            r++;

            ws.Cell(r, 1).Value = "Очное обучение в аудиториях КУ";
            ws.Range(r, 1, r, 4).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 5).Value = "Смешанное обучение: частично очно, частично дистанционно";
            ws.Range(r, 5, r, 9).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
            ws.Cell(r, 10).Value = "Обучение с применением дистанционных образовательных технологий";
            ws.Range(r, 10, r, 13).Merge().Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetWrapText(true);
            r++;

            ws.Cell(r, 1).Value = "Определяется на основе анкет"; 
            ws.Range(r, 1, r, 4).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 5).Value = "Определяется на основе анкет";
            ws.Range(r, 5, r, 9).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(r, 10).Value = "Определяется на основе анкет";
            ws.Range(r, 10, r, 13).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            r++;

            ws.Range(r - 3, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(r - 3, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // БЛОК: ТРАЕКТОРИЯ
            r++;
            ws.Cell(r, 1).Value = "Траектория изменения программы по результатам итогового опроса слушателей";
            ApplyHeaderStyle(ws.Range(r, 1, r, 13));
            r++;

            void DrawTrajectoryRow(string title, string text)
            {
                ws.Cell(r, 1).Value = title;
                ws.Range(r, 1, r, 4).Merge().Style.Font.SetBold();
                ws.Cell(r, 5).Value = text;
                ws.Range(r, 5, r, 13).Merge().Style.Alignment.SetWrapText(true);
                r++;
            }

            DrawTrajectoryRow("Потребность в дальнейшей реализации программы", $"Высокий уровень полезности ({Math.Round(result.UsefulnessAvg, 1)} из 10), общая удовлетворенность — {Math.Round(result.OverallSatisfaction, 1)}. Программа востребована.");
            DrawTrajectoryRow("Корректировка отбора слушателей", "Не требуется.");

            string recommendations = "Рекомендаций по изменению программы на основе текущих данных нет.";
            if (aiData?.Conclusions != null && aiData.Conclusions.Any())
            {
                recommendations = string.Join("\n", aiData.Conclusions.Select(c => $"• {c.Action} (Основание: {c.DataProof})"));
            }
            DrawTrajectoryRow("Дополнение программы учебными вопросами", recommendations);
            DrawTrajectoryRow("Изменение количества часов в программе", "Не требуется.");
            DrawTrajectoryRow("Изменение формы обучения", "Не требуется.");

            ws.Range(r - 6, 1, r - 1, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(r - 6, 1, r - 1, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Range(r - 6, 1, r - 1, 13).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            string safeFileName = string.IsNullOrWhiteSpace(result.ProgramName) ? "Analytics" : result.ProgramName.Replace(" ", "_");
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Аналитическая_справка_{safeFileName}.xlsx");
        }

        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> DownloadWord(int id)
        {
            try
            {
                var result = await _repository.GetByIdAsync(id);
                if (result == null) return NotFound("Отчет не найден");

                var aiDto = new AiAnalysisResultDto
                {
                    Sentiment = JsonSerializer.Deserialize<SentimentStats>(
                        string.IsNullOrWhiteSpace(result.SentimentJson) ? "{}" : result.SentimentJson) ?? new SentimentStats(),
                    TopTopics = JsonSerializer.Deserialize<List<AIFeedback.Models.DTOs.Topic>>(
                        string.IsNullOrWhiteSpace(result.ThemesJson) ? "[]" : result.ThemesJson) ?? new List<AIFeedback.Models.DTOs.Topic>(),
                    Conclusions = JsonSerializer.Deserialize<List<AIFeedback.Models.DTOs.Conclusion>>(
                        string.IsNullOrWhiteSpace(result.RecommendationsJson) ? "[]" : result.RecommendationsJson) ?? new List<AIFeedback.Models.DTOs.Conclusion>()
                };

                // ИСПРАВЛЕНИЕ ДЛЯ WORD: Десериализация и передача ScoresDistribution
                Dictionary<string, int[]> distMap = null;
                try
                {
                    distMap = JsonSerializer.Deserialize<Dictionary<string, int[]>>(
                        string.IsNullOrWhiteSpace(result.ScoresDistributionJson) ? "{}" : result.ScoresDistributionJson);
                }
                catch { }

                var wordData = new WordReportData
                {
                    ProgramName = result.ProgramName ?? "Без_названия",
                    ListenerCount = result.ListenerCount,
                    CreatedAt = result.CreatedAt,
                    UsefulnessAvg = result.UsefulnessAvg,
                    PracticalityAvg = result.PracticalityAvg,
                    AvailabilityAvg = result.AvailabilityAvg,
                    InteractionAvg = result.InteractionAvg,
                    EngagementYesPercent = result.EngagementYesPercent,
                    OverallSatisfaction = result.OverallSatisfaction,
                    AiAnalysis = aiDto,
                    ScoresDistribution = distMap ?? new Dictionary<string, int[]>()
                };

                Stream reportStream = await _reportService.GenerateWordReportAsync(wordData);
                string safeFileName = string.IsNullOrWhiteSpace(result.ProgramName) ? "Analytics" : result.ProgramName.Replace(" ", "_");

                return File(
                    reportStream,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"Аналитическая_справка_{safeFileName}.docx"
                );
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
                Id = result.Id,
                ProgramName = result.ProgramName,
                ListenerCount = result.ListenerCount,
                CreatedAt = result.CreatedAt,
                UsefulnessAvg = result.UsefulnessAvg,
                AvailabilityAvg = result.AvailabilityAvg,
                PracticalityAvg = result.PracticalityAvg,
                InteractionAvg = result.InteractionAvg,
                EngagementYesPercent = result.EngagementYesPercent,
                OverallSatisfaction = result.OverallSatisfaction,
                CorrelationMatrixJson = result.CorrelationMatrixJson,
                Dist1to3 = result.Dist1to3,
                Dist4to7 = result.Dist4to7,
                Dist8to10 = result.Dist8to10,

                TrendLabels = !string.IsNullOrEmpty(result.TrendLabelsJson)
                    ? JsonSerializer.Deserialize<List<string>>(result.TrendLabelsJson) : new List<string>(),
                TrendValues = !string.IsNullOrEmpty(result.TrendValuesJson)
                    ? JsonSerializer.Deserialize<List<double>>(result.TrendValuesJson) : new List<double>(),

                AiAnalysis = new AiAnalysisResultDto
                {
                    Sentiment = JsonSerializer.Deserialize<SentimentStats>(result.SentimentJson ?? "{}") ?? new SentimentStats(),
                    TopTopics = JsonSerializer.Deserialize<List<AIFeedback.Models.DTOs.Topic>>(result.ThemesJson ?? "[]") ?? new List<AIFeedback.Models.DTOs.Topic>(),
                    Conclusions = JsonSerializer.Deserialize<List<AIFeedback.Models.DTOs.Conclusion>>(result.RecommendationsJson ?? "[]") ?? new List<AIFeedback.Models.DTOs.Conclusion>()
                }
            };
            return View(viewModel);
        }
    }
}