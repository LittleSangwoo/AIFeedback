using System.Text.Json.Serialization;

namespace AIFeedback.Models.DTOs
{
    public class AiAnalysisResultDto
    {
        //public SentimentStats Sentiment { get; set; } = new SentimentStats();
        public List<Topic> TopTopics { get; set; } = new List<Topic>();
        public List<string> UnrelevantTopics { get; set; } = new List<string>();
        public List<Conclusion> Conclusions { get; set; } = new List<Conclusion>();
        // Добавить в AiAnalysisResultDto для Раздела 2
        public string UsefulnessComment { get; set; } = string.Empty;
        public string PracticalityComment { get; set; } = string.Empty;
        public string AccessibilityComment { get; set; } = string.Empty;
        public string InteractionComment { get; set; } = string.Empty;
        public string EngagementComment { get; set; } = string.Empty;

        [JsonPropertyName("themes")]
        public List<ThemeDto> Themes { get; set; } = new();

        [JsonPropertyName("sentiment")]
        public SentimentStats Sentiment { get; set; } = new();

        [JsonPropertyName("problems")]
        public List<ProblemDto> Problems { get; set; } = new();

        [JsonPropertyName("recommendations")]
        public List<RecommendationDto> Recommendations { get; set; } = new();
    }

    public class ThemeDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("mentionsCount")]
        public int MentionsCount { get; set; }
        [JsonPropertyName("isRelevant")]
        public bool IsRelevant { get; set; }
    }

   

    public class ProblemDto
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("frequency")]
        public int Frequency { get; set; }
        [JsonPropertyName("quotes")]
        public List<string> Quotes { get; set; } = new();
    }

    public class RecommendationDto
    {
        [JsonPropertyName("priority")]
        public string Priority { get; set; } // "High", "Medium", "Low"
        [JsonPropertyName("action")]
        public string Action { get; set; }
        [JsonPropertyName("dataProof")]
        public string DataProof { get; set; } // Ссылка на данные (п. 2.5 Критериев)
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
