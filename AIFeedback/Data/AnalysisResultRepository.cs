
using Microsoft.EntityFrameworkCore;

namespace AIFeedback.Data
{
    public class AnalysisResultRepository : IAnalysisResultRepository
    {
        private readonly ApplicationDbContext _context;

        public AnalysisResultRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(AnalysisResult result)
        {
            await _context.AnalysisResults.AddAsync(result);
            await _context.SaveChangesAsync();
        }

        public async Task<AnalysisResult?> GetByIdAsync(int id)
        {
            return await _context.AnalysisResults.FindAsync(id);
        }

        public async Task<AnalysisResult?> GetLatestAsync(string programName)
        {
            return await _context.AnalysisResults
                .Where(r => r.ProgramName == programName)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync<AnalysisResult>();
        }

        public async Task<List<AnalysisResult>> GetAllAsync()
        {
            return await _context.AnalysisResults
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync<AnalysisResult>();
        }

        public async Task<List<AnalysisResult>> GetByProgramNameAsync(string programName)
        {
            return await _context.AnalysisResults
                .Where(r => r.ProgramName == programName)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync<AnalysisResult>();
        }
    }
}
