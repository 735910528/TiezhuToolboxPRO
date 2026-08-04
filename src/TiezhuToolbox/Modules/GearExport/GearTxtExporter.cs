using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiezhuToolbox.Modules.GearExport;

public static class GearTxtExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(GearTxtDocument document)
        => JsonSerializer.Serialize(document, Options);

    public static void WriteToFile(string path, GearTxtDocument document)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, Serialize(document), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
