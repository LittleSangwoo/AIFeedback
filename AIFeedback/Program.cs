using AIFeedback.Data;
using AIFeedback.Services;
using AIFeedback.Services.Excel;
using AIFeedback.Services.Report;
using AIFeedback.Services.Excel;
using AIFeedback.Services.LLM;
using AIFeedback.Services.LLM.Providers;
using AIFeedback.Services.Report;
using Microsoft.EntityFrameworkCore;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Регистрируем сервис настроек
builder.Services.AddScoped<ILlmSettingsService, LlmSettingsService>();

// Регистрация HttpClientFactory
//builder.Services.AddHttpClient();

builder.Services.AddHttpClient<ILLMProvider, DynamicLlmProvider>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();

        // 1. Пути к обоим сертификатам
        string rootCertPath = Path.Combine(AppContext.BaseDirectory, "Certificates", "russian_trusted_root_ca.cer");
        string subCertPath = Path.Combine(AppContext.BaseDirectory, "Certificates", "russian_trusted_sub_ca.cer"); // Твой новый файл

        var rootCert = new X509Certificate2(rootCertPath);
        var subCert = new X509Certificate2(subCertPath);

        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // 2. Строгая настройка доверия (без костылей)
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Clear();

            // Корневой - это наш фундамент доверия
            chain.ChainPolicy.CustomTrustStore.Add(rootCert);

            // Выпускающий кладем в ExtraStore (помогает построить мост между сервером и корнем)
            chain.ChainPolicy.ExtraStore.Add(subCert);

            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            // 3. Честно строим цепочку. Теперь она 100% сойдется.
            return chain.Build(cert);
        };

        return handler;
    });

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