using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using AIFeedback.Models;
using AIFeedback.Models.DTOs;

namespace AIFeedback.Services.DataProcessing
{
    public class ReportExportService
    {
        // ====================================================================
        // 1. ЭКСПОРТ В WORD (.docx)
        // ====================================================================
        public byte[] GenerateDocxReport(ProgramSessionStats stats, AiAnalysisResultDto aiResult)
        {
            using var memoryStream = new MemoryStream();
            using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());

                // Ставим знак '!', чтобы убрать предупреждение о возможном null
                var body = mainPart.Document.Body!;

                // Раздел 1: Общая информация о программе
                AddHeading(body, "1. Общая информация о программе");
                body.Append(CreateGeneralInfoTable(stats));
                AddEmptyLine(body);

                // Раздел 2: Ключевые показатели по программе
                AddHeading(body, "2. Ключевые показатели по программе");
                body.Append(CreateCriteriaTable(stats, aiResult));
                AddEmptyLine(body);

                // Раздел 3: Предложения слушателей
                AddHeading(body, "3. Предложения слушателей");
                AddParagraph(body, "Темы, которые оказались неактуальны для слушателей:", true);
                foreach (var theme in aiResult.UnrelevantTopics ?? new List<string>())
                {
                    AddParagraph(body, $"- {theme}");
                }

                AddParagraph(body, "Темы, которыми можно дополнить программу:", true);
                foreach (var topic in aiResult.TopTopics ?? new List<Topic>())
                {
                    AddParagraph(body, $"- {topic.Name} ({topic.MentionCount} чел.)");
                }
                AddEmptyLine(body);

                // Раздел 4: Траектория изменения программы
                AddHeading(body, "4. Траектория изменения программы");
                foreach (var conclusion in aiResult.Conclusions ?? new List<Conclusion>())
                {
                    AddParagraph(body, $"[{conclusion.Priority}] {conclusion.Text}", true);
                    AddParagraph(body, $"Рекомендация: {conclusion.Recommendation}");
                    AddEmptyLine(body);
                }

                mainPart.Document.Save();
            }

            return memoryStream.ToArray();
        }

        // ====================================================================
        // 2. ЭКСПОРТ В PDF (PuppeteerSharp)
        // ====================================================================
        public async Task<byte[]> GeneratePdfReportAsync(ProgramSessionStats stats, AiAnalysisResultDto aiResult)
        {
            string htmlContent = BuildHtmlReport(stats, aiResult);

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();

            await page.SetContentAsync(htmlContent);

            var pdfData = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions { Top = "20mm", Bottom = "20mm", Left = "20mm", Right = "20mm" }
            });

            return pdfData;
        }

        // ====================================================================
        // 3. ГЕНЕРАТОР HTML-ШАБЛОНА
        // ====================================================================
        private string BuildHtmlReport(ProgramSessionStats stats, AiAnalysisResultDto aiResult)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='UTF-8'><style>");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; font-size: 14px; color: #333; line-height: 1.5; }");
            sb.Append("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            sb.Append("th, td { border: 1px solid #000; padding: 8px; text-align: left; vertical-align: top; }");
            sb.Append("h1 { font-size: 18px; color: #000; font-weight: bold; border-bottom: 1px solid #000; padding-bottom: 5px; }");
            sb.Append("h2 { font-size: 16px; color: #000; margin-top: 15px; }");
            sb.Append(".bold { font-weight: bold; }");
            sb.Append("</style></head><body>");

            // Раздел 1
            sb.Append("<h1>1. Общая информация о программе</h1>");
            sb.Append("<table>");
            sb.Append($"<tr><td class='bold' width='30%'>Наименование программы</td><td>{stats.ProgramName}</td></tr>");
            sb.Append($"<tr><td class='bold'>Период обучения</td><td>{stats.TrainingPeriod}</td></tr>");
            sb.Append($"<tr><td class='bold'>Количество слушателей</td><td>{stats.TotalListeners} чел.</td></tr>");
            sb.Append("</table>");

            // Раздел 2 (Здесь используем свойства Avg... вместо Average)
            sb.Append("<h1>2. Ключевые показатели по программе</h1>");
            sb.Append("<table>");
            sb.Append("<tr><td class='bold' width='25%'>Критерий</td><td class='bold' width='15%'>Средний балл</td><td class='bold'>Примечание ИИ</td></tr>");
            sb.Append($"<tr><td class='bold'>Полезность программы</td><td>{stats.AvgUsefulness:F1}</td><td>{aiResult.UsefulnessComment}</td></tr>");
            sb.Append($"<tr><td class='bold'>Практико-ориентированность</td><td>{stats.AvgPracticality:F1}</td><td>{aiResult.PracticalityComment}</td></tr>");
            sb.Append($"<tr><td class='bold'>Доступность материалов</td><td>{stats.AvgAccessibility:F1}</td><td>{aiResult.AccessibilityComment}</td></tr>");
            sb.Append($"<tr><td class='bold'>Взаимодействие с командой</td><td>{stats.AvgInteraction:F1}</td><td>{aiResult.InteractionComment}</td></tr>");
            sb.Append($"<tr><td class='bold'>Вовлеченность (Да/Нет)</td><td>{stats.EngagementPercentage:F1}%</td><td>{aiResult.EngagementComment}</td></tr>");
            sb.Append("</table>");

            // Раздел 3
            sb.Append("<h1>3. Предложения слушателей</h1>");
            sb.Append("<h2>Темы, которые оказались неактуальны:</h2><ul>");
            foreach (var t in aiResult.UnrelevantTopics ?? new List<string>()) sb.Append($"<li>{t}</li>");
            sb.Append("</ul><h2>Темы, которыми можно дополнить программу:</h2><ul>");
            foreach (var topic in aiResult.TopTopics ?? new List<Topic>()) sb.Append($"<li>{topic.Name} ({topic.MentionCount} чел.)</li>");
            sb.Append("</ul>");

            // Раздел 4
            sb.Append("<h1>4. Траектория изменения программы</h1><ul>");
            foreach (var c in aiResult.Conclusions ?? new List<Conclusion>()) sb.Append($"<li><b>[{c.Priority}]</b> {c.Text}<br/><i>Рекомендация:</i> {c.Recommendation}</li>");
            sb.Append("</ul>");

            sb.Append("</body></html>");
            return sb.ToString();
        }

        // ====================================================================
        // Вспомогательные методы для OpenXML (Word)
        // ====================================================================
        private void AddHeading(Body body, string text)
        {
            var paragraph = new Paragraph();
            var run = new Run(new Text(text));
            run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" });
            paragraph.Append(run);
            body.Append(paragraph);
        }

        private void AddParagraph(Body body, string text, bool isBold = false)
        {
            var paragraph = new Paragraph();
            var run = new Run(new Text(text));
            if (isBold) run.RunProperties = new RunProperties(new Bold());
            paragraph.Append(run);
            body.Append(paragraph);
        }

        private void AddEmptyLine(Body body)
        {
            body.Append(new Paragraph(new Run(new Text(""))));
        }

        private Table CreateGeneralInfoTable(ProgramSessionStats stats)
        {
            var table = new Table();
            table.AppendChild(GetStandardTableProperties());

            table.Append(CreateTableRow("Наименование программы", stats.ProgramName ?? "Не указано"));
            table.Append(CreateTableRow("Период обучения", stats.TrainingPeriod ?? "Не указано"));
            table.Append(CreateTableRow("Количество слушателей", $"{stats.TotalListeners} чел."));

            return table;
        }

        private Table CreateCriteriaTable(ProgramSessionStats stats, AiAnalysisResultDto aiResult)
        {
            var table = new Table();
            table.AppendChild(GetStandardTableProperties());

            var headerRow = new TableRow();
            headerRow.Append(CreateTableCell("Критерий", true), CreateTableCell("Средний балл", true), CreateTableCell("Примечание", true));
            table.Append(headerRow);

            // Здесь также используем свойства Avg... вместо Average
            table.Append(CreateTableRow("Полезность программы", stats.AvgUsefulness.ToString("F1"), aiResult.UsefulnessComment ?? ""));
            table.Append(CreateTableRow("Практико-ориентированность", stats.AvgPracticality.ToString("F1"), aiResult.PracticalityComment ?? ""));
            table.Append(CreateTableRow("Доступность материалов", stats.AvgAccessibility.ToString("F1"), aiResult.AccessibilityComment ?? ""));
            table.Append(CreateTableRow("Взаимодействие с командой", stats.AvgInteraction.ToString("F1"), aiResult.InteractionComment ?? ""));
            table.Append(CreateTableRow("Вовлеченность", $"{stats.EngagementPercentage:F1}%", aiResult.EngagementComment ?? ""));

            return table;
        }

        private TableProperties GetStandardTableProperties()
        {
            return new TableProperties(
                new TableBorders(
                    new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
                ),
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
            );
        }

        private TableRow CreateTableRow(params string[] cellValues)
        {
            var row = new TableRow();
            for (int i = 0; i < cellValues.Length; i++)
            {
                row.Append(CreateTableCell(cellValues[i], i == 0));
            }
            return row;
        }

        private TableCell CreateTableCell(string text, bool isBold)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph();
            var run = new Run(new Text(text));
            if (isBold) run.RunProperties = new RunProperties(new Bold());

            var cellProperties = new TableCellProperties(
                new TableCellMargin
                {
                    TopMargin = new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                    BottomMargin = new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                    LeftMargin = new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                    RightMargin = new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa }
                }
            );

            paragraph.Append(run);
            cell.Append(cellProperties);
            cell.Append(paragraph);
            return cell;
        }
    }
}