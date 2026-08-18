using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AIFeedback.Models.DTOs;

namespace AIFeedback.Services.Report
{
    // ==========================================
    // 1. МОДЕЛЬ ДАННЫХ
    // ==========================================
    public class WordReportData
    {
        public string ProgramName { get; set; }
        public int ListenerCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public double UsefulnessAvg { get; set; }
        public double PracticalityAvg { get; set; }
        public double AvailabilityAvg { get; set; }
        public double InteractionAvg { get; set; }
        public double EngagementYesPercent { get; set; }
        public double OverallSatisfaction { get; set; }
        public AiAnalysisResultDto AiAnalysis { get; set; }

        // НОВОЕ ПОЛЕ: Словарь распределения оценок
        public Dictionary<string, int[]> ScoresDistribution { get; set; } = new Dictionary<string, int[]>();
    }

    // ==========================================
    // 2. ИНТЕРФЕЙС
    // ==========================================
    public interface IReportService
    {
        Task<Stream> GenerateWordReportAsync(WordReportData data);
    }

    // ==========================================
    // 3. СЕРВИС-ГЕНЕРАТОР WORD
    // ==========================================
    public class ReportService : IReportService
    {
        public async Task<Stream> GenerateWordReportAsync(WordReportData data)
        {
            return await Task.Run(() =>
            {
                var memoryStream = new MemoryStream();

                using (var doc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
                {
                    var mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new Document(new Body());
                    var body = mainPart.Document.Body;

                    string safeName = string.IsNullOrWhiteSpace(data.ProgramName) ? "Не указано" : data.ProgramName;
                    var ai = data.AiAnalysis;

                    var title = new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
                    title.Append(new Run(
                        new RunProperties(new Bold(), new FontSize() { Val = "26" }),
                        new Text("АНАЛИТИЧЕСКАЯ СПРАВКА ПО ИТОГАМ РЕАЛИЗАЦИИ ПРОГРАММЫ ПОВЫШЕНИЯ КВАЛИФИКАЦИИ")
                    ));
                    body.Append(title);
                    body.Append(new Paragraph(new Run(new Text(""))));

                    Table table = new Table();
                    TableProperties tblProp = new TableProperties(
                        new TableBorders(
                            new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                            new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                            new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                            new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                            new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                            new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
                        ),
                        new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct }
                    );
                    table.AppendChild(tblProp);

                    // --- РАЗДЕЛ 1: Общая информация ---
                    table.Append(CreateSectionHeaderRow("Общая информация о программе", 14));
                    table.Append(CreateRowWithColspan("Наименование программы", safeName, 4, 10));
                    table.Append(CreateRowWithColspan("Период обучения", data.CreatedAt.ToString("dd.MM.yyyy"), 4, 10));
                    table.Append(CreateRowWithColspan("Форма обучения", "Очная", 4, 10));
                    table.Append(CreateRowWithColspan("Количество слушателей", $"{data.ListenerCount} слушателя", 4, 10));
                    table.Append(CreateRowWithColspan("Преподаватели программы", "Экспертный состав", 4, 10));

                    // --- РАЗДЕЛ 2: Ключевые показатели ---
                    table.Append(CreateSectionHeaderRow("Ключевые показатели по программе", 14));

                    TableRow metricsHeader = new TableRow();
                    metricsHeader.Append(CreateCell("Ключевые показатели", true, true, 2));
                    metricsHeader.Append(CreateCell("Баллы по шкале/кол-во оценок", true, true, 10));
                    metricsHeader.Append(CreateCell("Средний балл по показателю", true, true, 1));
                    metricsHeader.Append(CreateCell("Примечание", true, true, 1));
                    table.Append(metricsHeader);

                    TableRow numbersRow = new TableRow();
                    numbersRow.Append(CreateCell("", false, false, 2));
                    for (int i = 1; i <= 10; i++) numbersRow.Append(CreateCell(i.ToString(), true, true));
                    numbersRow.Append(CreateCell("", false, false, 1));
                    numbersRow.Append(CreateCell("", false, false, 1));
                    table.Append(numbersRow);

                    // ИСПРАВЛЕНИЕ ЗДЕСЬ: Передаем словарь оценок
                    table.Append(CreateMetricRow("Полезность программы", "Usefulness", data.UsefulnessAvg, GetComment(ai, "полезн"), data.ScoresDistribution));
                    table.Append(CreateMetricRow("Практико-ориентированность программы", "Practicality", data.PracticalityAvg, GetComment(ai, "практик"), data.ScoresDistribution));
                    table.Append(CreateMetricRow("Доступность материалов по программе", "Accessibility", data.AvailabilityAvg, GetComment(ai, "доступн"), data.ScoresDistribution));
                    table.Append(CreateMetricRow("Взаимодействие с командой КУ", "Interaction", data.InteractionAvg, GetComment(ai, "взаимодейств"), data.ScoresDistribution));

                    // --- РАЗДЕЛ 3: Вовлеченность ---
                    TableRow engagementRow1 = new TableRow();
                    engagementRow1.Append(CreateCell("Вовлеченность в образовательный процесс", true, true, 2));
                    engagementRow1.Append(CreateCell("Чувствовалась ли отстранённость от образовательного процесса?", false, true, 9));
                    engagementRow1.Append(CreateCell("Уровень вовлеченности", true, true, 1));
                    engagementRow1.Append(CreateCell($"{data.EngagementYesPercent}% был максимально вовлечен в процесс обучения.", false, true, 2));
                    table.Append(engagementRow1);

                    TableRow engagementRow2 = new TableRow();
                    engagementRow2.Append(CreateCell("", false, false, 2));
                    engagementRow2.Append(CreateCell("Да", false, true, 4));
                    engagementRow2.Append(CreateCell("Нет", false, true, 5));
                    engagementRow2.Append(CreateCell("", false, false, 1));
                    engagementRow2.Append(CreateCell("", false, false, 2));
                    table.Append(engagementRow2);

                    TableRow engagementRow3 = new TableRow();
                    engagementRow3.Append(CreateCell("", false, false, 2));
                    engagementRow3.Append(CreateCell((100 - data.EngagementYesPercent).ToString() + "%", true, true, 4, "F4CCCC"));
                    engagementRow3.Append(CreateCell(data.EngagementYesPercent.ToString() + "%", true, true, 5, "D9EAD3"));
                    engagementRow3.Append(CreateCell(data.EngagementYesPercent.ToString() + "%", true, true, 1, "D9EAD3"));
                    engagementRow3.Append(CreateCell("", false, false, 2));
                    table.Append(engagementRow3);

                    // --- РАЗДЕЛ 4: Предложения слушателей ---
                    table.Append(CreateSectionHeaderRow("Предложения слушателей", 14));

                    TableRow proposalsHeader = new TableRow();
                    proposalsHeader.Append(CreateCell("Темы, которые оказались неактуальны для слушателей", true, true, 7));
                    proposalsHeader.Append(CreateCell("Темы, которыми можно дополнить программу обучения", true, true, 7));
                    table.Append(proposalsHeader);

                    string topTopics = ai?.TopTopics != null && ai.TopTopics.Any()
                        ? string.Join("\n", ai.TopTopics.Select((t, i) => $"{i + 1}. {t.Name} (упоминаний: {t.MentionsCount})"))
                        : "Дополнений не зафиксировано.";

                    TableRow proposalsData = new TableRow();
                    proposalsData.Append(CreateCell("Неактуальных тем не выявлено.", false, false, 7));
                    proposalsData.Append(CreateCell(topTopics, false, false, 7));
                    table.Append(proposalsData);

                    // --- РАЗДЕЛ 5: Форма обучения ---
                    table.Append(CreateSectionHeaderRow("Предпочтительная форма обучения", 14));

                    TableRow formHeader = new TableRow();
                    formHeader.Append(CreateCell("Очное обучение в аудиториях Корпоративного университета", false, true, 4));
                    formHeader.Append(CreateCell("Смешанное обучение: частично очно, частично дистанционно", false, true, 5));
                    formHeader.Append(CreateCell("Обучение с применением дистанционных образовательных технологий на своем рабочем месте", false, true, 5));
                    table.Append(formHeader);

                    TableRow formDataRow = new TableRow();
                    formDataRow.Append(CreateCell("Определяется на основе анкет", false, true, 4, "D9EAD3"));
                    formDataRow.Append(CreateCell("Определяется на основе анкет", false, true, 5, "FFF2CC"));
                    formDataRow.Append(CreateCell("Определяется на основе анкет", false, true, 5, "F4CCCC"));
                    table.Append(formDataRow);

                    // --- РАЗДЕЛ 6: Траектория ---
                    table.Append(CreateSectionHeaderRow("Траектория изменения программы по результатам итогового опроса слушателей", 14));

                    string recommendations = "Рекомендаций по изменению программы нет.";
                    if (ai?.Conclusions != null && ai.Conclusions.Any())
                    {
                        recommendations = string.Join("\n", ai.Conclusions.Select(c => $"{c.Action}. Обоснование: {c.DataProof}"));
                    }

                    table.Append(CreateRowWithColspan("Потребность в дальнейшей реализации программы", $"Программа актуальна и востребована среди слушателей, высокая потребность в дальнейшей реализации программы.", 4, 10));
                    table.Append(CreateRowWithColspan("Корректировка отбора слушателей", "Не требуется.", 4, 10));
                    table.Append(CreateRowWithColspan("Дополнение программы учебными вопросами", recommendations, 4, 10));
                    table.Append(CreateRowWithColspan("Изменение количества часов в программе", "Требуется, если добавлять практические занятия.", 4, 10));
                    table.Append(CreateRowWithColspan("Изменение формы обучения", "Не требуется.", 4, 10));

                    body.Append(table);
                    doc.Save();
                }

                memoryStream.Position = 0;
                return (Stream)memoryStream;
            });
        }

        private string GetComment(AiAnalysisResultDto ai, string keyword)
        {
            var insight = ai?.Conclusions?.FirstOrDefault(c => (c.Action ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase) || (c.DataProof ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase));
            return insight != null ? $"{insight.Action}. {insight.DataProof}" : "Оценка стабильна. Замечаний не выявлено.";
        }

        // ==========================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ СЛОЖНЫХ ТАБЛИЦ
        // ==========================================
        private TableRow CreateSectionHeaderRow(string text, int colspan)
        {
            TableRow tr = new TableRow();
            tr.Append(CreateCell(text, true, true, colspan, "CFE2F3"));
            return tr;
        }

        private TableRow CreateRowWithColspan(string col1, string col2, int span1, int span2)
        {
            TableRow tr = new TableRow();
            tr.Append(CreateCell(col1, false, false, span1));
            tr.Append(CreateCell(col2, false, false, span2));
            return tr;
        }

        // ИСПРАВЛЕНИЕ ЗДЕСЬ: Метод теперь правильно считывает данные из словаря
        private TableRow CreateMetricRow(string name, string key, double avgScore, string note, Dictionary<string, int[]> distMap)
        {
            TableRow tr = new TableRow();
            tr.Append(CreateCell(name, false, true, 2));

            int[] scores = new int[10];
            if (distMap != null && distMap.ContainsKey(key))
            {
                scores = distMap[key];
            }

            for (int i = 0; i < 10; i++)
            {
                string color = (i + 1) >= 9 ? "D9EAD3" : ((i + 1) >= 5 ? "FFF2CC" : "");
                int val = scores.Length > i ? scores[i] : 0;
                tr.Append(CreateCell(val.ToString(), false, true, 1, color)); // ТУТ ВЫВОДИТСЯ РЕАЛЬНАЯ ЦИФРА
            }

            tr.Append(CreateCell(avgScore.ToString("F1"), true, true, 1));
            tr.Append(CreateCell(note, false, false, 1));
            return tr;
        }

        private TableCell CreateCell(string text, bool isBold, bool isCenter = false, int colspan = 1, string hexColor = null)
        {
            TableCell tc = new TableCell();
            TableCellProperties tcp = new TableCellProperties();

            if (colspan > 1) tcp.Append(new GridSpan() { Val = colspan });
            if (!string.IsNullOrEmpty(hexColor)) tcp.Append(new Shading() { Val = ShadingPatternValues.Clear, Color = "auto", Fill = hexColor });

            tcp.Append(new TableCellMargin()
            {
                TopMargin = new TopMargin() { Width = "100", Type = TableWidthUnitValues.Dxa },
                BottomMargin = new BottomMargin() { Width = "100", Type = TableWidthUnitValues.Dxa },
                LeftMargin = new LeftMargin() { Width = "100", Type = TableWidthUnitValues.Dxa },
                RightMargin = new RightMargin() { Width = "100", Type = TableWidthUnitValues.Dxa }
            });
            tc.Append(tcp);

            Paragraph p = new Paragraph();
            if (isCenter) p.Append(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));

            var lines = (text ?? "").Split('\n');
            for (int l = 0; l < lines.Length; l++)
            {
                Run r = new Run();
                if (isBold) r.Append(new RunProperties(new Bold()));
                r.Append(new Text(lines[l]) { Space = SpaceProcessingModeValues.Preserve });
                p.Append(r);
                if (l < lines.Length - 1) p.Append(new Break());
            }

            tc.Append(p);
            return tc;
        }
    }
}