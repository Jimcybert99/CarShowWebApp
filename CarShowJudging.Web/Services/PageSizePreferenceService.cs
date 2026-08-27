using Microsoft.JSInterop;

namespace CarShowJudging.Web.Services;

// Remembers each paginated list's chosen page size in the browser's localStorage, keyed per list,
// so returning to a list (including via cancel/save from an edit sub-page) shows the same page size
// instead of resetting to the app default. Read/write can only happen after the Blazor Server
// circuit is connected (JS interop isn't available during prerender/first OnInitialized), so callers
// load the saved value from OnAfterRenderAsync(firstRender), not OnInitializedAsync.
public class PageSizePreferenceService(IJSRuntime js)
{
    private const string KeyPrefix = "pageSize:";

    public async Task<(bool Found, int? PageSize)> GetAsync(string listKey)
    {
        string? raw;
        try
        {
            raw = await js.InvokeAsync<string?>("localStorage.getItem", KeyPrefix + listKey);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            return (false, null);
        }

        if (string.IsNullOrEmpty(raw)) return (false, null);
        if (raw == "all") return (true, null);
        return int.TryParse(raw, out var n) ? (true, n) : (false, null);
    }

    public async Task SetAsync(string listKey, int? pageSize)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", KeyPrefix + listKey, pageSize?.ToString() ?? "all");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
        }
    }
}
