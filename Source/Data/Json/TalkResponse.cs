#nullable enable
using System;
using System.Runtime.Serialization;
using Ustas.RimAI.Communication.Data;
using Verse;

namespace Ustas.RimAI.Communication.Data;

[DataContract]
public class TalkResponse(TalkType talkType, string name, string text) : IJsonData
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public TalkType TalkType { get; set; } = talkType;
    
    [DataMember(Name = "name")] 
    public string Name { get; set; } = name;

    [DataMember(Name = "text")] 
    public string Text { get; set; } = text;

    [DataMember(Name = "act", EmitDefaultValue = false)]
    public string? InteractionRaw { get; set; }

    [DataMember(Name = "target", EmitDefaultValue = false)]
    public string? TargetName { get; set; }
    
    public Guid ParentTalkId { get; set; }
    
    public bool IsReply()
    {
        return ParentTalkId != Guid.Empty;
    }
    
    public string GetText()
    {
        return Text;
    }
    
    public InteractionType GetInteractionType()
    {
        if (string.IsNullOrWhiteSpace(InteractionRaw)) 
            return InteractionType.None;

        return Enum.TryParse(InteractionRaw, true, out InteractionType result) ? result : InteractionType.None;
    }
    /// <summary>The speaker, resolved through the request's names while streaming. Not serialized.</summary>
    public Pawn? SpeakerPawn { get; set; }

    /// <summary>The target, resolved the same way. Not serialized.</summary>
    public Pawn? TargetPawn { get; set; }

    public Pawn? GetTarget()
    {
        return TargetPawn ?? (TargetName != null ? Cache.GetByName(TargetName)?.Pawn : null);
    }

    public override string ToString()
    {
        return $"Type: {TalkType} | Name: {Name} | Text: \"{Text}\" | " +
               $"Int: {InteractionRaw} | Target: {TargetName}";
    }
}