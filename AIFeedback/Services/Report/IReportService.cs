using AIFeedback.Models.DTOs;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AIFeedback.Services.Report
{
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
        public double UsefulnessMedian { get; set; }
        public double PracticalityMedian { get; set; }
        public double AvailabilityMedian { get; set; }
        public double InteractionMedian { get; set; }
        public double UsefulnessStdDev { get; set; }
        public double PracticalityStdDev { get; set; }
        public double AvailabilityStdDev { get; set; }
        public double InteractionStdDev { get; set; }
        public int DuplicateRowsRemoved { get; set; }
        public int FormatOfflineCount { get; set; }
        public int FormatMixedCount { get; set; }
        public int FormatOnlineCount { get; set; }
        public int EngagedCount { get; set; }
        public int DetachedCount { get; set; }
        public AiAnalysisResultDto AiAnalysis { get; set; }
        public Dictionary<string, int[]> ScoresDistribution { get; set; } = new Dictionary<string, int[]>();
    }

    public interface IReportService
    {
        Task<Stream> GenerateWordReportAsync(WordReportData data);
    }

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

                    string period = "Не указан";
                    var dateMatch = Regex.Match(safeName, @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?\s*[-–—]\s*\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?");
                    if (dateMatch.Success) period = dateMatch.Value.Replace("_", ".");
                    else
                    {
                        var singleMatch = Regex.Match(safeName, @"\d{2}[.\-_]\d{2}(?:[.\-_]\d{2,4})?");
                        if (singleMatch.Success) period = singleMatch.Value.Replace("_", ".");
                    }

                    var title = new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
                    title.Append(new Run(
                        new RunProperties(new Bold(), new FontSize() { Val = "24" }),
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

                    // Общая информация ---
                    table.Append(CreateSectionHeaderRow("Общая информация о программе", 14));
                    table.Append(CreateRowWithColspan("Наименование программы", safeName, 4, 10));
                    table.Append(CreateRowWithColspan("Период обучения", period, 4, 10));
                    table.Append(CreateRowWithColspan("Форма обучения", "Очная с применением дистанционных образовательных технологий и электронного обучения.", 4, 10));
                    table.Append(CreateRowWithColspan("Количество слушателей, принявших участие в опросе", $"{data.ListenerCount} слушателей", 4, 10));
                    table.Append(CreateRowWithColspan("Преподаватели программы", "Экспертный состав", 4, 10));

                    //  Ключевые показатели ---
                    table.Append(CreateSectionHeaderRow("Ключевые показатели по программе", 14));

                    TableRow metricsHeader = new TableRow();
                    metricsHeader.Append(CreateCell("Ключевые показатели", true, JustificationValues.Center, 2));
                    metricsHeader.Append(CreateCell("Баллы по шкале/кол-во оценок", true, JustificationValues.Center, 10));
                    metricsHeader.Append(CreateCell("Средний балл по показателю", true, JustificationValues.Center, 1));
                    metricsHeader.Append(CreateCell("Примечание", true, JustificationValues.Center, 1));
                    table.Append(metricsHeader);

                    TableRow numbersRow = new TableRow();
                    numbersRow.Append(CreateCell("", false, JustificationValues.Center, 2));
                    for (int i = 1; i <= 10; i++) numbersRow.Append(CreateCell(i.ToString(), true, JustificationValues.Center));
                    numbersRow.Append(CreateCell("", false, JustificationValues.Center, 1));
                    numbersRow.Append(CreateCell("", false, JustificationValues.Center, 1));
                    table.Append(numbersRow);

                    // Выводим аналитику от ИИ
                    table.Append(CreateMetricRow("Полезность программы", "Usefulness", data.UsefulnessAvg, ai?.MetricsNotes?.Usefulness, data.ScoresDistribution));
                    table.Append(CreateMetricRow("Практико-ориентированность программы", "Practicality", data.PracticalityAvg, ai?.MetricsNotes?.Practicality, data.ScoresDistribution));
                    table.Append(CreateMetricRow("Доступность материалов по программе", "Accessibility", data.AvailabilityAvg, ai?.MetricsNotes?.Accessibility, data.ScoresDistribution));
                    table.Append(CreateMetricRow("Взаимодействие с командой", "Interaction", data.InteractionAvg, ai?.MetricsNotes?.Interaction, data.ScoresDistribution));

                    //  Вовлеченность (Сложное объединение ячеек) ---
                    TableRow er1 = new TableRow();
                    er1.Append(CreateCell("Вовлеченность в образовательный процесс", true, JustificationValues.Center, 2, null, "restart"));
                    er1.Append(CreateCell("Чувствовалась ли отстранённость от образовательного процесса?", false, JustificationValues.Center, 9));
                    er1.Append(CreateCell("Уровень вовлеченности", true, JustificationValues.Center, 1, null, "restart"));
                    er1.Append(CreateCell(ai?.MetricsNotes?.Engagement ?? "", false, JustificationValues.Left, 2, null, "restart"));
                    table.Append(er1);

                    TableRow er2 = new TableRow();
                    er2.Append(CreateCell("", false, JustificationValues.Center, 2, null, "continue"));
                    er2.Append(CreateCell("Да", false, JustificationValues.Center, 4));
                    er2.Append(CreateCell("Нет", false, JustificationValues.Center, 5));
                    er2.Append(CreateCell("", false, JustificationValues.Center, 1, null, "continue"));
                    er2.Append(CreateCell("", false, JustificationValues.Center, 2, null, "continue"));
                    table.Append(er2);

                    TableRow er3 = new TableRow();
                    er3.Append(CreateCell("", false, JustificationValues.Center, 2, null, "continue"));
                    er3.Append(CreateCell(data.DetachedCount.ToString(), true, JustificationValues.Center, 4, "F4CCCC")); // Розовый
                    er3.Append(CreateCell(data.EngagedCount.ToString(), true, JustificationValues.Center, 5, "D9EAD3")); // Зеленый

                    int totalEng = data.EngagedCount + data.DetachedCount;
                    string engPercent = totalEng > 0 ? $"{Math.Round((double)data.EngagedCount / totalEng * 100)}%" : "Н/Д";

                    er3.Append(CreateCell(engPercent, true, JustificationValues.Center, 1, "D9EAD3"));
                    er3.Append(CreateCell("", false, JustificationValues.Center, 2, null, "continue"));
                    table.Append(er3);
                    table.Append(CreateSectionHeaderRow("Дополнительная статистика по критериям", 14));
                    
                    TableRow statsHeader = new TableRow();
                    statsHeader.Append(CreateCell("Критерий", true, JustificationValues.Center, 5));
                    statsHeader.Append(CreateCell("Среднее", true, JustificationValues.Center, 3));
                    statsHeader.Append(CreateCell("Медиана", true, JustificationValues.Center, 3));
                    statsHeader.Append(CreateCell("Стандартное отклонение", true, JustificationValues.Center, 3));
                    table.Append(statsHeader);
                    
                    void DrawStatsRow(string name, double avg, double median, double stdDev)
                                      {
                        TableRow tr = new TableRow();
                        tr.Append(CreateCell(name, false, JustificationValues.Left, 5));
                        tr.Append(CreateCell(avg.ToString("F1"), false, JustificationValues.Center, 3));
                        tr.Append(CreateCell(median.ToString("F1"), false, JustificationValues.Center, 3));
                        tr.Append(CreateCell(stdDev.ToString("F1"), false, JustificationValues.Center, 3));
                        table.Append(tr);
                      }
                    DrawStatsRow("Полезность программы", data.UsefulnessAvg, data.UsefulnessMedian, data.UsefulnessStdDev);
                    DrawStatsRow("Практико-ориентированность программы", data.PracticalityAvg, data.PracticalityMedian, data.PracticalityStdDev);
                    DrawStatsRow("Доступность материалов по программе", data.AvailabilityAvg, data.AvailabilityMedian, data.AvailabilityStdDev);
                    DrawStatsRow("Взаимодействие с командой", data.InteractionAvg, data.InteractionMedian, data.InteractionStdDev);
                    
                    if (data.DuplicateRowsRemoved > 0)
                                           {
                        TableRow dupRow = new TableRow();
                        dupRow.Append(CreateCell(
                        $"Примечание: при разборе исходного файла обнаружено и исключено из расчётов {data.DuplicateRowsRemoved} дублирующихся анкет.",
                        false, JustificationValues.Left, 14));
                        table.Append(dupRow);
                        }
                    // Предложения слушателей ---
                    table.Append(CreateSectionHeaderRow("Предложения слушателей", 14));

                    TableRow pHeader = new TableRow();
                    pHeader.Append(CreateCell("Темы, которые оказались неактуальны для слушателей", true, JustificationValues.Center, 7));
                    pHeader.Append(CreateCell("Темы, которыми можно дополнить программу обучения", true, JustificationValues.Center, 7));
                    table.Append(pHeader);

                    TableRow pData = new TableRow();
                    pData.Append(CreateCell(ai?.UnnecessaryTopics ?? "Неактуальных тем не выявлено.", false, JustificationValues.Left, 7));
                    pData.Append(CreateCell(ai?.TopicsToAdd ?? "Дополнений не зафиксировано.", false, JustificationValues.Left, 7));
                    table.Append(pData);

                    //Форма обучения ---
                    table.Append(CreateSectionHeaderRow("Предпочтительная форма обучения", 14));

                    TableRow formHeader = new TableRow();
                    formHeader.Append(CreateCell("Очное обучение в аудиториях КУ", true, JustificationValues.Center, 4));
                    formHeader.Append(CreateCell("Смешанное обучение: частично очно, частично дистанционно", true, JustificationValues.Center, 5));
                    formHeader.Append(CreateCell("Обучение с применением дистанционных образовательных технологий", true, JustificationValues.Center, 5));
                    table.Append(formHeader);

                    int totalFormat = data.FormatOfflineCount + data.FormatMixedCount + data.FormatOnlineCount;
                    string offP = totalFormat > 0 ? $"{Math.Round((double)data.FormatOfflineCount / totalFormat * 100)}%" : "0%";
                    string mixP = totalFormat > 0 ? $"{Math.Round((double)data.FormatMixedCount / totalFormat * 100)}%" : "0%";
                    string onlP = totalFormat > 0 ? $"{Math.Round((double)data.FormatOnlineCount / totalFormat * 100)}%" : "0%";

                    TableRow formData = new TableRow();
                    formData.Append(CreateCell($"{data.FormatOfflineCount} чел. ({offP})", false, JustificationValues.Center, 4));
                    formData.Append(CreateCell($"{data.FormatMixedCount} чел. ({mixP})", false, JustificationValues.Center, 5));
                    formData.Append(CreateCell($"{data.FormatOnlineCount} чел. ({onlP})", false, JustificationValues.Center, 5));
                    table.Append(formData);

                    // Траектория ---
                    table.Append(CreateSectionHeaderRow("Траектория изменения программы по результатам итогового опроса слушателей", 14));
                    table.Append(CreateRowWithColspan("Потребность в дальнейшей реализации программы", ai?.Trajectory?.Relevance ?? "", 4, 10));
                    table.Append(CreateRowWithColspan("Корректировка отбора слушателей", ai?.Trajectory?.Selection ?? "", 4, 10));
                    table.Append(CreateRowWithColspan("Дополнение программы учебными вопросами", ai?.Trajectory?.Additions ?? "", 4, 10));
                    table.Append(CreateRowWithColspan("Изменение количества часов в программе", ai?.Trajectory?.Hours ?? "", 4, 10));
                    table.Append(CreateRowWithColspan("Изменение формы обучения", ai?.Trajectory?.Format ?? "", 4, 10));

                    body.Append(table);

                    // ДОКУМЕНТ В АЛЬБОМНУЮ ОРИЕНТАЦИЮ
                    SectionProperties sectionProperties = new SectionProperties();
                    PageSize pageSize = new PageSize()
                    {
                        Width = (UInt32Value)16838U, 
                        Height = (UInt32Value)11906U, 
                        Orient = PageOrientationValues.Landscape
                    };
                    PageMargin pageMargin = new PageMargin()
                    {
                        Top = 1134,
                        Bottom = 1134,
                        Left = 1134,
                        Right = 1134, // Умеренные отступы
                        Header = (UInt32Value)708U,
                        Footer = (UInt32Value)708U,
                        Gutter = (UInt32Value)0U
                    };
                    sectionProperties.Append(pageSize);
                    sectionProperties.Append(pageMargin);
                    body.Append(sectionProperties);

                    doc.Save();
                }

                memoryStream.Position = 0;
                return (Stream)memoryStream;
            });
        }

        private TableRow CreateSectionHeaderRow(string text, int colspan)
        {
            TableRow tr = new TableRow();
            tr.Append(CreateCell(text, true, JustificationValues.Center, colspan, "CFE2F3"));
            return tr;
        }

        private TableRow CreateRowWithColspan(string col1, string col2, int span1, int span2)
        {
            TableRow tr = new TableRow();
            tr.Append(CreateCell(col1, false, JustificationValues.Left, span1));
            tr.Append(CreateCell(col2, false, JustificationValues.Left, span2));
            return tr;
        }

        private TableRow CreateMetricRow(string name, string key, double avgScore, string note, Dictionary<string, int[]> distMap)
        {
            TableRow tr = new TableRow();
            tr.Append(CreateCell(name, false, JustificationValues.Left, 2));

            int[] scores = new int[10];
            if (distMap != null && distMap.ContainsKey(key)) scores = distMap[key];

            for (int i = 0; i < 10; i++)
            {
                int val = scores.Length > i ? scores[i] : 0;
                string color = null; // По умолчанию белый

                // красим только если значение больше 0
                if (val > 0)
                {
                    if (i + 1 <= 5) color = "F4CCCC";      // 1-5: Красный
                    else if (i + 1 <= 8) color = "FFF2CC"; // 6-8: Желтый
                    else color = "D9EAD3";                 // 9-10: Зеленый
                }

                // Скрываем нули (если val == 0, выводим пустоту)
                tr.Append(CreateCell(val == 0 ? "" : val.ToString(), false, JustificationValues.Center, 1, color));
            }

            tr.Append(CreateCell(avgScore.ToString("F1"), true, JustificationValues.Center, 1));
            tr.Append(CreateCell(note ?? "Оценка стабильна. Замечаний не выявлено.", false, JustificationValues.Left, 1));
            return tr;
        }

        private TableCell CreateCell(string text, bool isBold, JustificationValues? align = null, int colspan = 1, string hexColor = null, string vMerge = null)
        {
            TableCell tc = new TableCell();
            TableCellProperties tcp = new TableCellProperties();

            if (colspan > 1) tcp.Append(new GridSpan() { Val = colspan });

            // Вертикальное объединение
            if (vMerge == "restart") tcp.Append(new VerticalMerge() { Val = MergedCellValues.Restart });
            else if (vMerge == "continue") tcp.Append(new VerticalMerge() { Val = MergedCellValues.Continue });

            if (!string.IsNullOrEmpty(hexColor)) tcp.Append(new Shading() { Val = ShadingPatternValues.Clear, Color = "auto", Fill = hexColor });

            tcp.Append(new TableCellMargin()
            {
                TopMargin = new TopMargin() { Width = "100", Type = TableWidthUnitValues.Dxa },
                BottomMargin = new BottomMargin() { Width = "100", Type = TableWidthUnitValues.Dxa },
                LeftMargin = new LeftMargin() { Width = "100", Type = TableWidthUnitValues.Dxa },
                RightMargin = new RightMargin() { Width = "100", Type = TableWidthUnitValues.Dxa }
            });
            tc.Append(tcp);

            JustificationValues actualAlign = align ?? JustificationValues.Left;

            if (vMerge != "continue")
            {
                var lines = (text ?? "").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    Paragraph p = new Paragraph(new ParagraphProperties(new Justification() { Val = actualAlign }));
                    Run r = new Run(new Text(lines[i].Trim('\r')) { Space = SpaceProcessingModeValues.Preserve });
                    if (isBold) r.Append(new RunProperties(new Bold()));
                    p.Append(r);
                    tc.Append(p);
                }
            }
            else
            {
                tc.Append(new Paragraph());
            }

            return tc;
        }
    }
}