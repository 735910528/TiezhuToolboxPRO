using System.Text.Json.Serialization;

namespace TiezhuToolbox.Modules.GearExport;

public sealed class GearTxtDocument
{
    [JsonPropertyName("items")]
    public List<GearTxtItem> Items { get; set; } = new();

    [JsonPropertyName("heroes")]
    public List<object> Heroes { get; set; } = new();
}

public sealed class GearTxtItem
{
    [JsonPropertyName("gear")]
    public string? Gear { get; set; }

    [JsonPropertyName("rank")]
    public string? Rank { get; set; }

    [JsonPropertyName("set")]
    public string? Set { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("enhance")]
    public int Enhance { get; set; }

    [JsonPropertyName("main")]
    public GearTxtStat? Main { get; set; }

    [JsonPropertyName("substats")]
    public List<GearTxtStat> Substats { get; set; } = new();

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ingameId")]
    public string? IngameId { get; set; }

    [JsonPropertyName("ingameEquippedId")]
    public string? IngameEquippedId { get; set; }
}

public sealed class GearTxtStat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("rolls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Rolls { get; set; }

    [JsonPropertyName("modified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Modified { get; set; }
}

public sealed class GearUnpackResult
{
    public required GearTxtDocument Document { get; init; }
    public int RawItemCount { get; init; }
    public int ExportedItemCount { get; init; }
    public int LevelZeroCount { get; init; }
}
