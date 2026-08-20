using AIFeedback.Models.DTOs;

namespace AIFeedback.Services
{
    public interface IAiService
    {
        Task<AiAnalysisResultDto> AnalyzeFeedbackAsync(string systemPrompt, string userPrompt, string providerName = null);
        Task<string> AskQuestionAsync(string reportContext, string question, string providerName = null);
    }
}
