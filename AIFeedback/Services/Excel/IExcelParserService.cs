namespace AIFeedback.Services.Excel
{
    public interface IExcelParserService
    {
        // Возвращает: (programName, listenerCount, numericAverages, allComments)
        Task<(string ProgramName, int ListenerCount, Dictionary<string, double> NumericAverages, List<string> AllComments)>
            ParseAsync(Stream fileStream);
    }
}
