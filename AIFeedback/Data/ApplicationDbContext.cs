using AIFeedback.Models;
using Microsoft.EntityFrameworkCore;

namespace AIFeedback.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<ProgramSessionStats> SessionStats { get; set; }
    }
}
