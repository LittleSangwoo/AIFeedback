using AIFeedback.Models.DTOs;

namespace AIFeedback.ViewModels
{
    public class ResultViewModel
    {
        // Общая информация
        public string ProgramName { get; set; }
        public string Period { get; set; }
        public string TrainingFormat { get; set; }
        public int ListenerCount { get; set; }
        public string Teachers { get; set; }

        // Числовые метрики (5 критериев)
        public Dictionary<string, double> Averages { get; set; } // ключи: "Полезность", "Доступность", "Практика", "Взаимодействие", "Вовлеченность"
        public double OverallSatisfaction { get; set; }

        // Распределение оценок (1-3, 4-7, 8-10) для каждого критерия
        public Dictionary<string, (int Low, int Mid, int High)> Distribution { get; set; }

        // Результаты ИИ-аналитики
        public AiAnalysisResultDto AiAnalysis { get; set; }

        // Для вовлечённости
        public double EngagementYesPercent { get; set; } // % ответивших "нет" на вопрос об отстранённости -> вовлечённость = 100 - %
        public List<string> EngagementReasons { get; set; } // причины отстранённости

        // Предложения слушателей
        public List<string> IrrelevantTopics { get; set; }
        public Dictionary<string, int> SuggestedTopics { get; set; } // тема -> кол-во человек
        public Dictionary<string, double> PreferredFormats { get; set; } // формат -> доля (0-1)

        // Итоговые выводы (генерация на основе данных)
        public List<Conclusion> Conclusions { get; set; }
    }

    public class Conclusion
    {
        public string Text { get; set; }
        public string Recommendation { get; set; }
        public string Priority { get; set; } // "Высокий", "Средний", "Низкий"
    }
}
