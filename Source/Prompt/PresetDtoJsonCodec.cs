using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Ustas.RimAI.Communication.Prompt;

/// <summary>
/// Verse-free preset DTO codec. PresetSerializer remains the file/UI
/// owner; this is the JSON contract used by import/export.
/// </summary>
public static class PresetDtoJsonCodec
{
    public static string Export(PresetDto dto)
    {
        if (dto == null)
            return null;
        using (var stream = new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(PresetDto)).WriteObject(stream, dto);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public static PresetDto Import(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            return new DataContractJsonSerializer(typeof(PresetDto)).ReadObject(stream) as PresetDto;
        }
    }
}
