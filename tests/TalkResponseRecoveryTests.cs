using System;
using Ustas.RimAI.Communication.Client.ProviderPolicy;

internal static class TalkResponseRecoveryTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        T(TalkResponseRecovery.Evaluate(null) == TalkResponseEvaluation.Empty, "empty-null");
        T(TalkResponseRecovery.Evaluate("   ") == TalkResponseEvaluation.Empty, "empty-ws");
        T(TalkResponseRecovery.Evaluate("sorry I cannot") == TalkResponseEvaluation.Malformed, "prose-malformed");
        T(TalkResponseRecovery.Evaluate("{\"foo\":1}") == TalkResponseEvaluation.Malformed, "object-without-talk");
        T(TalkResponseRecovery.Evaluate("{\"name\":\"A\"}") == TalkResponseEvaluation.Malformed, "name-only");

        const string valid = "{\"name\":\"Aman\",\"text\":\"Привіт\"}";
        T(TalkResponseRecovery.Evaluate(valid) == TalkResponseEvaluation.Recovered, "valid");
        var one = TalkResponseRecovery.Extract(valid);
        T(one.Count == 1 && one[0].Name == "Aman" && one[0].Text == "Привіт", "valid-fields");

        const string mixed = "garbage {not json} \n{\"name\":\"Aman\",\"text\":\"Hi\"}\n trailing";
        var recovered = TalkResponseRecovery.Extract(mixed);
        T(TalkResponseRecovery.Evaluate(mixed) == TalkResponseEvaluation.Recovered, "mixed-recovered");
        T(recovered.Count == 1 && recovered[0].Text == "Hi", "mixed-skips-garbage");

        const string two = "{\"name\":\"A\",\"text\":\"one\"} oops {\"name\":\"B\",\"text\":\"two\"}";
        T(TalkResponseRecovery.Extract(two).Count == 2, "two-objects");

        T(CommunicationFailureClassifier.FromPayload(null, mixed, 0) == CommunicationFailureClass.None, "payload-recovered");
        T(CommunicationFailureClassifier.FromPayload(null, "", 0) == CommunicationFailureClass.EmptyResponse, "payload-empty");
        T(CommunicationFailureClassifier.FromPayload(null, "not-json", 0) == CommunicationFailureClass.MalformedResponse, "payload-malformed");
        T(CommunicationFailureClassifier.FromPayload("boom", "not-json", 1) == CommunicationFailureClass.None, "delivered-wins");
        return n;
    }
}
