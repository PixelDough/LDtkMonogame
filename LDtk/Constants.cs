namespace LDtk;

using System.Text.Json;
using System.Text.Json.Serialization;

using Parsers;

/// <summary> General Constants used in LDtkMonogame. </summary>
public static class Constants
{
    /// <summary> The supported version of ldtk so you are in a newer version any new features may not be added yet please create an issue on the GitHub requesting them. </summary>
    public static readonly string SupportedLDtkVersion = "1.5.3";

    /// <summary> The converter used internally with JsonSerializer.Deserialize(..., Constants.SerializeOptions) not needed by the user just use .FromFileReflection instead. </summary>
    public static readonly JsonSerializerOptions SerializeOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new RectangleConverter(),
            new Vector2Converter(),
            new ColorConverter(),
            new PointConverter(),
            new GuidConverter(),
        },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary> The converter used internally with JsonSerializer.Deserialize(..., Constants.SerializeOptions) not needed by the user just use .FromFile instead. </summary>
    public static readonly LDtkJsonSourceGenerator JsonSourceGenerator = new(SerializeOptions);
}
