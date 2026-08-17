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

        // =====================================
        // НОВЫЕ СВОЙСТВА ДЛЯ ГРАФИКА ТРЕНДА
        // =====================================
        public List<double> TrendValues { get; set; } = new List<double>();
        public List<string> TrendLabels { get; set; } = new List<string>();
        public string CorrelationMatrixJson { get; set; }
    }
}