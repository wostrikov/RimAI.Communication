using System.Threading.Tasks;
using Verse;

namespace Ustas.RimAI.Communication.Data;

public interface IPersonaGenerator
{
    bool TryGenerate(Pawn pawn, out Task<PersonalityData> result);
}
