using AIFeedback.Models.DTOs;

namespace AIFeedback.Services.Report
{
    public interface IReportService
    {
        // Генерирует Word-отчёт на основе данных анализа и возвращает поток
        Task<Stream> GenerateWordReportAsync(AiAnalysisResultDto analysis, string programName, int listenerCount);
    }
}
