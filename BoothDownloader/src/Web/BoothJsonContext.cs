using System.Text.Json.Serialization;
using BoothDownloader.Configuration;

namespace BoothDownloader.Web;

[JsonSerializable(typeof(BoothJsonItem))]
[JsonSerializable(typeof(BoothDownloadCache))]
[JsonSerializable(typeof(Dictionary<string, BoothItemAssets>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(BoothConfig))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = true,
    AllowTrailingCommas = true,
    IncludeFields = true)]
internal partial class BoothJsonContext : JsonSerializerContext;