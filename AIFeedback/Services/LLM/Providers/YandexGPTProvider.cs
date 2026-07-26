using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AIFeedback.Services.LLM.Providers
{
    public class YandexGPTProvider : BaseLLMProvider
    {
        private readonly YandexGPTOptions _options;
        public override string ProviderName => "YandexGPT";

        public YandexGPTProvider(HttpClient httpClient, ILogger<YandexGPTProvider> logger,
                                  IOptions<YandexGPTOptions> options)
            : base(httpClient, logger)
        {
            _options = options.Value;
            _httpClient.BaseAddress = new Uri(_options.ApiUrl);
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.IamToken);
        }

        public override async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0)
        {
            var payload = new
            {
                modelUri = $"gpt://{_options.FolderId}/yandexgpt-lite",
                completionOptions = new
                {
                    temperature = temperature,
                    maxTokens = 4096
                },
                messages = new[]
                {
                    new { role = "system", text = systemPrompt },
                    new { role = "user", text = userPrompt }
                }
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
            return doc.RootElement.GetProperty("result").GetProperty("alternatives")[0].GetProperty("message").GetProperty("text").GetString();
        }
    }

    public class YandexGPTOptions
    {
        public string ApiUrl { get; set; }
        public string IamToken { get; set; }
        public string FolderId { get; set; }
    }
}
