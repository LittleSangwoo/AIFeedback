namespace AIFeedback.Services.LLM
{
    public interface ILLMProvider
    {
        // Temperature = 0.0 для детерминированности ответов (снижение субъективности)
        Task<string> AnalyzeTextAsync(string systemPrompt, string userPrompt, double temperature = 0.0, string providerName = null);

        string ProviderName { get; }
    }
}