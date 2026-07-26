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

        // JSON-поля для текстового анализа (храним как строки)
        public string ThemesJson { get; set; } = string.Empty;
        public string SentimentJson { get; set; } = string.Empty;
        public string ProblemsJson { get; set; } = string.Empty;
        public string QuotesJson { get; set; } = string.Empty;
        public string RecommendationsJson { get; set; } = string.Empty;

        // Для отладки (опционально)
        public string? RawComments { get; set; }
    }
}
