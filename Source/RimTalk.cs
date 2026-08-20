using System.Linq;
using Ustas.RimAI.Communication.Client;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Error;
using Ustas.RimAI.Communication.Patches;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Core.Communication;
using Verse;

namespace Ustas.RimAI.Communication;

public class RimTalk : GameComponent
{
    public RimTalk(Game game)
    {
    }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        Reset();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        Reset();
    }

    public static void Reset(bool soft = false)
    {
        var settings = Settings.Get();
        if (settings != null)
        {
            settings.CurrentCloudConfigIndex = 0;
        }

        AIErrorHandler.ResetQuotaWarning();
        TickManagerPatch.Reset();
        AIClientFactory.Clear();
        AIService.Clear();
        TalkHistory.Clear();
        PatchThoughtHandlerGetDistinctMoodThoughtGroups.Clear();
        Cache.GetAll().ToList().ForEach(pawnState => pawnState.IgnoreAllTalkResponses());
        Cache.InitializePlayerPawn();
        UserRequestPool.Clear();

        if (soft)
        {
            TalkLifecycle.PublishGameSessionReset("soft");
            return;
        }

        Counter.Tick = 0;
        Cache.Clear();
        Stats.Reset();
        TalkRequestPool.Clear();
        ApiHistory.Clear();
        TalkLifecycle.PublishGameSessionReset("full");
    }
}