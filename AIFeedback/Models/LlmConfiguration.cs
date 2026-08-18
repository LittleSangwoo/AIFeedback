namespace AIFeedback.Models
{
    public class LlmConfiguration
    {
        public List<LlmProviderConfig> Providers { get; set; } = new List<LlmProviderConfig>();
        public string ActiveProviderId { get; set; } = string.Empty;
    }

    public class LlmProviderConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsLocal { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Scope { get; set; }
        public string AuthType { get; set; } = "OpenAI"; // "OpenAI" или "GigaChat"
    }
}
