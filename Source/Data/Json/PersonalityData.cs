using System.Runtime.Serialization;

namespace Ustas.RimAI.Communication.Data;

[DataContract]
public class PersonalityData(string persona, float chattiness = 1.0f) : IJsonData
{
    [DataMember(Name = "persona")] public string Persona { get; set; } = persona;

    [DataMember(Name = "chattiness")] public float Chattiness { get; set; } = chattiness;

    public string GetText()
    {
        return Persona;
    }
}