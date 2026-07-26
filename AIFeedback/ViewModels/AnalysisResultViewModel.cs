namespace AIFeedback.ViewModels
{
    public class AnalysisResultViewModel
    {
        // 1. Ссылка на скачивание сгенерированного Word/PDF отчета
        public string ReportDownloadUrl { get; set; } = string.Empty;

        // 2. Общая статистика для линейчатой диаграммы (Средние баллы)
        public double AvgUsefulness { get; set; }
        public double AvgPracticality { get; set; }
        public double AvgAccessibility { get; set; }
        public double AvgInteraction { get; set; }
        public double OverallSatisfaction { get; set; }
        public double EngagementPercentage { get; set; } // Вовлеченность (Да/Нет)

        // 3. Данные для столбчатой диаграммы (Распределение оценок)
        public DistributionStats Distribution { get; set; } = new DistributionStats();

        // 4. Текстовые выводы от ИИ (Модуль 4)
        public List<string> Conclusions { get; set; } = new List<string>();

        // Для тепловой карты (матрица корреляций) потребуется отдельный класс или двумерный массив. 
        // Пока оставляем задел.
    }

    public class DistributionStats
    {
        public int Count1To3 { get; set; }
        public int Count4To7 { get; set; }
        public int Count8To10 { get; set; }
    }
}
