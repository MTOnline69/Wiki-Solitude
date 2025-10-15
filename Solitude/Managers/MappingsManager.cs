using System.Text.Json;
using RestSharp;

namespace Solitude.Managers;

public static class MappingsManager
{
    private static bool TryFindSavedMappings(out string mappingsPath)
    {
        DirectoryInfo mappingsDir = new(DirectoryManager.MappingsDir);

        var mostRecentMappings =
            (from usmap in mappingsDir.GetFiles("*.usmap")
             orderby usmap.LastWriteTime descending
             select usmap).FirstOrDefault();

        if (mostRecentMappings is not null)
        {
            mappingsPath = mostRecentMappings.FullName;
            return true;
        }

        mappingsPath = string.Empty;

        return false;
    }

    public static bool TryGetMappings(out string mappingsPath)
    {
        Log.Information("Attempting to retrieve mappings");

        mappingsPath = string.Empty;

        using var client = new RestClient();

        var request = new RestRequest("https://uedb.dev/svc/api/v1/fortnite/mappings", Method.Get)
        {
            Timeout = TimeSpan.FromMilliseconds(3 * 1000)
        };

        var response = client.Execute(request);

        if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
        {
            Log.Error("Request to UEDB for mappings failed.");

            return TryFindSavedMappings(out mappingsPath);
        }

        using var doc = JsonDocument.Parse(response.Content);
        var root = doc.RootElement;

        if (!root.TryGetProperty("version", out var versionProp) ||
			!root.TryGetProperty("mappings", out var mappingsProp) ||
			!mappingsProp.TryGetProperty("ZStandard", out var zstdUrlProp))
		{
			Log.Error("Invalid mapping format or missing ZStandard URL.");
			return TryFindSavedMappings(out mappingsPath);
		}
		
		var version = versionProp.GetString();
		var zstdUrl = zstdUrlProp.GetString();
		var fileName = Path.GetFileName(new Uri(zstdUrl).LocalPath);
		
		mappingsPath = Path.Join(DirectoryManager.MappingsDir, fileName);
		
		if (File.Exists(mappingsPath))
		{
			return true;
		}
		
		var mappingsData = client.DownloadData(new(zstdUrl));
		
		if (mappingsData is null || mappingsData.Length <= 0)
		{
			Log.Error("Mappings data downloaded from UEDB is null.");
			return TryFindSavedMappings(out mappingsPath);
		}
		
		File.WriteAllBytes(mappingsPath, mappingsData);
		return true;
    }
}
