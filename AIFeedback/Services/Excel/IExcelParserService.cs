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

namespace AIFeedback.Services.Excel
{
    public interface IExcelParserService
    {
        // ДОБАВИЛИ string fileName в сигнатуру
        Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments, int Dist1to3, int Dist4to7, int Dist8to10, string CorrelationMatrixJson, string ScoresDistributionJson)> ParseAsync(Stream fileStream, string fileName);
        Task<double> ParseHistoryFileAsync(Stream fileStream);
    }

    public class ExcelParserService : IExcelParserService
    {
        private readonly ILogger<ExcelParserService> _logger;

        public ExcelParserService(ILogger<ExcelParserService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ОБЯЗАТЕЛЬНО ДОБАВЬ string fileName СЮДА ЖЕ:
        public async Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments, int Dist1to3, int Dist4to7, int Dist8to10, string CorrelationMatrixJson, string ScoresDistributionJson)> ParseAsync(Stream fileStream, string fileName)
        {
            return await Task.Run(() =>
            {
                // БЕРЕМ НАЗВАНИЕ ИЗ ИМЕНИ ЗАГРУЖЕННОГО ФАЙЛА (БЕЗ РАСШИРЕНИЯ)
                string programName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrWhiteSpace(programName))
                {
                    programName = "Анализируемая программа";
                }

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
                int totalEngagementAnswers = 0;

                try
                {
                    using var workbook = new XLWorkbook(fileStream);
                    var worksheet = workbook.Worksheet(1);

                    var headerRow = worksheet.FirstRowUsed();
                    if (headerRow == null) return (programName, 0, new Dictionary<string, double>(), allComments, 0, 0, 0, "null", "{}");

                    int usefulnessCol = -1, practicalityCol = -1, accessibilityCol = -1, interactionCol = -1, engagementCol = -1;
                    var textColumns = new List<int>();

                    foreach (var cell in headerRow.CellsUsed())
                    {
                        // Убираем переносы строк и лишние пробелы из заголовка для точного поиска
                        string header = cell.Value.ToString()?.ToLower() ?? string.Empty;
                        header = Regex.Replace(header, @"\s+", " ");
                        int colIndex = cell.Address.ColumnNumber;

                        if (header.Contains("полезност") && (header.Contains("10") || header.Contains("шкале"))) usefulnessCol = colIndex;
                        else if (header.Contains("практик") && (header.Contains("10") || header.Contains("шкале"))) practicalityCol = colIndex;
                        else if (header.Contains("доступност") && (header.Contains("10") || header.Contains("шкале"))) accessibilityCol = colIndex;
                        else if (header.Contains("взаимодействи") && (header.Contains("10") || header.Contains("шкале"))) interactionCol = colIndex;
                        else if (header.Contains("отстран") || header.Contains("потерю интерес")) engagementCol = colIndex;

                        if (!header.Contains("ф.и.о.") && !header.Contains("место вашей работы") && !header.Contains("категории относится"))
                        {
                            textColumns.Add(colIndex);
                        }
                    }

                    var range = worksheet.RangeUsed();
                    if (range == null) return (programName, 0, new Dictionary<string, double>(), allComments, 0, 0, 0, "null", "{}");

                    var rows = range.RowsUsed().Skip(1).ToList();
                    listenerCount = rows.Count;

                    foreach (var row in rows)
                    {
                        var userScores = new List<double>();

                        // ИСПРАВЛЕНИЕ: Читаем как double и аккуратно округляем для словаря
                        if (usefulnessCol != -1 && TryParseDouble(row.Cell(usefulnessCol).Value.ToString(), out double u))
                        {
                            usefulnessList.Add(u); userScores.Add(u);
                            int rounded = ClampScore(u);
                            scoresDist["Usefulness"][rounded - 1]++;
                        }
                        else { usefulnessList.Add(0); }

                        if (practicalityCol != -1 && TryParseDouble(row.Cell(practicalityCol).Value.ToString(), out double p))
                        {
                            practicalityList.Add(p); userScores.Add(p);
                            int rounded = ClampScore(p);
                            scoresDist["Practicality"][rounded - 1]++;
                        }
                        else { practicalityList.Add(0); }

                        if (accessibilityCol != -1 && TryParseDouble(row.Cell(accessibilityCol).Value.ToString(), out double a))
                        {
                            accessibilityList.Add(a); userScores.Add(a);
                            int rounded = ClampScore(a);
                            scoresDist["Accessibility"][rounded - 1]++;
                        }
                        else { accessibilityList.Add(0); }

                        if (interactionCol != -1 && TryParseDouble(row.Cell(interactionCol).Value.ToString(), out double i))
                        {
                            interactionList.Add(i); userScores.Add(i);
                            int rounded = ClampScore(i);
                            scoresDist["Interaction"][rounded - 1]++;
                        }
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

                        // РАСПРЕДЕЛЕНИЕ 1-3, 4-7, 8-10
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

                var averages = new Dictionary<string, double>
                {
                    { "Usefulness", usefulnessList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Practicality", practicalityList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Accessibility", accessibilityList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Interaction", interactionList.Where(x => x > 0).DefaultIfEmpty(0).Average() },
                    { "Engagement", totalEngagementAnswers > 0 ? Math.Round((double)engagedCount / totalEngagementAnswers * 100) : 0 }
                };

                var matrix = new double[4][];
                var dataLists = new List<List<double>> { usefulnessList, practicalityList, accessibilityList, interactionList };

                for (int i = 0; i < 4; i++)
                {
                    matrix[i] = new double[4];
                    for (int j = 0; j < 4; j++)
                    {
                        if (i == j) matrix[i][j] = 1.0;
                        else matrix[i][j] = CalculatePearsonCorrelation(dataLists[i], dataLists[j]);
                    }
                }

                string matrixJson = JsonSerializer.Serialize(matrix);
                string distJson = JsonSerializer.Serialize(scoresDist);

                return (programName, listenerCount, averages, allComments, dist1to3, dist4to7, dist8to10, matrixJson, distJson);
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

        // ИСПРАВЛЕННЫЙ МЕТОД ПАРСИНГА
        private bool TryParseDouble(string? value, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            // Извлекаем только цифры, точки и запятые
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

        // Ограничитель и округлитель для массива (от 1 до 10)
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