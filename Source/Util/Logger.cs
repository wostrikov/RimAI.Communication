using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication.Util;

public static class Logger
{
    public static void Message(object message) =>
        RimAiLog.Info(RimAiLogCategory.Communication, message?.ToString() ?? string.Empty);

    public static void Debug(object message) =>
        RimAiLog.Debug(RimAiLogCategory.Communication, message?.ToString() ?? string.Empty);

    public static void Warning(object message) =>
        RimAiLog.Warning(RimAiLogCategory.Communication, message?.ToString() ?? string.Empty);

    public static void Error(object message) =>
        RimAiLog.Error(RimAiLogCategory.Communication, message?.ToString() ?? string.Empty);

    public static void ErrorOnce(object text, int key) =>
        RimAiLog.ErrorOnce(RimAiLogCategory.Communication, text?.ToString() ?? string.Empty, key);
}
