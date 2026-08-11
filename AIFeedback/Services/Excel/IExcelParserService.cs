using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AIFeedback.Services.Excel
{
    // ==========================================
    // 1. ИНТЕРФЕЙС НАПАРНИЦЫ
    // ==========================================
    public interface IExcelParserService
    {
        Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments, int Dist1to3, int Dist4to7, int Dist8to10)> ParseAsync(Stream fileStream);
    }

    // ==========================================
    // 2. ТВОЙ КЛАСС ПАРСЕРА
    // ==========================================
    public class ExcelParserService : IExcelParserService
    {
        private readonly ILogger<ExcelParserService> _logger;

        public ExcelParserService(ILogger<ExcelParserService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments, int Dist1to3, int Dist4to7, int Dist8to10)> ParseAsync(Stream fileStream)
        {
            return await Task.Run(() =>
            {
                var allComments = new List<string>();
                var usefulnessList = new List<int>();
                var practicalityList = new List<int>();
                var accessibilityList = new List<int>();
                var interactionList = new List<int>();

                string programName = "Анализируемая программа";
                int listenerCount = 0;

                // --- ТЕ САМЫЕ СЧЕТЧИКИ ДЛЯ ГРАФИКА ---
                int dist1to3 = 0, dist4to7 = 0, dist8to10 = 0;

                // --- СЧЕТЧИКИ ДЛЯ ВОВЛЕЧЕННОСТИ ---
                int engagedCount = 0;
                int totalEngagementAnswers = 0;

                try
                {
                    using var workbook = new XLWorkbook(fileStream);
                    var worksheet = workbook.Worksheet(1);

                    var headerRow = worksheet.FirstRowUsed();
                    if (headerRow == null) return (programName, 0, new Dictionary<string, double>(), allComments, 0, 0, 0);

                    int usefulnessCol = -1, practicalityCol = -1, accessibilityCol = -1, interactionCol = -1, engagementCol = -1;
                    var textColumns = new List<int>();

                    foreach (var cell in headerRow.CellsUsed())
                    {
                        string header = cell.Value.ToString()?.ToLower() ?? string.Empty;
                        int colIndex = cell.Address.ColumnNumber;

                        if (header.Contains("полезность программы по 10")) usefulnessCol = colIndex;
                        else if (header.Contains("практическую часть по 10")) practicalityCol = colIndex;
                        else if (header.Contains("доступность материала программы по 10")) accessibilityCol = colIndex;
                        else if (header.Contains("взаимодействие по 10")) interactionCol = colIndex;

                        // --- ИЩЕМ КОЛОНКУ С ОТСТРАНЕННОСТЬЮ ---
                        // Используем if, а не else if, чтобы колонка также попала в textColumns и ИИ мог забрать причины отстраненности
                        if (header.Contains("отстраненность")) engagementCol = colIndex;

                        if (!header.Contains("ф.и.о.") && !header.Contains("место вашей работы") && !header.Contains("категории относится"))
                        {
                            textColumns.Add(colIndex);
                        }
                    }

                    var range = worksheet.RangeUsed();
                    if (range == null) return (programName, 0, new Dictionary<string, double>(), allComments, 0, 0, 0);

                    var rows = range.RowsUsed().Skip(1).ToList();
                    listenerCount = rows.Count;

                    foreach (var row in rows)
                    {
                        var userScores = new List<int>();

                        if (usefulnessCol != -1 && TryParseInt(row.Cell(usefulnessCol).Value.ToString(), out int u))
                        { usefulnessList.Add(u); userScores.Add(u); }

                        if (practicalityCol != -1 && TryParseInt(row.Cell(practicalityCol).Value.ToString(), out int p))
                        { practicalityList.Add(p); userScores.Add(p); }

                        if (accessibilityCol != -1 && TryParseInt(row.Cell(accessibilityCol).Value.ToString(), out int a))
                        { accessibilityList.Add(a); userScores.Add(a); }

                        if (interactionCol != -1 && TryParseInt(row.Cell(interactionCol).Value.ToString(), out int i))
                        { interactionList.Add(i); userScores.Add(i); }

                        // --- СЧИТАЕМ ВОВЛЕЧЕННОСТЬ ---
                        if (engagementCol != -1)
                        {
                            var detachmentAnswer = row.Cell(engagementCol).Value.ToString()?.Trim().ToLower();
                            if (!string.IsNullOrWhiteSpace(detachmentAnswer))
                            {
                                totalEngagementAnswers++;
                                // Если ответ содержит "нет" (нет отстраненности), значит человек вовлечен
                                if (detachmentAnswer.Contains("нет")) engagedCount++;
                            }
                        }

                        // --- СЧИТАЕМ РАСПРЕДЕЛЕНИЕ ДЛЯ ДАШБОРДА ---
                        if (userScores.Count > 0)
                        {
                            double avgScore = userScores.Average();
                            int roundedScore = (int)Math.Round(avgScore, MidpointRounding.AwayFromZero);

                            if (roundedScore >= 1 && roundedScore <= 3) dist1to3++;
                            else if (roundedScore >= 4 && roundedScore <= 7) dist4to7++;
                            else if (roundedScore >= 8 && roundedScore <= 10) dist8to10++;
                        }

                        foreach (int colIndex in textColumns)
                        {
                            var comment = row.Cell(colIndex).Value.ToString()?.Trim();
                            if (IsValidComment(comment))
                            {
                                allComments.Add(comment!);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при парсинге Excel файла.");
                }

                var averages = new Dictionary<string, double>
                {
                    { "Usefulness", usefulnessList.Count > 0 ? usefulnessList.Average() : 0 },
                    { "Practicality", practicalityList.Count > 0 ? practicalityList.Average() : 0 },
                    { "Accessibility", accessibilityList.Count > 0 ? accessibilityList.Average() : 0 },
                    { "Interaction", interactionList.Count > 0 ? interactionList.Average() : 0 },
                    // --- ПРОЦЕНТ ВОВЛЕЧЕННОСТИ ---
                    { "Engagement", totalEngagementAnswers > 0 ? Math.Round((double)engagedCount / totalEngagementAnswers * 100) : 0 }
                };

                // Отдаем всё контроллерам
                return (programName, listenerCount, averages, allComments, dist1to3, dist4to7, dist8to10);
            });
        }

        private bool TryParseInt(string? value, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            // Извлекаем первое попавшееся число из строки (справится и с "10.0", и с "8 - средне")
            var match = Regex.Match(value.Trim(), @"\d+");
            if (match.Success && int.TryParse(match.Value, out int val))
            {
                if (val >= 1 && val <= 10)
                {
                    result = val;
                    return true;
                }
            }
            return false;
        }

        private bool IsValidComment(string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment)) return false;

            var lower = comment.ToLower();
            if (lower == "-" || lower == "нет" || lower == "да" || lower.Contains("затрудняюсь") || lower.Length < 3)
                return false;

            return true;
        }
    }
}