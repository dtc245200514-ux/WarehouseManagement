using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WarehouseManagement.Data;
using WarehouseManagement.Models.AiAnalysis;
using WarehouseManagement.Models.Enums;
using WarehouseManagement.Options;
using WarehouseManagement.Services.Ai;

static class MovementChecks
{
    public static void Run(Action<bool,string> check)
    {
        var data = new InventoryAnalysisData { SnapshotAtUtc=DateTime.UtcNow, OpeningQuantity=10, ClosingQuantity=7, OtherChange=-3 };
        check(data.NetStockChange == -3 && !data.HasPostedMovements, "Movement: no Posted does not imply no other stock change");
        check((data with { ClosingQuantity=13 }).NetStockChange == 3 && (data with { ClosingQuantity=10 }).NetStockChange == 0, "Movement: signed increase and unchanged balances calculated in backend");
        var product = new AnalysisProduct("fixture","fixture","kg",2.125m,3m,0m,0m);
        check(product.NetPostedChange == -0.875m && product.Unit == "kg", "Movement: per-product net preserves decimals and unit");
    }

    public static async Task Sql(ApplicationDbContext db, InventoryAnalysisData data, Action<bool,string> check)
    {
        check(data.NetStockChange == data.ImportedQuantity-data.ExportedQuantity+data.OtherChange, "Movement SQL: net balances reconcile including other changes");
        var ledger=db.InventoryTransactions.AsNoTracking().Where(t =>
            (!data.FromUtc.HasValue || t.OccurredAt>=data.FromUtc) && (!data.ToExclusiveUtc.HasValue || t.OccurredAt<data.ToExclusiveUtc));
        var imports=ledger.Where(t=>t.TransactionType==InventoryTransactionType.Import && t.ImportReceipt!.Status==ReceiptStatus.Posted);
        var exports=ledger.Where(t=>t.TransactionType==InventoryTransactionType.Export && t.ExportReceipt!.Status==ReceiptStatus.Posted);
        check(data.ImportedQuantity==(await imports.SumAsync(t=>(decimal?)t.QuantityChange)??0) &&
            data.ExportedQuantity==-(await exports.SumAsync(t=>(decimal?)t.QuantityChange)??0), "Movement SQL: only Posted imports/exports inside selected bounds");
        check(data.HasPostedMovements==(await imports.AnyAsync() || await exports.AnyAsync()), "Movement SQL: presence comes from Posted transactions, not current stock");
        foreach(var row in data.TopMovements.Take(1))
        {
            var product=await db.Products.AsNoTracking().SingleAsync(p=>p.Code==row.Code);
            var actual=(await imports.Where(t=>t.ProductId==product.Id).SumAsync(t=>(decimal?)t.QuantityChange)??0)+
                (await exports.Where(t=>t.ProductId==product.Id).SumAsync(t=>(decimal?)t.QuantityChange)??0);
            check(actual==row.NetPostedChange && product.Unit==row.Unit, "Movement SQL: notable product net and unit match source ledger");
        }
        var handler=new Capture();
        var adapter=new GeminiAnalysisAdapter(new HttpClient(handler),NullLogger<GeminiAnalysisAdapter>.Instance);
        var result=await adapter.AnalyzeAsync(new AiOptions {Provider="Gemini",Model="test-model",ApiKey="fixture-key"},InventoryAnalysisPrompt.Input(data),default);
        using var body=JsonDocument.Parse(handler.Body!);
        var input=body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
        check(result.Success && input==InventoryAnalysisPrompt.Input(data) && input.Contains(JsonSerializer.Serialize(data)), "Movement SQL: exact real DTO including computed changes reaches fake Gemini transport");
    }

    private sealed class Capture:HttpMessageHandler
    {
        public string? Body {get;private set;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var content="{\"name\":\"models/test-model\",\"supportedGenerationMethods\":[\"generateContent\"],\"outputTokenLimit\":4000}";
            if(request.Method==HttpMethod.Post)
            {
                Body=await request.Content!.ReadAsStringAsync(token);
                content="{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Fixture only, not an AI quality evaluation.\"}]}}]}";
            }
            return new(HttpStatusCode.OK){Content=new StringContent(content)};
        }
    }
}
