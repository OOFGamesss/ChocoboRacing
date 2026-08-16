using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Tracks a player's balance and whether they play from outside the host's party.
/// </summary>
namespace ChocoboRacing.Models;

[Serializable]
public class PlayerBank
{
    private const string LegacyArchivedField = "IsArchived";

    [JsonExtensionData]
    private IDictionary<string, JToken>? legacyFields;

    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public long Balance { get; set; }
    public bool IsExternal { get; set; }

    [OnDeserialized]
    private void MigrateLegacyArchivedFlag(StreamingContext context)
    {
        if (legacyFields == null) return;

        if (legacyFields.TryGetValue(LegacyArchivedField, out var archived)
            && archived.Type == JTokenType.Boolean
            && archived.Value<bool>())
            IsExternal = true;

        legacyFields = null;
    }
}
