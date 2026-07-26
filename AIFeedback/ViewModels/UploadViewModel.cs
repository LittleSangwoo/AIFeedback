namespace AIFeedback.ViewModels
{
    public class UploadViewModel
    {
        public IFormFile ExcelFile { get; set; }
        public string SelectedProvider { get; set; } // опционально
        public List<string> AvailableProviders { get; set; } // список имён из llm_providers.json

        public string? ErrorMessage { get; set; }
    }
}
