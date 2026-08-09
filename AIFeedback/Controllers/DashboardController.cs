using AIFeedback.Data;
using AIFeedback.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AIFeedback.Controllers
{

    public class DashboardController : Controller
    {
        private readonly IAnalysisResultRepository _repository;

        public DashboardController(IAnalysisResultRepository repository)
        {
            _repository = repository;
        }

        // Список всех обработанных программ
        public async Task<IActionResult> Index()
        {
            var results = await _repository.GetAllAsync();
            var viewModels = results.Select(r => new DashboardViewModel
            {
                Id = r.Id,
                ProgramName = r.ProgramName,
                ListenerCount = r.ListenerCount,
                CreatedAt = r.CreatedAt,
                UsefulnessAvg = r.UsefulnessAvg,
                AvailabilityAvg = r.AvailabilityAvg,
                PracticalityAvg = r.PracticalityAvg,
                InteractionAvg = r.InteractionAvg,
                EngagementYesPercent = r.EngagementYesPercent,
                OverallSatisfaction = r.OverallSatisfaction

            }).ToList();
            return View(viewModels);
        }

        // Детальный дашборд для конкретного анализа
        public async Task<IActionResult> Details(int id)
        {
            var result = await _repository.GetByIdAsync(id);
            if (result == null)
                return NotFound();

            var viewModel = new DashboardViewModel
            {
                Id = result.Id,
                ProgramName = result.ProgramName,
                ListenerCount = result.ListenerCount,
                CreatedAt = result.CreatedAt,
                UsefulnessAvg = result.UsefulnessAvg,
                AvailabilityAvg = result.AvailabilityAvg,
                PracticalityAvg = result.PracticalityAvg,
                InteractionAvg = result.InteractionAvg,
                EngagementYesPercent = result.EngagementYesPercent,
                OverallSatisfaction = result.OverallSatisfaction,
                AiAnalysis = new Models.DTOs.AiAnalysisResultDto
                {
                    Sentiment = JsonSerializer.Deserialize<Models.DTOs.SentimentStats>(result.SentimentJson) ?? new Models.DTOs.SentimentStats(),
                    TopTopics = JsonSerializer.Deserialize<List<Models.DTOs.Topic>>(result.ThemesJson) ?? new List<Models.DTOs.Topic>(),
                    Conclusions = JsonSerializer.Deserialize<List<Models.DTOs.Conclusion>>(result.RecommendationsJson) ?? new List<Models.DTOs.Conclusion>()
                }
            };
            return View(viewModel);
        }
    }
}
