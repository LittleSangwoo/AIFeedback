using AIFeedback.Models.DTOs;
using System;
using System.Collections.Generic;

namespace AIFeedback.ViewModels
{
    public class DashboardViewModel
    {
        public int Id { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public int ListenerCount { get; set; }
        public DateTime CreatedAt { get; set; }

        // Числовые метрики
        public double UsefulnessAvg { get; set; }
        public double AvailabilityAvg { get; set; }
        public double PracticalityAvg { get; set; }
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
        // Результаты ИИ-анализа
        public AiAnalysisResultDto? AiAnalysis { get; set; }

        // Для визуализаций
        public Dictionary<string, double> CriteriaAverages => new()
        {
            ["Полезность"] = UsefulnessAvg,
            ["Доступность"] = AvailabilityAvg,
            ["Практико-ориентированность"] = PracticalityAvg,
            ["Взаимодействие с КУ"] = InteractionAvg,
            ["Вовлеченность"] = EngagementYesPercent
        };

        public int Dist1to3 { get; set; }
        public int Dist4to7 { get; set; }
        public int Dist8to10 { get; set; }

        public List<double> TrendValues { get; set; } = new List<double>();
        public List<string> TrendLabels { get; set; } = new List<string>();
        public string CorrelationMatrixJson { get; set; }
    }
}