using System.Text;
using System.Text.Json;

namespace AIFeedback.Services.LLM.Providers
{
    public class OpenAiCompatibleProvider : BaseLLMProvider
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _providerName;

        public OpenAiCompatibleProvider(HttpClient httpClient, ILogger<OpenAiCompatibleProvider> logger,
                                         string baseUrl, string apiKey, string model, string providerName = null)
            : base(httpClient, logger)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
            _model = model;
            _providerName = providerName ?? "OpenAI-Compatible";
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public override string ProviderName => _providerName;

        public override async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0)
        {
            var payload = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = temperature,
                max_tokens = 4096,
                stream = false
            };
            var request = new HttpRequestMessage(HttpMethod.Post, "")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            var responseJson = await SendRequestAsync(request);
            return ExtractContentFromResponse(responseJson);
        }

        protected override string ExtractContentFromResponse(string jsonResponse)
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
    }
}
