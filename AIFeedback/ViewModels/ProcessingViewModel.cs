namespace AIFeedback.ViewModels
{
    public class ProcessingViewModel
    {
        public int AnalysisId { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public string? StatusMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
