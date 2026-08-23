using System.Collections.Generic;

namespace AIFeedback.Services.Excel
{
    public class ExcelParseResult
    {
        public string ProgramName { get; set; }
        public int ListenerCount { get; set; }

        public Dictionary<string, double> NumericAverages { get; set; } = new();
        public Dictionary<string, double> NumericMedians { get; set; } = new();
        public Dictionary<string, double> NumericStdDeviations { get; set; } = new();

        public List<string> AllComments { get; set; } = new();

        public int Dist1to3 { get; set; }
        public int Dist4to7 { get; set; }
        public int Dist8to10 { get; set; }

        public string CorrelationMatrixJson { get; set; } = "null";
        public string ScoresDistributionJson { get; set; } = "{}";

        public int FormatOffline { get; set; }
        public int FormatMixed { get; set; }
        public int FormatOnline { get; set; }

        public int EngagedCount { get; set; }
        public int DetachedCount { get; set; }

        public bool ParseSuccess { get; set; } = true;
        public int DuplicateRowsRemoved { get; set; }
    }
}