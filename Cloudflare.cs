public class AcmeDnsProvider : IAcmeDnsProvider {
    private readonly HttpClient _http;
    private readonly string _apiToken;

    public AcmeDnsProvider() {
        _http = new HttpClient();
        _apiToken = /*"API_TOKEN_HERE"*/;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
    }

    public async Task<bool> CreateDnsRecord(string domain, string txtValue) {
        var zoneName = GetZoneName(domain);
        var zoneId = await GetZoneId(zoneName);
        if (zoneId == null) return false;

        var name = "_acme-challenge." + domain.TrimEnd('.');
        var payload = new {
            type = "TXT",
            name,
            content = txtValue,
            ttl = 120
        };

        var resp = await _http.PostAsync(
            $"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );

        if (!resp.IsSuccessStatusCode) return false;

        await Task.Delay(TimeSpan.FromSeconds(15));
        return true;
    }

    public async Task<bool> DeleteDnsRecord(string domain, string txtValue) {
        var zoneName = GetZoneName(domain);
        var zoneId = await GetZoneId(zoneName);
        if (zoneId == null) return false;

        var recordsResp = await _http.GetAsync(
            $"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records?type=TXT&name=_acme-challenge.{domain}"
        );
        if (!recordsResp.IsSuccessStatusCode) return false;

        using var doc = JsonDocument.Parse(await recordsResp.Content.ReadAsStringAsync());
        var rec = doc.RootElement.GetProperty("result")
            .EnumerateArray()
            .FirstOrDefault(x => x.GetProperty("content").GetString() == txtValue);

        if (rec.ValueKind == JsonValueKind.Undefined) return false;
        var id = rec.GetProperty("id").GetString();
        if (id == null) return false;

        var delResp = await _http.DeleteAsync($"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records/{id}");
        return delResp.IsSuccessStatusCode;
    }

    private async Task<string?> GetZoneId(string zoneName) {
        var resp = await _http.GetAsync($"https://api.cloudflare.com/client/v4/zones?name={zoneName}");
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("result")
            .EnumerateArray()
            .FirstOrDefault()
            .GetProperty("id").GetString();
    }

    private static string GetZoneName(string domain) {
        var parts = domain.Split('.');
        return parts.Length >= 2 ? string.Join('.', parts[^2], parts[^1]) : domain;
    }
}

return new AcmeDnsProvider();