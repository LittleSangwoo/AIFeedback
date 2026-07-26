using AIFeedback.Models;
using Microsoft.EntityFrameworkCore;

namespace AIFeedback.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<AnalysisResult> AnalysisResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка для SQLite (можно сохранить как текст)
            modelBuilder.Entity<AnalysisResult>()
                .Property(e => e.ThemesJson)
                .HasColumnType("TEXT");
            modelBuilder.Entity<AnalysisResult>()
                .Property(e => e.SentimentJson)
                .HasColumnType("TEXT");
            modelBuilder.Entity<AnalysisResult>()
                .Property(e => e.ProblemsJson)
                .HasColumnType("TEXT");
            modelBuilder.Entity<AnalysisResult>()
                .Property(e => e.QuotesJson)
                .HasColumnType("TEXT");
            modelBuilder.Entity<AnalysisResult>()
                .Property(e => e.RecommendationsJson)
                .HasColumnType("TEXT");

            base.OnModelCreating(modelBuilder);
        }
    }
}
