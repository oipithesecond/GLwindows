using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace GLWPF.Logic;

public static class HttpClientUploader
{
    private static readonly HttpClient client = new();

    public static async Task UploadStatsAsync(string filePath, string url)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            var json = await File.ReadAllTextAsync(filePath);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync(url, content);
            response.EnsureSuccessStatusCode(); // Throws if not 200–299

            Console.WriteLine($"Uploaded stats.json to {url} — Status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to upload stats.json: {ex.Message}");
        }
    }
}
