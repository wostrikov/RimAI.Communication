using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Ustas.RimAI.Communication.Prompt;

/// <summary>
/// Data Transfer Object for preset JSON serialization.
/// Uses DataContract for compatibility with JsonUtil (DataContractJsonSerializer).
/// </summary>
[DataContract]
public class PresetDto
{
    [DataMember(Name = "version")]
    public int Version { get; set; } = 1;
    
    [DataMember(Name = "name")]
    public string Name { get; set; }
    
    [DataMember(Name = "description")]
    public string Description { get; set; }
    
    [DataMember(Name = "entries")]
    public List<EntryDto> Entries { get; set; } = new();
}

/// <summary>
/// Data Transfer Object for prompt entry JSON serialization.
/// </summary>
[DataContract]
public class EntryDto
{
    [DataMember(Name = "name")]
    public string Name { get; set; }
    
    [DataMember(Name = "content")]
    public string Content { get; set; }
    
    [DataMember(Name = "role")]
    public string Role { get; set; }

    [DataMember(Name = "customRole")]
    public string CustomRole { get; set; }
    
    [DataMember(Name = "position")]
    public string Position { get; set; }
    
    [DataMember(Name = "inChatDepth")]
    public int InChatDepth { get; set; }
    
    [DataMember(Name = "enabled")]
    public bool Enabled { get; set; } = true;

    [DataMember(Name = "isMainChatHistory")]
    public bool IsMainChatHistory { get; set; }
}
