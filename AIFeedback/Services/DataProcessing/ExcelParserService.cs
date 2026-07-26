using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace AIFeedback.Services.DataProcessing
{
    public class SurveyResponse
    {
        public int? Usefulness { get; set; }
        public int? Practicality { get; set; }
        public int? Accessibility { get; set; }
        public int? Interaction { get; set; }
        public string Engagement { get; set; } = string.Empty;
        public List<string> Comments { get; set; } = new List<string>();
    }

    public class ExcelParserService
    {
        private readonly ILogger<ExcelParserService> _logger;

        public ExcelParserService(ILogger<ExcelParserService> logger)
        {
            _logger = logger;
        }

        public List<SurveyResponse> ParseSurvey(Stream excelStream)
        {
            var responses = new List<SurveyResponse>();

            try
            {
                using var workbook = new XLWorkbook(excelStream);
                var worksheet = workbook.Worksheet(1);

                // Читаем первую строку (заголовки)
                var headerRow = worksheet.FirstRowUsed();

                // Словарик для хранения динамических индексов (начинаются с 1 в ClosedXML)
                int usefulnessCol = -1, practicalityCol = -1, accessibilityCol = -1, interactionCol = -1, engagementCol = -1;
                var textColumns = new List<int>();

                // Анализируем заголовки и ищем ключевые слова
                foreach (var cell in headerRow.CellsUsed())
                {
                    string header = cell.Value.ToString().ToLower();
                    int colIndex = cell.Address.ColumnNumber;

                    if (header.Contains("полезность программы по 10-балльной")) usefulnessCol = colIndex;
                    else if (header.Contains("практическую часть по 10-балльной")) practicalityCol = colIndex;
                    else if (header.Contains("доступность материала программы по 10-балльной")) accessibilityCol = colIndex;
                    else if (header.Contains("взаимодействие по 10")) interactionCol = colIndex;
                    else if (header.Contains("отстраненность от процесса")) engagementCol = colIndex;
                    else if (!header.Contains("ф.и.о.") && !header.Contains("место вашей работы") && !header.Contains("категории относится"))
                    {
                        // Все остальные колонки считаем текстовыми ответами, исключая ФИО и должность
                        textColumns.Add(colIndex);
                    }
                }

                // Пропускаем первую строку с заголовками и читаем данные
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    var response = new SurveyResponse();

                    if (usefulnessCol != -1) response.Usefulness = ParseIntSafe(row.Cell(usefulnessCol).Value.ToString());
                    if (practicalityCol != -1) response.Practicality = ParseIntSafe(row.Cell(practicalityCol).Value.ToString());
                    if (accessibilityCol != -1) response.Accessibility = ParseIntSafe(row.Cell(accessibilityCol).Value.ToString());
                    if (interactionCol != -1) response.Interaction = ParseIntSafe(row.Cell(interactionCol).Value.ToString());

                    if (engagementCol != -1) response.Engagement = row.Cell(engagementCol).Value.ToString()?.Trim().ToLower();

                    // Собираем текстовые комментарии
                    foreach (int colIndex in textColumns)
                    {
                        var comment = row.Cell(colIndex).Value.ToString()?.Trim();
                        if (IsValidComment(comment))
                        {
                            response.Comments.Add(comment);
                        }
                    }

                    responses.Add(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка при чтении Excel файла. Неверный формат.");
            }

            return responses;
        }

        private int? ParseIntSafe(string value)
        {
            if (int.TryParse(value?.Trim(), out int result))
            {
                if (result >= 1 && result <= 10) return result;
            }
            return null;
        }

        private bool IsValidComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment)) return false;

            var lowerComment = comment.ToLower();
            if (lowerComment == "-" || lowerComment == "нет" || lowerComment == "да" || lowerComment.Contains("затрудняюсь"))
                return false;

            return true;
        }
    }
}