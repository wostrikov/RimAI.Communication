using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// Pairs an approach with a subject from <see cref="TopicKeywordPool"/>, drawing each from its
/// own shuffled deck so no pairing repeats until a deck runs out, whatever the pawn's persona.
/// </summary>
public static class TopicService
{
    private static readonly object Lock = new();
    private static readonly Random Rng = new();

    private static Queue<string> _approachDeck = new();
    private static Queue<string> _subjectDeck = new();

    /// <summary>
    /// A topic half the time, and always for a pawn's first talk. Animals, mechanoids, entities
    /// and mutants never get a human narrative topic.
    /// </summary>
    public static string TryGetTopic(Pawn pawn = null)
    {
        if (pawn != null && (!pawn.RaceProps.Humanlike || pawn.IsMutant))
            return null;

        lock (Lock)
        {
            bool isFirstTalk = pawn != null && Cache.Get(pawn)?.LastTalkTick == 0;
            if (!isFirstTalk && Rng.NextDouble() >= 0.50) return null;
            EnsureDecks();
            string approach = Draw(_approachDeck) ?? "casual remark";
            string subject = Draw(_subjectDeck) ?? "daily life";
            return $"[{approach}, {subject}]";
        }
    }

    /// <summary>Empties both decks; the next topic reshuffles them.</summary>
    public static void Reset()
    {
        lock (Lock)
        {
            _approachDeck.Clear();
            _subjectDeck.Clear();
        }
    }

    private static string Draw(Queue<string> deck) => deck.Count == 0 ? null : deck.Dequeue();

    private static void EnsureDecks()
    {
        if (_approachDeck.Count == 0) _approachDeck = Shuffled(TopicKeywordPool.ApproachKeywords);
        if (_subjectDeck.Count == 0) _subjectDeck = Shuffled(TopicKeywordPool.SubjectKeywords);
    }

    private static Queue<string> Shuffled(string[] source)
    {
        var list = new List<string>(source ?? []);
        for (int n = list.Count - 1; n > 0; n--)
        {
            int k = Rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
        return new Queue<string>(list);
    }
}
