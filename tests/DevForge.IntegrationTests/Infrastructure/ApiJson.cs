using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevForge.IntegrationTests.Infrastructure;

internal static class ApiJson
{
    /// <summary>The same conventions the API uses: camelCase properties, enums as strings.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Options);
        return value ?? throw new InvalidOperationException("The response body was empty.");
    }
}
