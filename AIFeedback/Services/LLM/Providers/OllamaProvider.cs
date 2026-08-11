using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AIFeedback.Services.LLM.Providers
{
    public class OllamaProvider : BaseLLMProvider
    {
        private readonly OllamaOptions _options;
        public override string ProviderName => "llama3";

        public OllamaProvider(HttpClient httpClient, ILogger<OllamaProvider> logger,
                              IOptions<OllamaOptions> options)
            : base(httpClient, logger)
        {
            _options = options.Value;
            _httpClient.BaseAddress = new Uri(_options.ApiUrl);
        }

        public override async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0, string providerName = null)
        {
            var payload = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = false,
                options = new { temperature = temperature }
            };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            var responseJson = await SendRequestAsync(request);
            return ExtractContentFromResponse(responseJson);
        }

        protected override string ExtractContentFromResponse(string jsonResponse)
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString();
        }
    }

    public class OllamaOptions
    {
        public string ApiUrl { get; set; }
        public string Model { get; set; }
    }
}
