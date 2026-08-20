using System;

namespace AIFeedback.Models
{
    public class ProgramSessionStats
    {
        public int Id { get; set; }
        public string ProgramName { get; set; } = string.Empty;

        public string TrainingPeriod { get; set; } = string.Empty;
        public int TotalListeners { get; set; }

        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

        // Цифровые метрики
        public double AvgUsefulness { get; set; }
        public double AvgPracticality { get; set; }
        public double AvgAccessibility { get; set; }
        public double AvgInteraction { get; set; }
        public double OverallSatisfaction { get; set; }
        public double EngagementPercentage { get; set; }

        // Результаты ИИ-аналитики (сохраняем сериализованный JSON)
        public string AiAnalysisJson { get; set; } = string.Empty;
    }
}