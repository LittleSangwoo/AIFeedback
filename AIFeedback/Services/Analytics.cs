using System;
using System.Collections.Generic;
using System.Linq;

namespace AIFeedback.Services.Analytics
{
    public static class MetricsCalculator
    {
        // Подсчет среднего арифметического
        public static double CalculateAverage(IEnumerable<int> scores)
        {
            if (scores == null || !scores.Any()) return 0;
            return Math.Round(scores.Average(), 1);
        }

        // Подсчет медианы
        public static double CalculateMedian(IEnumerable<int> scores)
        {
            if (scores == null || !scores.Any()) return 0;

            var sortedList = scores.OrderBy(n => n).ToList();
            int count = sortedList.Count;
            int itemIndex = count / 2;

            if (count % 2 == 0)
            {
                return Math.Round((sortedList[itemIndex] + sortedList[itemIndex - 1]) / 2.0, 1);
            }

            return sortedList[itemIndex];
        }

        // Подсчет стандартного отклонения
        public static double CalculateStandardDeviation(IEnumerable<int> scores)
        {
            if (scores == null || !scores.Any()) return 0;

            double average = scores.Average();
            double sumOfSquaresOfDifferences = scores.Select(val => (val - average) * (val - average)).Sum();
            return Math.Round(Math.Sqrt(sumOfSquaresOfDifferences / scores.Count()), 2);
        }

        // Процентное распределение по корзинам (1-3, 4-7, 8-10)
        public static Dictionary<string, double> CalculateBuckets(IEnumerable<int> scores)
        {
            var result = new Dictionary<string, double>
            {
                { "1-3", 0 },
                { "4-7", 0 },
                { "8-10", 0 }
            };

            if (scores == null || !scores.Any()) return result;

            int total = scores.Count();
            result["1-3"] = Math.Round(scores.Count(s => s >= 1 && s <= 3) / (double)total * 100, 1);
            result["4-7"] = Math.Round(scores.Count(s => s >= 4 && s <= 7) / (double)total * 100, 1);
            result["8-10"] = Math.Round(scores.Count(s => s >= 8 && s <= 10) / (double)total * 100, 1);

            return result;
        }

        // Подсчет вовлеченности (Да/Нет)
        public static double CalculateEngagementPercentage(IEnumerable<string> answers, string targetAnswer = "Да")
        {
            if (answers == null || !answers.Any()) return 0;

            int total = answers.Count();
            int targetCount = answers.Count(a => string.Equals(a.Trim(), targetAnswer, StringComparison.OrdinalIgnoreCase));

            return Math.Round(targetCount / (double)total * 100, 1);
        }
    }
}