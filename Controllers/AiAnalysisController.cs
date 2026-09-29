using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WarehouseManagement.Authorization;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Services.Ai;

namespace WarehouseManagement.Controllers;

[Authorize(Policy = ApplicationPolicies.ViewReports)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiAnalysisController(InventoryAnalysisDataService dataService, IAiAnalysisService aiService,
    ILogger<AiAnalysisController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? fromDate, string? toDate, CancellationToken cancellationToken) =>
        View(await BuildModelAsync(fromDate, toDate, false, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai-analysis")]
    public async Task<IActionResult> Generate(string? fromDate, string? toDate, CancellationToken cancellationToken) =>
        View("Index", await BuildModelAsync(fromDate, toDate, true, cancellationToken));

    private async Task<AiAnalysisViewModel> BuildModelAsync(string? fromDate, string? toDate, bool generate,
        CancellationToken cancellationToken)
    {
        var config = aiService.Configuration;
        var model = new AiAnalysisViewModel
        {
            FromDate = fromDate, ToDate = toDate, Mode = config.Mode, ConfigurationMessage = config.Error
        };
        if (!AnalysisPeriod.TryCreate(fromDate, toDate, out var period, out var error))
            ModelState.AddModelError(string.Empty, error!);
        if (!ModelState.IsValid) return model;
        try
        {
            model.Data = await dataService.GetAsync(period, cancellationToken);
            if (model.Data.ProductCount == 0)
                model.ErrorMessage = "Không có hàng hóa để phân tích. Chưa gửi yêu cầu đến AI.";
            else if (generate)
            {
                if (!config.IsReady) { model.ErrorMessage = config.Error; return model; }
                var result = await aiService.AnalyzeAsync(model.Data, cancellationToken);
                model.Report = result.Success ? result.Text : null;
                model.ErrorMessage = result.Error;
                model.IsMock = result.IsMock;
                if (result.Success) model.GeneratedAtUtc = DateTime.UtcNow;
            }
        }
        catch (AnalysisDataException exception)
        { model.ErrorMessage = exception.Message; }
        catch (DbException)
        {
            // Avoid logging SQL statements, connection details, or any sensitive data.
            logger.LogWarning("AI analysis data query failed.");
            model.ErrorMessage = "Không thể lấy số liệu kho. Vui lòng thử lại sau.";
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return model;
    }
}
