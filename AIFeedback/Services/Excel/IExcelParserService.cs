using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Globalization;

using System.IO;
using System.Threading.Tasks;

namespace AIFeedback.Services.Excel
{
    public interface IExcelParserService
    {
        Task<ExcelParseResult> ParseAsync(Stream fileStream, string fileName);
        Task<double> ParseHistoryFileAsync(Stream fileStream);
    }

public class ExcelParserService : IExcelParserService
    {
        private readonly ILogger<ExcelParserService> _logger;

        public ExcelParserService(ILogger<ExcelParserService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExcelParseResult> ParseAsync(Stream fileStream, string fileName)
        {
            return await Task.Run(() =>
            {
                string programName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrWhiteSpace(programName))
                {
                    programName = "Анализируемая программа";
                }

                var result = new ExcelParseResult { ProgramName = programName };

                var allComments = new List<string>();

                var usefulnessList = new List<double>();
                var practicalityList = new List<double>();
                var accessibilityList = new List<double>();
                var interactionList = new List<double>();

                var scoresDist = new Dictionary<string, int[]>
        {
            { "Usefulness", new int[10] },
            { "Practicality", new int[10] },
            { "Accessibility", new int[10] },
            { "Interaction", new int[10] }
        };

                int listenerCount = 0;
                int dist1to3 = 0, dist4to7 = 0, dist8to10 = 0;

                int engagedCount = 0;
                int detachedCount = 0;
                int totalEngagementAnswers = 0;

                int formatOffline = 0;
                int formatMixed = 0;
                int formatOnline = 0;

                int usefulnessCol = -1, practicalityCol = -1, accessibilityCol = -1, interactionCol = -1;
                int duplicatesRemoved = 0;

                try
                {
                    using var workbook = new XLWorkbook(fileStream);
                    var worksheet = workbook.Worksheet(1);

                    var headerRow = worksheet.FirstRowUsed();
                    if (headerRow == null)
                    {
                        result.ParseSuccess = false;
                        return result;
                    }

                    int engagementCol = -1, formatCol = -1;
                    var textColumns = new List<int>();

                    foreach (var cell in headerRow.CellsUsed())
                    {
                        string header = cell.Value.ToString()?.ToLower() ?? string.Empty;
                        header = Regex.Replace(header, @"\s+", " ");
                        int colIndex = cell.Address.ColumnNumber;

                        if (header.Contains("полезност") && (header.Contains("10") || header.Contains("шкале"))) usefulnessCol = colIndex;
                        else if (header.Contains("практик") && (header.Contains("10") || header.Contains("шкале"))) practicalityCol = colIndex;
                        else if (header.Contains("доступност") && (header.Contains("10") || header.Contains("шкале"))) accessibilityCol = colIndex;
                        else if (header.Contains("взаимодействи") && (header.Contains("10") || header.Contains("шкале"))) interactionCol = colIndex;
                        else if (header.Contains("отстран") || header.Contains("потерю интерес")) engagementCol = colIndex;
                        else if (header.Contains("выбрать формат обучения")) formatCol = colIndex;

                        if (!header.Contains("ф.и.о.") && !header.Contains("место вашей работы") && !header.Contains("категории относится"))
                        {
                            textColumns.Add(colIndex);
                        }
                    }

                    var range = worksheet.RangeUsed();
                    if (range == null)
                    {
                        result.ParseSuccess = false;
                        return result;
                    }

                    var rows = range.RowsUsed().Skip(1).ToList();

                    // ==========================================
                    // ДЕДУПЛИКАЦИЯ: строим сигнатуру строки из значимых столбцов
                    // (оценки + текстовые ответы), исключая пустые анкеты из сравнения
                    // ==========================================
                    var seenSignatures = new HashSet<string>();
                    var uniqueRows = new List<IXLRangeRow>();

                    var signatureColumns = new List<int>();
                    if (usefulnessCol != -1) signatureColumns.Add(usefulnessCol);
                    if (practicalityCol != -1) signatureColumns.Add(practicalityCol);
                    if (accessibilityCol != -1) signatureColumns.Add(accessibilityCol);
                    if (interactionCol != -1) signatureColumns.Add(interactionCol);
                    signatureColumns.AddRange(textColumns);

                    foreach (var row in rows)
                    {
                        var parts = signatureColumns
                            .Select(c => row.Cell(c).Value.ToString()?.Trim().ToLowerInvariant() ?? "")
                            .ToList();

                        string signature = string.Join("|", parts);

                        // Пустые строки (все значения пусты) не считаем дублями — пропускаем их как есть
                        bool isEffectivelyEmpty = parts.All(string.IsNullOrWhiteSpace);

                        if (!isEffectivelyEmpty && !seenSignatures.Add(signature))
                        {
                            duplicatesRemoved++;
                            continue; // строка уже встречалась — пропускаем
                        }

                        uniqueRows.Add(row);
                    }

                    listenerCount = uniqueRows.Count;

                    foreach (var row in uniqueRows)
                    {
                        var userScores = new List<double>();

                        if (usefulnessCol != -1 && TryParseDouble(row.Cell(usefulnessCol).Value.ToString(), out double u))
                        {
                            usefulnessList.Add(u); userScores.Add(u);
                            scoresDist["Usefulness"][ClampScore(u) - 1]++;
                        }
                        else { usefulnessList.Add(0); }

                        if (practicalityCol != -1 && TryParseDouble(row.Cell(practicalityCol).Value.ToString(), out double p))
                        {
                            practicalityList.Add(p); userScores.Add(p);
                            scoresDist["Practicality"][ClampScore(p) - 1]++;
                        }
                        else { practicalityList.Add(0); }

                        if (accessibilityCol != -1 && TryParseDouble(row.Cell(accessibilityCol).Value.ToString(), out double a))
                        {
                            accessibilityList.Add(a); userScores.Add(a);
                            scoresDist["Accessibility"][ClampScore(a) - 1]++;
                        }
                        else { accessibilityList.Add(0); }

                        if (interactionCol != -1 && TryParseDouble(row.Cell(interactionCol).Value.ToString(), out double i))
                        {
                            interactionList.Add(i); userScores.Add(i);
                            scoresDist["Interaction"][ClampScore(i) - 1]++;
                        }
                        else { interactionList.Add(0); }

                        if (engagementCol != -1)
                        {
                            var detachmentAnswer = row.Cell(engagementCol).Value.ToString()?.Trim().ToLower();
                            if (!string.IsNullOrWhiteSpace(detachmentAnswer))
                            {
                                totalEngagementAnswers++;
                                if (detachmentAnswer.Contains("да")) detachedCount++;
                                else if (detachmentAnswer.Contains("нет")) engagedCount++;
                            }
                        }

                        if (formatCol != -1)
                        {
                            var formatAnswer = row.Cell(formatCol).Value.ToString()?.Trim().ToLower();
                            if (!string.IsNullOrWhiteSpace(formatAnswer))
                            {
                                if (formatAnswer.Contains("смешанное")) formatMixed++;
                                else if (formatAnswer.Contains("дистанционн")) formatOnline++;
                                else if (formatAnswer.Contains("очно") || formatAnswer.Contains("аудитори")) formatOffline++;
                            }
                        }

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
                    _logger.LogError(ex, "Ошибка при парсинге основного Excel файла.");
                    result.ParseSuccess = false;
                }

                double Median(List<double> values)
                {
                    var clean = values.Where(x => x > 0).OrderBy(x => x).ToList();
                    if (clean.Count == 0) return 0;
                    int mid = clean.Count / 2;
                    return clean.Count % 2 != 0
                        ? clean[mid]
                        : Math.Round((clean[mid - 1] + clean[mid]) / 2.0, 2);
                }

                double StdDev(List<double> values)
                {
                    var clean = values.Where(x => x > 0).ToList();
                    if (clean.Count <= 1) return 0;
                    double avg = clean.Average();
                    double sumSquares = clean.Sum(x => Math.Pow(x - avg, 2));
                    return Math.Round(Math.Sqrt(sumSquares / (clean.Count - 1)), 2);
                }

                result.NumericAverages = new Dictionary<string, double>
        {
            { "Usefulness", usefulnessList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
            { "Practicality", practicalityList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
            { "Accessibility", accessibilityList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
            { "Interaction", interactionList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
            { "Engagement", totalEngagementAnswers > 0 ? Math.Round((double)engagedCount / totalEngagementAnswers * 100) : 0 }
        };

                result.NumericMedians = new Dictionary<string, double>
        {
            { "Usefulness", Median(usefulnessList) },
            { "Practicality", Median(practicalityList) },
            { "Accessibility", Median(accessibilityList) },
            { "Interaction", Median(interactionList) }
        };

                result.NumericStdDeviations = new Dictionary<string, double>
        {
            { "Usefulness", StdDev(usefulnessList) },
            { "Practicality", StdDev(practicalityList) },
            { "Accessibility", StdDev(accessibilityList) },
            { "Interaction", StdDev(interactionList) }
        };

                var matrix = new double[4][];
                var dataLists = new List<List<double>> { usefulnessList, practicalityList, accessibilityList, interactionList };
                for (int i = 0; i < 4; i++)
                {
                    matrix[i] = new double[4];
                    for (int j = 0; j < 4; j++)
                    {
                        matrix[i][j] = i == j ? 1.0 : CalculatePearsonCorrelation(dataLists[i], dataLists[j]);
                    }
                }

                result.ListenerCount = listenerCount;
                result.AllComments = allComments;
                result.Dist1to3 = dist1to3;
                result.Dist4to7 = dist4to7;
                result.Dist8to10 = dist8to10;
                result.CorrelationMatrixJson = JsonSerializer.Serialize(matrix);
                result.ScoresDistributionJson = JsonSerializer.Serialize(scoresDist);
                result.FormatOffline = formatOffline;
                result.FormatMixed = formatMixed;
                result.FormatOnline = formatOnline;
                result.EngagedCount = engagedCount;
                result.DetachedCount = detachedCount;
                result.DuplicateRowsRemoved = duplicatesRemoved;

                bool noMetricColumnsFound = listenerCount > 0
                    && usefulnessCol == -1 && practicalityCol == -1
                    && accessibilityCol == -1 && interactionCol == -1;

                result.ParseSuccess = result.ParseSuccess && !noMetricColumnsFound;

                return result;
            });
        }

        public async Task<double> ParseHistoryFileAsync(Stream fileStream)
        {
            return await Task.Run(() =>
            {
                var allScores = new List<double>();
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
                        if (usefulnessCol != -1 && TryParseDouble(row.Cell(usefulnessCol).Value.ToString(), out double u)) allScores.Add(u);
                        if (practicalityCol != -1 && TryParseDouble(row.Cell(practicalityCol).Value.ToString(), out double p)) allScores.Add(p);
                        if (accessibilityCol != -1 && TryParseDouble(row.Cell(accessibilityCol).Value.ToString(), out double a)) allScores.Add(a);
                        if (interactionCol != -1 && TryParseDouble(row.Cell(interactionCol).Value.ToString(), out double i)) allScores.Add(i);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при парсинге исторического Excel файла.");
                }
                return allScores.Count > 0 ? Math.Round(allScores.Average(), 1) : 0.0;
            });
        }

        private bool TryParseDouble(string? value, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var match = Regex.Match(value.Trim(), @"\d+([.,]\d+)?");
            if (match.Success)
            {
                string cleanVal = match.Value.Replace(",", ".");
                if (double.TryParse(cleanVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                {
                    if (val >= 1 && val <= 10)
                    {
                        result = val;
                        return true;
                    }
                }
            }
            return false;
        }

        private int ClampScore(double score)
        {
            int rounded = (int)Math.Round(score, MidpointRounding.AwayFromZero);
            if (rounded < 1) return 1;
            if (rounded > 10) return 10;
            return rounded;
        }

        private bool IsValidComment(string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment)) return false;
            var lower = comment.ToLower();
            if (lower == "-" || lower == "нет" || lower == "да" || lower.Contains("затрудняюсь") || lower.Length < 3) return false;
            return true;
        }

        private double CalculatePearsonCorrelation(List<double> x, List<double> y)
        {
            if (x.Count == 0 || y.Count == 0 || x.Count != y.Count) return 0.0;

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

            if (cleanX.Count <= 1) return 0.0;

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
            if (double.IsNaN(result) || double.IsInfinity(result)) return 0.0;

            return Math.Round(result, 2);
        }
    }
}