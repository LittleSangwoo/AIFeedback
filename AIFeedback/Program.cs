using AIFeedback.Data;
using AIFeedback.Services;
using AIFeedback.Services.Excel;
using AIFeedback.Services.Report;
using AIFeedback.Services.LLM;
using AIFeedback.Services.LLM.Providers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Регистрируем сервис настроек
builder.Services.AddScoped<ILlmSettingsService, LlmSettingsService>();

// Регистрация HttpClientFactory
builder.Services.AddHttpClient();

// Фабрика и сервис ИИ
builder.Services.AddScoped<ILLMProviderFactory, LLMProviderFactory>();
builder.Services.AddScoped<IAiService, AiService>();


// Регистрация фабрики провайдеров (если она используется через DI)
builder.Services.AddScoped<AIFeedback.Services.LLM.Providers.LLMProviderFactory>();

builder.Services.AddScoped<AIFeedback.Services.LLM.ILLMProvider, AIFeedback.Services.LLM.DynamicLlmProvider>();

// Если IAiService тоже еще не зарегистрирован, убедись, что есть эта строка:
builder.Services.AddScoped<AIFeedback.Services.IAiService, AIFeedback.Services.AiService>();

// Регистрация репозитория для работы с БД
builder.Services.AddScoped<AIFeedback.Data.IAnalysisResultRepository, AIFeedback.Data.AnalysisResultRepository>();

// Добавляем контекст БД (SQLite)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Регистрируем репозиторий
builder.Services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>();

// РЕГИСТРИРУЕМ СЕРВИСЫ РАЗРАБОТЧИКА 2 (ЭТО МЫ!)
builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseStaticFiles();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();