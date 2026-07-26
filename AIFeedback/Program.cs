using AIFeedback.Data;
using AIFeedback.Services;
using AIFeedback.Services.LLM;
using AIFeedback.Services.LLM.Providers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

//// Игнорируем ошибки SSL для GigaChat (из-за российских сертификатов)
//builder.Services.AddHttpClient<ILLMProvider, DynamicLlmProvider>()
//    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
//    {
//        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
//    });

// Регистрируем сервис настроек
builder.Services.AddScoped<ILlmSettingsService, LlmSettingsService>();

// Регистрация HttpClientFactory
builder.Services.AddHttpClient();

// Фабрика и сервис
builder.Services.AddScoped<ILLMProviderFactory, LLMProviderFactory>();
builder.Services.AddScoped<IAiService, AiService>();

// Добавляем контекст БД (SQLite)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Регистрируем репозиторий
//builder.Services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>(); важно

// Регистрируем сервисы (заглушки для Разработчика 2)
//builder.Services.AddScoped<IExcelParserService, ExcelParserService>(); // важно будет реализован позже
//builder.Services.AddScoped<IReportService, ReportService>(); // важно будет реализован позже

// Наши сервисы
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<ILLMProviderFactory, LLMProviderFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
