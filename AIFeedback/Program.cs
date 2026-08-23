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
builder.Services.AddScoped<ILlmSettingsService, LlmSettingsService>();


builder.Services.AddHttpClient<ILLMProvider, DynamicLlmProvider>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();

        string rootCertPath = Path.Combine(AppContext.BaseDirectory, "Certificates", "russian_trusted_root_ca.cer");
        string subCertPath = Path.Combine(AppContext.BaseDirectory, "Certificates", "russian_trusted_sub_ca.cer");

        var rootCert = new X509Certificate2(rootCertPath);
        var subCert = new X509Certificate2(subCertPath);

        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Clear();

            chain.ChainPolicy.CustomTrustStore.Add(rootCert);

            chain.ChainPolicy.ExtraStore.Add(subCert);

            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            return chain.Build(cert);
        };

        return handler;
    });

builder.Services.AddScoped<ILLMProviderFactory, LLMProviderFactory>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<AIFeedback.Services.LLM.Providers.LLMProviderFactory>();
builder.Services.AddScoped<AIFeedback.Services.LLM.ILLMProvider, AIFeedback.Services.LLM.DynamicLlmProvider>();
builder.Services.AddScoped<AIFeedback.Services.IAiService, AIFeedback.Services.AiService>();
builder.Services.AddScoped<AIFeedback.Data.IAnalysisResultRepository, AIFeedback.Data.AnalysisResultRepository>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>();

builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();
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

// Автоматическое применение миграций при запуске
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>(); // Замени на свой контекст, если он называется иначе
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при автоматической миграции базы данных.");
    }
}

app.Run();