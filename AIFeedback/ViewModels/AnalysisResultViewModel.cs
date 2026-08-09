namespace AIFeedback.ViewModels
{
    public class AnalysisResultViewModel
    {
        public int Id { get; set; }

        public string SessionName { get; set; }

        // Средние баллы по 4 критериям
        public double AvgUtility { get; set; }
        public double AvgPractice { get; set; }
        public double AvgAccessibility { get; set; }
        public double AvgEngagement { get; set; }

        // Расчетный общий балл (округляем до 1 знака)
        public double GeneralScore => Math.Round((AvgUtility + AvgPractice + AvgAccessibility + AvgEngagement) / 4.0, 1);

        // Процент вовлеченности (ответы "Нет" на вопрос об отстраненности)
        public double EngagementPercent { get; set; }

        // Данные для столбчатой диаграммы (распределение оценок)
        public int Dist1to3 { get; set; }
        public int Dist4to7 { get; set; }
        public int Dist8to10 { get; set; }

        // JSON с инсайтами от ИИ
        public string AiInsightsJson { get; set; }

        // 1. Ссылка на скачивание сгенерированного Word/PDF отчета
        public string ReportDownloadUrl { get; set; } = string.Empty;

        // 2. Общая статистика для линейчатой диаграммы (Средние баллы)
        public double AvgUsefulness { get; set; }
        public double AvgPracticality { get; set; }
        //public double AvgAccessibility { get; set; }
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
