using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Data;

/// <summary>A story told in the colony that is still being carried on. Saved with the world.</summary>
public class StoryThread : IExposable
{
    public string Id;
    public string Teller;
    public string Subject;
    public string Summary;
    public List<string> HeardBy = [];   // ThingIDs of everyone who spoke or listened
    public string LastTellerId;
    public int Steps;
    public int CreatedTick;
    public int LastTick;
    public bool IsClosed;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Teller, "teller");
        Scribe_Values.Look(ref Subject, "subject");
        Scribe_Values.Look(ref Summary, "summary");
        Scribe_Collections.Look(ref HeardBy, "heardBy", LookMode.Value);
        Scribe_Values.Look(ref LastTellerId, "lastTellerId");
        Scribe_Values.Look(ref Steps, "steps");
        Scribe_Values.Look(ref CreatedTick, "createdTick");
        Scribe_Values.Look(ref LastTick, "lastTick");
        Scribe_Values.Look(ref IsClosed, "isClosed");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            HeardBy ??= [];
    }
}
