using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace AIFeedback.Services.Excel
{
    // ==========================================
    // 1. ИНТЕРФЕЙС НАПАРНИЦЫ
    // ==========================================
    public interface IExcelParserService
    {
        Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments)> ParseAsync(Stream fileStream);
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

        public async Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments)> ParseAsync(Stream fileStream)
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

                try
                {
                    using var workbook = new XLWorkbook(fileStream);
                    var worksheet = workbook.Worksheet(1);

                    var headerRow = worksheet.FirstRowUsed();
                    if (headerRow == null) return (programName, 0, new Dictionary<string, double>(), allComments);

                    int usefulnessCol = -1, practicalityCol = -1, accessibilityCol = -1, interactionCol = -1;
                    var textColumns = new List<int>();

                    foreach (var cell in headerRow.CellsUsed())
                    {
                        string header = cell.Value.ToString()?.ToLower() ?? string.Empty;
                        int colIndex = cell.Address.ColumnNumber;

                        if (header.Contains("полезность программы по 10-балльной")) usefulnessCol = colIndex;
                        else if (header.Contains("практическую часть по 10-балльной")) practicalityCol = colIndex;
                        else if (header.Contains("доступность материала программы по 10-балльной")) accessibilityCol = colIndex;
                        else if (header.Contains("взаимодействие по 10")) interactionCol = colIndex;
                        else if (!header.Contains("ф.и.о.") && !header.Contains("место вашей работы") && !header.Contains("категории относится"))
                        {
                            textColumns.Add(colIndex);
                        }
                    }

                    var range = worksheet.RangeUsed();
                    if (range == null) return (programName, 0, new Dictionary<string, double>(), allComments);

                    var rows = range.RowsUsed().Skip(1).ToList();
                    listenerCount = rows.Count;

                    foreach (var row in rows)
                    {
                        if (usefulnessCol != -1 && TryParseInt(row.Cell(usefulnessCol).Value.ToString(), out int u)) usefulnessList.Add(u);
                        if (practicalityCol != -1 && TryParseInt(row.Cell(practicalityCol).Value.ToString(), out int p)) practicalityList.Add(p);
                        if (accessibilityCol != -1 && TryParseInt(row.Cell(accessibilityCol).Value.ToString(), out int a)) accessibilityList.Add(a);
                        if (interactionCol != -1 && TryParseInt(row.Cell(interactionCol).Value.ToString(), out int i)) interactionList.Add(i);

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
                    { "Interaction", interactionList.Count > 0 ? interactionList.Average() : 0 }
                };

                return (programName, listenerCount, averages, allComments);
            });
        }

        private bool TryParseInt(string? value, out int result)
        {
            result = 0;
            if (int.TryParse(value?.Trim(), out int val))
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