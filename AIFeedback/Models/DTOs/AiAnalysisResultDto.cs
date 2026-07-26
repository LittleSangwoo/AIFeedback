namespace AIFeedback.Models.DTOs
{
    public class AiAnalysisResultDto
    {
        public SentimentStats Sentiment { get; set; } = new SentimentStats();
        public List<Topic> TopTopics { get; set; } = new List<Topic>();
        public List<string> UnrelevantTopics { get; set; } = new List<string>();
        public List<Conclusion> Conclusions { get; set; } = new List<Conclusion>();
    }

    public class SentimentStats
    {
        public double PositivePercent { get; set; }
        public double NeutralPercent { get; set; }
        public double NegativePercent { get; set; }
    }

    public class Topic
    {
        public string Name { get; set; } = string.Empty;
        public int MentionCount { get; set; }
    }

    public class Conclusion
    {
        public string Text { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }
}
