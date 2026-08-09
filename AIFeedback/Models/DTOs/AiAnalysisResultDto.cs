using System.Text.Json.Serialization;

namespace AIFeedback.Models.DTOs
{
    public class AiAnalysisResultDto
    {
        [JsonPropertyName("Sentiment")]
        public SentimentStats Sentiment { get; set; } = new SentimentStats();

        [JsonPropertyName("TopTopics")]
        public List<Topic> TopTopics { get; set; } = new List<Topic>();

        [JsonPropertyName("Conclusions")]
        public List<Conclusion> Conclusions { get; set; } = new List<Conclusion>();

        //public SentimentStats Sentiment { get; set; } = new SentimentStats();
        //public List<Topic> TopTopics { get; set; } = new List<Topic>();
        public List<string> UnrelevantTopics { get; set; } = new List<string>();
        //public List<Conclusion> Conclusions { get; set; } = new List<Conclusion>();
        // Добавить в AiAnalysisResultDto для Раздела 2
        public string UsefulnessComment { get; set; } = string.Empty;
        public string PracticalityComment { get; set; } = string.Empty;
        public string AccessibilityComment { get; set; } = string.Empty;
        public string InteractionComment { get; set; } = string.Empty;
        public string EngagementComment { get; set; } = string.Empty;
    }

    public class SentimentStats
    {
        public double PositivePercent { get; set; }
        public double NeutralPercent { get; set; }
        public double NegativePercent { get; set; }
    }

    public class Topic
    {
        //public string Name { get; set; } = string.Empty;
        public int MentionCount { get; set; }

        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("MentionsCount")]
        public int MentionsCount { get; set; }

        [JsonPropertyName("IsRelevant")]
        public bool IsRelevant { get; set; }
    }

    public class Conclusion
    {
        public string Text { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        //public string Priority { get; set; } = string.Empty;

        [JsonPropertyName("Priority")]
        public string Priority { get; set; }

        [JsonPropertyName("Action")]
        public string Action { get; set; }

        [JsonPropertyName("DataProof")]
        public string DataProof { get; set; }
    }
}
