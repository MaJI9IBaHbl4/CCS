using CustomCodeSystem.Dtos;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CustomCodeSystem;

public static class GOR_API
{
    private const string APP_NAME = "CustomCodeManager";

    private static readonly HttpClient _http = new HttpClient();

    private static string GetBaseUrlOrThrow()
    {
        var baseUrl = AppState.GetBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("BaseUrl is empty. Call AppState.SetBaseUrl(...) before API calls.");

        return baseUrl.Trim().TrimEnd('/');
    }

    public static async Task<(bool success, SessionInfoDto? data, string errorText)> GetSessionAsync(
        string id,
        CancellationToken ct = default)
    {
        try
        {
            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v1/session/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(APP_NAME)}";
            
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("text/plain"); // как в curl

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            var data = System.Text.Json.JsonSerializer.Deserialize<SessionInfoDto>(
                body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return data is null
                ? (false, null, "Empty/invalid JSON (deserialized to null).")
                : (true, data, "");
        }
        catch (Exception ex)
        {
            return (false, null, ex.ToString());
        }
    }

    // ============================================================
    // POST /api/v2/{sessionId}/boxes/child
    // return: (success, errorText)
    // ============================================================
    public static async Task<(bool success, string errorText)> CreateChildBoxAsync(
        string sessionId,
        string boxId,
        int size,
        string[] operationalCodes,
        string operationCode,
        CancellationToken ct = default)
    {
        try
        {
            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v2/{sessionId}/boxes/child";

            var payload = new
            {
                BoxId = boxId,
                Size = size,
                OperationalCodes = operationalCodes ?? Array.Empty<string>(),
                OperationCode = operationCode
            };

            var json = JsonSerializer.Serialize(payload);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");

            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            // Если хочешь — тут можно дополнительно проверить, что ответ реально JSON с Id/BoxNumber.
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.ToString());
        }
    }

    public static async Task<(bool success, BoxTreeInfoDto? boxInfo, string errorText)> GetBoxTreeInfoAsync(
       string sessionId,
       string boxId,
       CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, null, "sessionId is empty.");
            if (string.IsNullOrWhiteSpace(boxId))
                return (false, null, "boxId is empty.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v3/{Uri.EscapeDataString(sessionId)}/boxes/tree";
            var json = JsonSerializer.Serialize(new[] { boxId });

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);

            // =========================
            // Variant #1: HTTP 204 -> not found
            // =========================
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) // 204
            {
                return (true, null, ""); // success=true, boxInfo=null => not found
            }

            // =========================
            // Variant #2: HTTP != 200 and != 204 -> error
            // =========================
            if (resp.StatusCode != System.Net.HttpStatusCode.OK) // not 200
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct);
                return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {errBody}");
            }

            // =========================
            // Variant #3: HTTP 200 -> parse
            // =========================
            var body = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("Boxes", out var boxes) ||
                boxes.ValueKind != JsonValueKind.Array ||
                boxes.GetArrayLength() == 0)
            {
                // 200 пришел, но структура не та => считаем ошибкой
                return (false, null, "HTTP 200 but response JSON does not contain Boxes[0].");
            }

            var box0 = boxes[0];

            var result = new BoxTreeInfoDto();

            // Box fields
            if (box0.TryGetProperty("Id", out var el) && el.ValueKind == JsonValueKind.String)
                result.Id = el.GetString();

            if (box0.TryGetProperty("ResultById", out el) && el.ValueKind == JsonValueKind.String)
                result.ResultById = el.GetString();

            if (box0.TryGetProperty("Batch", out el) && el.ValueKind == JsonValueKind.String)
                result.Batch = el.GetString();

            if (box0.TryGetProperty("Code", out el) && el.ValueKind == JsonValueKind.String)
                result.Code = el.GetString();

            if (box0.TryGetProperty("BoxNumber", out el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                    result.BoxNumber = v;
                else if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var vs))
                    result.BoxNumber = vs;
            }

            if (box0.TryGetProperty("Size", out el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                    result.Size = v;
                else if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var vs))
                    result.Size = vs;
            }

            // Products
            if (box0.TryGetProperty("Products", out var productsEl) && productsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in productsEl.EnumerateArray())
                {
                    var prod = new BoxTreeProductDto();

                    if (p.TryGetProperty("Id", out el) && el.ValueKind == JsonValueKind.String)
                        prod.Id = el.GetString();

                    if (p.TryGetProperty("Code", out el) && el.ValueKind == JsonValueKind.String)
                        prod.Code = el.GetString();

                    if (p.TryGetProperty("SerialNumber", out el) && el.ValueKind == JsonValueKind.String)
                        prod.SerialNumber = el.GetString();

                    if (p.TryGetProperty("OperationalNumber", out el) && el.ValueKind == JsonValueKind.String)
                        prod.OperationalNumber = el.GetString();

                    if (p.TryGetProperty("CaseSerialNumber", out el) && el.ValueKind == JsonValueKind.String)
                        prod.CaseSerialNumber = el.GetString();

                    if (p.TryGetProperty("ConfigurationMetadata", out el))
                    {
                        if (el.ValueKind == JsonValueKind.String)
                            prod.ConfigurationMetadata = el.GetString();
                        else if (el.ValueKind == JsonValueKind.Null)
                            prod.ConfigurationMetadata = null;
                        else
                            prod.ConfigurationMetadata = el.ToString();
                    }

                    result.Products.Add(prod);
                }
            }

            return (true, result, "");
        }
        catch (Exception ex)
        {
            return (false, null, ex.ToString());
        }
    }

    public static async Task<(bool success, List<BoxTreeInfoDto>? boxesInfo, string errorText)> GetBoxesTreeInfoAsync(
        string sessionId,
        List<string> boxIds,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, null, "sessionId is empty.");

            if (boxIds is null || boxIds.Count == 0)
                return (false, null, "boxIds is empty.");

            // (опционально) отфильтровать пустые и убрать дубликаты
            var ids = boxIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (ids.Count == 0)
                return (false, null, "boxIds contains only empty values.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v3/{Uri.EscapeDataString(sessionId)}/boxes/tree";
            var json = JsonSerializer.Serialize(ids);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);

            // =========================
            // Variant #1: HTTP 204 -> not found
            // =========================
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) // 204
            {
                return (true, new List<BoxTreeInfoDto>(), ""); // success=true, empty list => not found
            }

            // =========================
            // Variant #2: HTTP != 200 and != 204 -> error
            // =========================
            if (resp.StatusCode != System.Net.HttpStatusCode.OK) // not 200
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct);
                return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {errBody}");
            }

            // =========================
            // Variant #3: HTTP 200 -> parse all Boxes
            // =========================
            var body = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("Boxes", out var boxesEl) ||
                boxesEl.ValueKind != JsonValueKind.Array)
            {
                return (false, null, "HTTP 200 but response JSON does not contain Boxes array.");
            }

            var results = new List<BoxTreeInfoDto>();

            foreach (var boxEl in boxesEl.EnumerateArray())
            {
                // иногда в массиве могут быть null/не-объекты — пропускаем
                if (boxEl.ValueKind != JsonValueKind.Object)
                    continue;

                var result = new BoxTreeInfoDto();

                // Box fields
                if (boxEl.TryGetProperty("Id", out var el) && el.ValueKind == JsonValueKind.String)
                    result.Id = el.GetString();

                if (boxEl.TryGetProperty("ResultById", out el) && el.ValueKind == JsonValueKind.String)
                    result.ResultById = el.GetString();

                if (boxEl.TryGetProperty("Batch", out el) && el.ValueKind == JsonValueKind.String)
                    result.Batch = el.GetString();

                if (boxEl.TryGetProperty("Code", out el) && el.ValueKind == JsonValueKind.String)
                    result.Code = el.GetString();

                if (boxEl.TryGetProperty("BoxNumber", out el))
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                        result.BoxNumber = v;
                    else if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var vs))
                        result.BoxNumber = vs;
                }

                if (boxEl.TryGetProperty("Size", out el))
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                        result.Size = v;
                    else if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var vs))
                        result.Size = vs;
                }

                // Products
                if (boxEl.TryGetProperty("Products", out var productsEl) && productsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in productsEl.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.Object)
                            continue;

                        var prod = new BoxTreeProductDto();

                        if (p.TryGetProperty("Id", out el) && el.ValueKind == JsonValueKind.String)
                            prod.Id = el.GetString();

                        if (p.TryGetProperty("Code", out el) && el.ValueKind == JsonValueKind.String)
                            prod.Code = el.GetString();

                        if (p.TryGetProperty("SerialNumber", out el) && el.ValueKind == JsonValueKind.String)
                            prod.SerialNumber = el.GetString();

                        if (p.TryGetProperty("OperationalNumber", out el) && el.ValueKind == JsonValueKind.String)
                            prod.OperationalNumber = el.GetString();

                        if (p.TryGetProperty("CaseSerialNumber", out el) && el.ValueKind == JsonValueKind.String)
                            prod.CaseSerialNumber = el.GetString();

                        if (p.TryGetProperty("ConfigurationMetadata", out el))
                        {
                            if (el.ValueKind == JsonValueKind.String)
                                prod.ConfigurationMetadata = el.GetString();
                            else if (el.ValueKind == JsonValueKind.Null)
                                prod.ConfigurationMetadata = null;
                            else
                                prod.ConfigurationMetadata = el.ToString();
                        }

                        result.Products.Add(prod);
                    }
                }

                results.Add(result);
            }

            // Если хочешь считать “200, но пусто” как not found (аналог 204)
            // можно вернуть success=true и пустой список:
            // if (results.Count == 0) return (true, results, "");

            return (true, results, "");
        }
        catch (Exception ex)
        {
            return (false, null, ex.ToString());
        }
    }

    // ============================================================
    // PUT /api/v1/testtool/{sessionId}/metadataVariation/{key}
    // Body: { "CustomSerial": "..." }
    // return: (success, errorText)
    // ============================================================
    public static async Task<(bool success, string errorText)> PutMetadataVariationCustomSerialAsync(
        string sessionId,
        string key,
        string customSerial,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, "sessionId is empty.");
            if (string.IsNullOrWhiteSpace(key))
                return (false, "key is empty.");

            // customSerial может быть пустым — зависит от бизнес-логики. Если нельзя — раскомментируй:
            // if (string.IsNullOrWhiteSpace(customSerial)) return (false, "customSerial is empty.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v1/testtool/{Uri.EscapeDataString(sessionId)}/metadataVariation/{Uri.EscapeDataString(key)}";

            var payload = new { CustomSerial = customSerial ?? "" };
            var json = JsonSerializer.Serialize(payload);

            using var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.ToString());
        }
    }

    // ============================================================
    // POST /api/v1/{sessionId}/tasks
    // Body: { "OperationCode": "..." }
    // 200: { "TaskId": 123 }
    // 400: "Operation code not found"
    // return: (success, taskId, errorText)
    // ============================================================
    public static async Task<(bool success, int? taskId, string errorText)> StartTaskAsync(
        string sessionId,
        string operationCode,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, null, "sessionId is empty.");
            if (string.IsNullOrWhiteSpace(operationCode))
                return (false, null, "operationCode is empty.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v1/{Uri.EscapeDataString(sessionId)}/tasks";

            var payload = new { OperationCode = operationCode };
            var json = JsonSerializer.Serialize(payload);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            // 200 -> parse TaskId
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);

                    if (doc.RootElement.TryGetProperty("TaskId", out var el))
                    {
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id))
                            return (true, id, "");

                        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var ids))
                            return (true, ids, "");
                    }

                    return (false, null, $"HTTP 200 but response JSON does not contain valid TaskId. Body: {body}");
                }
                catch (Exception parseEx)
                {
                    return (false, null, $"HTTP 200 but failed to parse JSON. {parseEx.Message}. Body: {body}");
                }
            }

            // 400 -> текстовая ошибка (но на всякий случай возвращаем тело как есть)
            if (resp.StatusCode == HttpStatusCode.BadRequest)
            {
                // body может быть: "Operation code not found"
                var msg = body?.Trim();
                if (string.IsNullOrWhiteSpace(msg))
                    msg = "BadRequest (400).";
                return (false, null, msg);
            }

            // other codes
            return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }
        catch (Exception ex)
        {
            return (false, null, ex.ToString());
        }
    }
    // ============================================================
    // POST /api/v1/{sessionId}/actions/operationalcode
    // Body:
    // {
    //   "TaskId": 0,                 (required)
    //   "OperationalCode": "string", (required)
    //   "Passed": true,              (required)
    //   "DocumentationCode": "string", (optional)
    //   "Comment": "string"            (optional)
    // }
    // return: (success, errorText)
    // ============================================================
    public static async Task<(bool success, string errorText)> CreateActionByOperationalCodeAsync(
        string sessionId,
        int taskId,
        string operationalCode,
        bool passed,
        string? documentationCode = null,
        string? comment = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, "sessionId is empty.");
            if (taskId <= 0)
                return (false, "taskId must be > 0.");
            if (string.IsNullOrWhiteSpace(operationalCode))
                return (false, "operationalCode is empty.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v1/{Uri.EscapeDataString(sessionId)}/actions/operationalcode";

            // Важно: не отправляем optional поля если они null/empty
            var payload = new
            {
                TaskId = taskId,
                OperationalCode = operationalCode,
                Passed = passed,
                DocumentationCode = string.IsNullOrWhiteSpace(documentationCode) ? null : documentationCode,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            // Обычно 200/201 = success. Если у вас строго 200 — можно оставить только OK.
            if (resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Created)
                return (true, "");

            return (false, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }
        catch (Exception ex)
        {
            return (false, ex.ToString());
        }


    }


    // ============================================================
    // GET /api/v2/{sessionId}/operationcode/{operationCode}
    // 200:
    // {
    //   "Id": 86024,
    //   "Code": "2K32850213",
    //   "DoesRequireLot": true,
    //   "Description": "Rankinis testavimas",
    //   "MandatoryOperations": "AK32850204,",
    //   "Repeatable": true,
    //   "Disabled": true,
    //   "Quota": 1,
    //   "Additional": true
    // }
    // return: (success, data, errorText)
    // ============================================================
    public static async Task<(bool success, OperationCodeInfoDto? data, string errorText)> GetOperationCodeAsync(
        string sessionId,
        string operationCode,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, null, "sessionId is empty.");
            if (string.IsNullOrWhiteSpace(operationCode))
                return (false, null, "operationCode is empty.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v2/{Uri.EscapeDataString(sessionId)}/operationcode/{Uri.EscapeDataString(operationCode)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.StatusCode != HttpStatusCode.OK)
                return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            OperationCodeInfoDto? data;
            try
            {
                data = JsonSerializer.Deserialize<OperationCodeInfoDto>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (Exception parseEx)
            {
                return (false, null, $"HTTP 200 but failed to parse JSON: {parseEx.Message}. Body: {body}");
            }

            return data is null
                ? (false, null, "HTTP 200 but deserialized to null.")
                : (true, data, "");
        }
        catch (Exception ex)
        {
            return (false, null, ex.ToString());
        }
    }

    // ============================================================
    // PUT /api/v1/{sessionId}/tasks
    // Body: { "TaskId": 0, "CompletedActionsCount": 0 }
    // 200: Success
    // 400: "Failed to end task."
    // 500: "string"
    // return: (success, errorText)
    // ============================================================
    public static async Task<(bool success, string errorText)> EndTaskAsync(
        string sessionId,
        int taskId,
        int completedActionsCount,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, "sessionId is empty.");
            if (taskId <= 0)
                return (false, "taskId must be > 0.");
            if (completedActionsCount < 0)
                return (false, "completedActionsCount must be >= 0.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v1/{Uri.EscapeDataString(sessionId)}/tasks";

            var payload = new
            {
                TaskId = taskId,
                CompletedActionsCount = completedActionsCount
            };

            var json = JsonSerializer.Serialize(payload);

            using var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.StatusCode == HttpStatusCode.OK)
                return (true, "");

            // 400 / 500 обычно возвращают plain string
            if (resp.StatusCode == HttpStatusCode.BadRequest || resp.StatusCode == HttpStatusCode.InternalServerError)
            {
                var msg = (body ?? "").Trim();
                if (string.IsNullOrWhiteSpace(msg))
                    msg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}.";
                return (false, msg);
            }

            return (false, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }
        catch (Exception ex)
        {
            return (false, ex.ToString());
        }
    }

    // ============================================================
    // POST /api/v2/{sessionId}/products/Find
    // Body: ["95011846","95011847","95011848"]
    // 200: [ { ... }, { ... } ]  (может быть [] если ничего не найдено)
    // return: (success, items, errorText)
    // ============================================================
    public static async Task<(bool success, List<ProductFindDto>? items, string errorText)> FindProductsAsync(
        string sessionId,
        IEnumerable<string> operationalNumbers,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, null, "sessionId is empty.");
            if (operationalNumbers == null)
                return (false, null, "operationalNumbers is null.");

            var list = new List<string>();
            foreach (var x in operationalNumbers)
            {
                var v = x?.Trim();
                if (!string.IsNullOrWhiteSpace(v))
                    list.Add(v);
            }

            // можно разрешить пустой список и вернуть [] без запроса
            if (list.Count == 0)
                return (true, new List<ProductFindDto>(), "");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v2/{Uri.EscapeDataString(sessionId)}/products/Find";
            var json = JsonSerializer.Serialize(list);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.StatusCode != HttpStatusCode.OK)
                return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            List<ProductFindDto>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<ProductFindDto>>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (Exception parseEx)
            {
                return (false, null, $"HTTP 200 but failed to parse JSON: {parseEx.Message}. Body: {body}");
            }

            // если сервер вернул [] -> items будет пустым списком
            if (items == null)
                return (false, null, "HTTP 200 but deserialized to null (unexpected).");

            return (true, items, "");
        }
        catch (Exception ex)
        {
            return (false, null, ex.ToString());
        }
    }

    // ============================================================
    // POST /api/v1/{sessionId}/search/completedactions
    // Body:
    // {
    //   "DateFrom": "2026-01-30T00:00:00.000Z",
    //   "DateTo":   "2026-01-30T23:59:59.999Z",
    //   ... optional filters ...
    // }
    // return: (success, items, errorText)
    //
    // Вызов: передаём даты БЕЗ времени.
    // Метод сам округляет:
    //   DateFrom -> 00:00:00.000
    //   DateTo   -> 23:59:59.999
    // ============================================================
    public static async Task<(bool success, List<SearchCompletedActionDto>? items, string errorText)> SearchCompletedActionsAsync(
        string sessionId,
        DateTime dateFromDateOnly,
        DateTime dateToDateOnly,
        string? workerId = null,
        string? workerName = null,
        string? workerSurname = null,
        string? operationCode = null,
        string? operationalCode = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return (false, null, "sessionId is empty.");

            // Берём только Date часть и делаем границы дня
            var from = dateFromDateOnly.Date; // 00:00:00.000
            var to = dateToDateOnly.Date.AddDays(1).AddMilliseconds(-1); // 23:59:59.999

            if (to < from)
                return (false, null, "dateTo must be >= dateFrom.");

            var baseUrl = GetBaseUrlOrThrow();
            var url = $"{baseUrl}/api/v1/{Uri.EscapeDataString(sessionId)}/search/completedactions";

            var payload = new
            {
                DateFrom = from, // сериализация в ISO
                DateTo = to,
                WorkerId = string.IsNullOrWhiteSpace(workerId) ? null : workerId,
                WorkerName = string.IsNullOrWhiteSpace(workerName) ? null : workerName,
                WorkerSurname = string.IsNullOrWhiteSpace(workerSurname) ? null : workerSurname,
                OperationCode = string.IsNullOrWhiteSpace(operationCode) ? null : operationCode,
                OperationalCode = string.IsNullOrWhiteSpace(operationalCode) ? null : operationalCode
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.StatusCode != HttpStatusCode.OK)
                return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            SearchCompletedActionsResponseDto? data;
            try
            {
                data = JsonSerializer.Deserialize<SearchCompletedActionsResponseDto>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (Exception parseEx)
            {
                return (false, null, $"HTTP 200 but failed to parse JSON: {parseEx.Message}. Body: {body}");
            }

            if (data?.SearchCompletedActions == null)
                return (true, new List<SearchCompletedActionDto>(), ""); // считаем "ничего не найдено"

            return (true, data.SearchCompletedActions, "");
        }
        catch (Exception ex)
        {
            return (false, null, ex.ToString());
        }
    }
}


