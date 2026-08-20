using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AIFeedback.Models.DTOs
{
    public class AiAnalysisResultDto
    {
        // ==========================================
        // НОВЫЕ ПОЛЯ (ДЛЯ ДЕТАЛЬНОГО WORD-ОТЧЕТА)
        // ==========================================
        [JsonPropertyName("MetricsNotes")]
        public MetricsNotesDto MetricsNotes { get; set; } = new MetricsNotesDto();

        [JsonPropertyName("UnnecessaryTopics")]
        public string UnnecessaryTopics { get; set; } = "Неактуальных тем не выявлено.";

        [JsonPropertyName("TopicsToAdd")]
        public string TopicsToAdd { get; set; } = "Дополнений не зафиксировано.";

        [JsonPropertyName("Trajectory")]
        public TrajectoryDto Trajectory { get; set; } = new TrajectoryDto();

        // ==========================================
        // СТАРЫЕ ПОЛЯ (ДЛЯ ВЕБ-ДАШБОРДА)
        // ==========================================
        [JsonPropertyName("Sentiment")]
        public SentimentStats Sentiment { get; set; } = new SentimentStats();

        [JsonPropertyName("TopTopics")]
        public List<Topic> TopTopics { get; set; } = new List<Topic>();

        [JsonPropertyName("Conclusions")]
        public List<Conclusion> Conclusions { get; set; } = new List<Conclusion>();
    }

    // --- КЛАССЫ ДЛЯ НОВОГО ОТЧЕТА ---

    public class MetricsNotesDto
    {
        [JsonPropertyName("Usefulness")]
        public string Usefulness { get; set; } = "Оценка стабильна. Замечаний не выявлено.";

        [JsonPropertyName("Practicality")]
        public string Practicality { get; set; } = "Оценка стабильна. Замечаний не выявлено.";

        [JsonPropertyName("Accessibility")]
        public string Accessibility { get; set; } = "Оценка стабильна. Замечаний не выявлено.";

        [JsonPropertyName("Interaction")]
        public string Interaction { get; set; } = "Оценка стабильна. Замечаний не выявлено.";

        [JsonPropertyName("Engagement")]
        public string Engagement { get; set; } = "В целом, вовлеченность слушателей была на высоком уровне.";
    }

    public class TrajectoryDto
    {
        [JsonPropertyName("Relevance")]
        public string Relevance { get; set; } = "Программа актуальна и востребована среди слушателей.";

        [JsonPropertyName("Selection")]
        public string Selection { get; set; } = "Не требуется.";

        [JsonPropertyName("Additions")]
        public string Additions { get; set; } = "Рекомендаций по изменению программы нет.";

        [JsonPropertyName("Hours")]
        public string Hours { get; set; } = "Не требуется.";

        [JsonPropertyName("Format")]
        public string Format { get; set; } = "Не требуется.";
    }

    // --- КЛАССЫ ДЛЯ ДАШБОРДА ---

    public class SentimentStats
    {
        [JsonPropertyName("PositivePercent")]
        public double PositivePercent { get; set; }

        [JsonPropertyName("NeutralPercent")]
        public double NeutralPercent { get; set; }

        [JsonPropertyName("NegativePercent")]
        public double NegativePercent { get; set; }
    }

    public class Topic
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("MentionsCount")]
        public int MentionsCount { get; set; }

        [JsonPropertyName("IsRelevant")]
        public bool IsRelevant { get; set; }
    }

    public class Conclusion
    {
        [JsonPropertyName("Priority")]
        public string Priority { get; set; } = string.Empty;

        [JsonPropertyName("Action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("DataProof")]
        public string DataProof { get; set; } = string.Empty;
    }
}