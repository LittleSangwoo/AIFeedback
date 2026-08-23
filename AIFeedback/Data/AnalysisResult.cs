
﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIFeedback.Data
{
    public interface IAnalysisResultRepository
    {
        Task AddAsync(AnalysisResult result);
        Task<AnalysisResult?> GetByIdAsync(int id);
        Task<AnalysisResult?> GetLatestAsync(string programName);
        Task<List<AnalysisResult>> GetAllAsync();
        Task<List<AnalysisResult>> GetByProgramNameAsync(string programName);
    }
    public class AnalysisResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }


        public DateTime CreatedAt { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public int ListenerCount { get; set; }

        // Числовые метрики
        public double UsefulnessAvg { get; set; }
        public double AvailabilityAvg { get; set; }
        public double PracticalityAvg { get; set; }
        public double InteractionAvg { get; set; }
        public double EngagementYesPercent { get; set; }
        public double OverallSatisfaction { get; set; }
        public int Dist1to3 { get; set; }
        public int Dist4to7 { get; set; }
        public int Dist8to10 { get; set; }

        // JSON-поля для текстового анализа 
        public string ThemesJson { get; set; } = string.Empty;
        public int EngagedCount { get; set; }
        public int DetachedCount { get; set; }
        public string SentimentJson { get; set; } = string.Empty;
        public string ProblemsJson { get; set; } = string.Empty;
        public string QuotesJson { get; set; } = string.Empty;
        public string RecommendationsJson { get; set; } = string.Empty;
        // Медиана и стандартное отклонение по каждому критерию
        public double UsefulnessMedian { get; set; }
        public double PracticalityMedian { get; set; }
        public double AvailabilityMedian { get; set; }
        public double InteractionMedian { get; set; }

        public double UsefulnessStdDev { get; set; }
        public double PracticalityStdDev { get; set; }
        public double AvailabilityStdDev { get; set; }
        public double InteractionStdDev { get; set; }
        public string? ProviderName { get; set; }

        // Сколько дублирующихся анкет было исключено при разборе файла
        public int DuplicateRowsRemoved { get; set; }
        // Для отладки 
        public string? RawComments { get; set; }
        public string CorrelationMatrixJson { get; set; } = "null";
        public string SessionName { get; set; }
        public DateTime DateProcessed { get; set; }
        public double AvgUtility { get; set; }
        public double AvgPractice { get; set; }
        public double AvgAccessibility { get; set; }
        public double AvgEngagement { get; set; }
        public string AiInsightsJson { get; set; } // Для хранения JSON-ответа от LLM
        public long ProcessingTimeMs { get; set; } // В миллисекундах
        public string ScoresDistributionJson { get; set; }
        public string? TrendLabelsJson { get; set; }
        public string? TrendValuesJson { get; set; }
        public int FormatOfflineCount { get; set; }
        public int FormatMixedCount { get; set; }
        public int FormatOnlineCount { get; set; }
    }
}
