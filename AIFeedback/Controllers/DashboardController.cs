using AIFeedback.Data;
using AIFeedback.Models.DTOs;
using AIFeedback.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using DtoConclusion = AIFeedback.Models.DTOs.Conclusion;

namespace AIFeedback.Controllers
{

    public class DashboardController : Controller
    {
        private readonly IAnalysisResultRepository _repository;
        private readonly ILogger<DashboardController> _logger; // добавить

        public DashboardController(IAnalysisResultRepository repository, ILogger<DashboardController> logger)
        {
            _repository = repository;
            _logger = logger; // добавить
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
        //public async Task<IActionResult> Details(int id)
        //{
        //    var result = await _repository.GetByIdAsync(id);
        //    if (result == null)
        //        return NotFound();

        //    var viewModel = new DashboardViewModel
        //    {
        //        Id = result.Id,
        //        ProgramName = result.ProgramName,
        //        ListenerCount = result.ListenerCount,
        //        CreatedAt = result.CreatedAt,
        //        UsefulnessAvg = result.UsefulnessAvg,
        //        AvailabilityAvg = result.AvailabilityAvg,
        //        PracticalityAvg = result.PracticalityAvg,
        //        InteractionAvg = result.InteractionAvg,
        //        EngagementYesPercent = result.EngagementYesPercent,
        //        OverallSatisfaction = result.OverallSatisfaction,
        //        AiAnalysis = new Models.DTOs.AiAnalysisResultDto
        //        {
        //            Sentiment = JsonSerializer.Deserialize<Models.DTOs.SentimentStats>(result.SentimentJson) ?? new Models.DTOs.SentimentStats(),
        //            TopTopics = JsonSerializer.Deserialize<List<Models.DTOs.Topic>>(result.ThemesJson) ?? new List<Models.DTOs.Topic>(),
        //            Conclusions = JsonSerializer.Deserialize<List<Models.DTOs.Conclusion>>(result.RecommendationsJson) ?? new List<Models.DTOs.Conclusion>()
        //        }
        //    };
        //    return View(viewModel);
        //}
        public async Task<IActionResult> Details(int id)
        {
            var result = await _repository.GetByIdAsync(id);
            if (result == null) return NotFound();

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
                AiAnalysis = new AiAnalysisResultDto
                {
                    Sentiment = JsonSerializer.Deserialize<SentimentStats>(result.SentimentJson) ?? new SentimentStats(),
                    TopTopics = JsonSerializer.Deserialize<List<Topic>>(result.ThemesJson) ?? new List<Topic>(),
                    Conclusions = JsonSerializer.Deserialize<List<DtoConclusion>>(result.RecommendationsJson) ?? new List<DtoConclusion>()
                },
                Dist1to3 = 2,   // пример: 2 человека поставили 1–3
                Dist4to7 = 8,
                Dist8to10 = 10,
                HistoryData = new List<double> { 8.1, 8.5, 8.2, result.OverallSatisfaction },
                HistoryLabels = new List<string> { "Янв 2025", "Мар 2025", "Май 2025", "Текущий" }
            };

            // Логирование для отладки
            _logger.LogInformation("Dashboard Details: Program={Program}, Avg={Avg}, Sentiment={Sent}",
                viewModel.ProgramName, viewModel.OverallSatisfaction, viewModel.AiAnalysis?.Sentiment?.PositivePercent);

            return View(viewModel);
        }
    }
}
