using System.Text.Json;
using System.Text.Json.Serialization;

namespace MultiAgentTaskSolver.Infrastructure.Serialization;

internal static class JsonDefaults
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };
}
