using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIFeedback.Services.LLM.Providers
{
    public class GigaChatProvider : BaseLLMProvider
    {
        private readonly GigaChatOptions _options;
        private string _accessToken;
        private DateTime _tokenExpiry;
        public override string ProviderName => "GigaChat";

        public GigaChatProvider(HttpClient httpClient, ILogger<GigaChatProvider> logger,
                                 IOptions<GigaChatOptions> options)
            : base(httpClient, logger)
        {
            _options = options.Value;
            _httpClient.BaseAddress = new Uri(_options.ApiUrl);
        }

        public override async Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0, string providerName = null)
        {
            await EnsureTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                model = "GigaChat-Pro",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = temperature,
                max_tokens = 4096,
                stream = false
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var responseJson = await SendRequestAsync(request);
            return ExtractContentFromResponse(responseJson);
        }

        private async Task EnsureTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(5))
                return;

            using var authClient = new HttpClient();
            var authRequest = new HttpRequestMessage(HttpMethod.Post, _options.AuthUrl);
            authRequest.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.Secret}"))}");
            authRequest.Headers.Add("RqUID", Guid.NewGuid().ToString());
            authRequest.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
            });

            var response = await authClient.SendAsync(authRequest);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<GigaChatAuthResponse>(json);
            _accessToken = tokenData.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
        }

        protected override string ExtractContentFromResponse(string jsonResponse)
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }

        private class GigaChatAuthResponse
        {
            public string AccessToken { get; set; }
            public int ExpiresIn { get; set; }
        }
    }

    public class GigaChatOptions
    {
        public string AuthUrl { get; set; }
        public string ApiUrl { get; set; }
        public string ClientId { get; set; }
        public string Secret { get; set; }
    }
}
