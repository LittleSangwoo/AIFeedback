using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text.Json; // Добавлено для JSON

namespace AIFeedback.Services.Excel
{
    public interface IExcelParserService
    {
        // Измененная сигнатура: теперь возвращает также CorrelationMatrixJson
        Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments, int Dist1to3, int Dist4to7, int Dist8to10, string CorrelationMatrixJson)> ParseAsync(Stream fileStream);

        Task<double> ParseHistoryFileAsync(Stream fileStream);
    }

    public class ExcelParserService : IExcelParserService
    {
        private readonly ILogger<ExcelParserService> _logger;

        public ExcelParserService(ILogger<ExcelParserService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments, int Dist1to3, int Dist4to7, int Dist8to10, string CorrelationMatrixJson)> ParseAsync(Stream fileStream)
        {
            return await Task.Run(() =>
            {
                var allComments = new List<string>();

                // Списки для сырых оценок (нужны для подсчета средних И корреляции)
                var usefulnessList = new List<double>();
                var practicalityList = new List<double>();
                var accessibilityList = new List<double>();
                var interactionList = new List<double>();

                string programName = "Анализируемая программа";
                int listenerCount = 0;

                int dist1to3 = 0, dist4to7 = 0, dist8to10 = 0;
                int engagedCount = 0;
                int totalEngagementAnswers = 0;

                try
                {
                    using var workbook = new XLWorkbook(fileStream);
                    var worksheet = workbook.Worksheet(1);

                    var headerRow = worksheet.FirstRowUsed();
                    if (headerRow == null) return (programName, 0, new Dictionary<string, double>(), allComments, 0, 0, 0, "null");

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
                        else if (header.Contains("отстраненность")) engagementCol = colIndex;

                        if (!header.Contains("ф.и.о.") && !header.Contains("место вашей работы") && !header.Contains("категории относится"))
                        {
                            textColumns.Add(colIndex);
                        }
                    }

                    var range = worksheet.RangeUsed();
                    if (range == null) return (programName, 0, new Dictionary<string, double>(), allComments, 0, 0, 0, "null");

                    var rows = range.RowsUsed().Skip(1).ToList();
                    listenerCount = rows.Count;

                    foreach (var row in rows)
                    {
                        var userScores = new List<double>();

                        // Читаем оценки слушателя (используем 0, если не ответил, чтобы массивы были одинаковой длины для корреляции)
                        double uScore = 0, pScore = 0, aScore = 0, iScore = 0;

                        if (usefulnessCol != -1 && TryParseInt(row.Cell(usefulnessCol).Value.ToString(), out int u))
                        { uScore = u; usefulnessList.Add(u); userScores.Add(u); }
                        else { usefulnessList.Add(0); }

                        if (practicalityCol != -1 && TryParseInt(row.Cell(practicalityCol).Value.ToString(), out int p))
                        { pScore = p; practicalityList.Add(p); userScores.Add(p); }
                        else { practicalityList.Add(0); }

                        if (accessibilityCol != -1 && TryParseInt(row.Cell(accessibilityCol).Value.ToString(), out int a))
                        { aScore = a; accessibilityList.Add(a); userScores.Add(a); }
                        else { accessibilityList.Add(0); }

                        if (interactionCol != -1 && TryParseInt(row.Cell(interactionCol).Value.ToString(), out int i))
                        { iScore = i; interactionList.Add(i); userScores.Add(i); }
                        else { interactionList.Add(0); }

                        // ВОВЛЕЧЕННОСТЬ
                        if (engagementCol != -1)
                        {
                            var detachmentAnswer = row.Cell(engagementCol).Value.ToString()?.Trim().ToLower();
                            if (!string.IsNullOrWhiteSpace(detachmentAnswer))
                            {
                                totalEngagementAnswers++;
                                if (detachmentAnswer.Contains("нет")) engagedCount++;
                            }
                        }

                        // РАСПРЕДЕЛЕНИЕ
                        if (userScores.Count > 0)
                        {
                            double avgScore = userScores.Average();
                            int roundedScore = (int)Math.Round(avgScore, MidpointRounding.AwayFromZero);

                            if (roundedScore >= 1 && roundedScore <= 3) dist1to3++;
                            else if (roundedScore >= 4 && roundedScore <= 7) dist4to7++;
                            else if (roundedScore >= 8 && roundedScore <= 10) dist8to10++;
                        }

                        // КОММЕНТАРИИ
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
                    _logger.LogError(ex, "Ошибка при парсинге основного Excel файла.");
                }

                // 1. Считаем средние баллы (игнорируя нули, которые мы добавляли для выравнивания массивов)
                var averages = new Dictionary<string, double>
                {
                    { "Usefulness", usefulnessList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Practicality", practicalityList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Accessibility", accessibilityList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Interaction", interactionList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Engagement", totalEngagementAnswers > 0 ? Math.Round((double)engagedCount / totalEngagementAnswers * 100) : 0 }
                };

                // 2. Считаем Матрицу Корреляций Пирсона (4x4)
                var matrix = new double[4][];
                var dataLists = new List<List<double>> { usefulnessList, practicalityList, accessibilityList, interactionList };

                for (int i = 0; i < 4; i++)
                {
                    matrix[i] = new double[4];
                    for (int j = 0; j < 4; j++)
                    {
                        if (i == j)
                        {
                            matrix[i][j] = 1.0; // Корреляция с самим собой всегда 1
                        }
                        else
                        {
                            matrix[i][j] = CalculatePearsonCorrelation(dataLists[i], dataLists[j]);
                        }
                    }
                }

                // Превращаем матрицу в JSON-строку для передачи в БД и Дашборд
                string matrixJson = JsonSerializer.Serialize(matrix);

                return (programName, listenerCount, averages, allComments, dist1to3, dist4to7, dist8to10, matrixJson);
            });
        }

        public async Task<double> ParseHistoryFileAsync(Stream fileStream)
        {
            return await Task.Run(() =>
            {
                var allScores = new List<int>();

                try
                {
                    using var workbook = new XLWorkbook(fileStream);
                    var worksheet = workbook.Worksheet(1);

                    var headerRow = worksheet.FirstRowUsed();
                    if (headerRow == null) return 0.0;

                    int usefulnessCol = -1, practicalityCol = -1, accessibilityCol = -1, interactionCol = -1;

                    foreach (var cell in headerRow.CellsUsed())
                    {
                        string header = cell.Value.ToString()?.ToLower() ?? string.Empty;
                        int colIndex = cell.Address.ColumnNumber;

                        if (header.Contains("полезность программы по 10")) usefulnessCol = colIndex;
                        else if (header.Contains("практическую часть по 10")) practicalityCol = colIndex;
                        else if (header.Contains("доступность материала программы по 10")) accessibilityCol = colIndex;
                        else if (header.Contains("взаимодействие по 10")) interactionCol = colIndex;
                    }

                    var range = worksheet.RangeUsed();
                    if (range == null) return 0.0;

                    var rows = range.RowsUsed().Skip(1);

                    foreach (var row in rows)
                    {
                        if (usefulnessCol != -1 && TryParseInt(row.Cell(usefulnessCol).Value.ToString(), out int u)) allScores.Add(u);
                        if (practicalityCol != -1 && TryParseInt(row.Cell(practicalityCol).Value.ToString(), out int p)) allScores.Add(p);
                        if (accessibilityCol != -1 && TryParseInt(row.Cell(accessibilityCol).Value.ToString(), out int a)) allScores.Add(a);
                        if (interactionCol != -1 && TryParseInt(row.Cell(interactionCol).Value.ToString(), out int i)) allScores.Add(i);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при парсинге исторического Excel файла.");
                }

                return allScores.Count > 0 ? Math.Round(allScores.Average(), 1) : 0.0;
            });
        }

        // =========================================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // =========================================================================

        private bool TryParseInt(string? value, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

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

        // Математическая функция расчета коэффициента корреляции Пирсона
        private double CalculatePearsonCorrelation(List<double> x, List<double> y)
        {
            if (x.Count == 0 || y.Count == 0 || x.Count != y.Count) return 0.0;

            // Очищаем нули (пропуски) по обоим массивам синхронно, чтобы не искажать корреляцию
            var cleanX = new List<double>();
            var cleanY = new List<double>();

            for (int i = 0; i < x.Count; i++)
            {
                if (x[i] > 0 && y[i] > 0)
                {
                    cleanX.Add(x[i]);
                    cleanY.Add(y[i]);
                }
            }

            if (cleanX.Count <= 1) return 0.0; // Невозможно найти корреляцию для 1 человека

            double avgX = cleanX.Average();
            double avgY = cleanY.Average();

            double sumXY = 0, sumX2 = 0, sumY2 = 0;

            for (int i = 0; i < cleanX.Count; i++)
            {
                double dx = cleanX[i] - avgX;
                double dy = cleanY[i] - avgY;

                sumXY += dx * dy;
                sumX2 += dx * dx;
                sumY2 += dy * dy;
            }

            if (sumX2 == 0 || sumY2 == 0) return 0.0;

            double result = sumXY / Math.Sqrt(sumX2 * sumY2);

            // Если получился NaN или бесконечность - возвращаем 0
            if (double.IsNaN(result) || double.IsInfinity(result)) return 0.0;

            return Math.Round(result, 2);
        }
    }
}