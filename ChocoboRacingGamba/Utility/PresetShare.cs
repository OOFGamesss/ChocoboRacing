using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using ChocoboRacing.Models;
using Newtonsoft.Json;

/// <summary>
/// Encodes and decodes a settings preset as a portable share code.
/// </summary>
namespace ChocoboRacing.Utility;

public static class PresetShare
{
    private const string Prefix = "CRG1:";

    public static string Export(SettingsPreset preset)
    {
        var raw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(preset));
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            gzip.Write(raw, 0, raw.Length);
        return Prefix + Convert.ToBase64String(output.ToArray());
    }

    public static bool TryImport(string code, out SettingsPreset preset)
    {
        preset = null!;
        if (string.IsNullOrWhiteSpace(code)) return false;

        var trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        try
        {
            var decoded = Decode(trimmed[Prefix.Length..]);
            if (decoded == null || string.IsNullOrWhiteSpace(decoded.Name)) return false;
            preset = decoded;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static SettingsPreset? Decode(string payload)
    {
        using var input = new MemoryStream(Convert.FromBase64String(payload));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return JsonConvert.DeserializeObject<SettingsPreset>(reader.ReadToEnd());
    }
}
