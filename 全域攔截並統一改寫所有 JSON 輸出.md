# 全域攔截並統一改寫所有 JSON 輸出

可以。要「全域攔截並統一改寫所有 JSON 輸出」而不去改每個端點，常見做法有三種，依你是 MVC 控制器或 Minimal API 而定：

# 1) MVC/Controllers：Result Filter（建議）

撰寫 `IAsyncResultFilter` 檢查動作回傳，如果是 `ObjectResult/JsonResult` 就包成你要的格式（例如 `{ success, data, traceId }`），並全域註冊濾器即可。

```csharp
public class WrapJsonResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext ctx, ResultExecutionDelegate next)
    {
        // 只處理會被 JSON 序列化的結果
        if (ctx.Result is ObjectResult obj && obj.Value is not null)
        {
            var traceId = ctx.HttpContext.TraceIdentifier;
            ctx.Result = new ObjectResult(new {
                success = true,
                data = obj.Value,
                traceId
            })
            { StatusCode = obj.StatusCode };
        }
        else if (ctx.Result is JsonResult json && json.Value is not null)
        {
            var traceId = ctx.HttpContext.TraceIdentifier;
            ctx.Result = new JsonResult(new {
                success = true,
                data = json.Value,
                traceId
            })
            { StatusCode = ctx.HttpContext.Response.StatusCode };
        }

        await next();
    }
}
```

註冊（.NET 6+）：

```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add<WrapJsonResultFilter>(); // 全域套用
});
```

ASP.NET Core 的「濾器」就是用來在動作前/後、結果前/後插入邏輯的官方機制。([Microsoft Learn][1])

> 補充：若只想統一序列化設定（如命名風格、忽略 null），可用 `AddJsonOptions`；但若要「包一層外殼」，用 Result Filter 較直接。關於格式化與 JSON 輸出可參考官方的「格式化」與 `SystemTextJsonOutputFormatter`。([Microsoft Learn][2])

# 2) Minimal APIs：Endpoint Filter（.NET 7+）

Minimal API 沒有 MVC 濾器，但有 **Endpoint Filters** 可在處理器前後攔截與改寫結果。

```csharp
public class WrapResultEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var result = await next(ctx);

        // 若處理器已回傳 IResult，盡量避免覆寫狀態碼；這裡示範統一包裝
        return Results.Json(new {
            success = true,
            data = result,
            traceId = ctx.HttpContext.TraceIdentifier
        });
    }
}

// 註冊到路由群組/端點
var api = app.MapGroup("/api");
api.AddEndpointFilter(new WrapResultEndpointFilter());
```

官方文件指出 Endpoint Filters 可以在處理器前後執行並「攔截回應行為」，用來實作跨端點的邏輯（驗證、記錄、統一回應等）。([Microsoft Learn][3])
Minimal API 回應模型與 `IResult` 的行為可見此文。([Microsoft Learn][4])

# 3) Middleware：最後手段（不若前兩者精準）

你也能寫一個中介軟體（Middleware）包住 `HttpResponse.Body`，在 `await next()` 後讀取輸出並改寫為你想要的 JSON，再寫回去；但要小心回應已開始送出就不能改 header/狀態碼、以及內容長度與串流重繪等細節。一般只有在你需要跨 MVC/Minimal API 之外的情境（例如靜態或第三方產生的 JSON）才會這樣做。([Microsoft Learn][5])

---

## 何者該選？

* **MVC 專案**：選 **Result Filter**，精準且簡潔。([Microsoft Learn][1])
* **Minimal API**：選 **Endpoint Filter**。([Microsoft Learn][3])
* **混合/特殊情境**：才考慮 **Middleware**，但需處理回應串流的細節風險。([Microsoft Learn][5])

如果你貼一下你目前的專案型態（MVC/Minimal API）與想統一的 JSON 結構，我可以直接給你最小可行的完整範本。

[1]: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-9.0&utm_source=chatgpt.com "Filters in ASP.NET Core"
[2]: https://learn.microsoft.com/en-us/aspnet/core/web-api/advanced/formatting?view=aspnetcore-9.0&utm_source=chatgpt.com "Format response data in ASP.NET Core Web API"
[3]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-9.0&utm_source=chatgpt.com "Filters in Minimal API apps"
[4]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-9.0&utm_source=chatgpt.com "Create responses in Minimal API applications"
[5]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-9.0&utm_source=chatgpt.com "ASP.NET Core Middleware"
