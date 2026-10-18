using System;
using Ustas.RimAI.Communication.Client.OpenAI;

/// <summary>
/// The base URL a player types for an OpenAI-compatible provider becomes one chat completions
/// endpoint, whether it is a bare host, stops at /v1, or is already the full endpoint.
/// </summary>
internal static class ChatCompletionsEndpointTests
{
    public static int Run()
    {
        int n = 0;
        void Eq(string input, string expected, string name)
        {
            string actual = ChatCompletionsEndpoint.Format(input);
            if (actual != expected)
                throw new Exception("FAILED " + name + ": '" + input + "' gave '" + actual + "', expected '" + expected + "'");
            n++;
        }

        Eq(null, string.Empty, "null-is-empty");
        Eq("   ", string.Empty, "blank-is-empty");

        Eq("http://localhost:1234", "http://localhost:1234/v1/chat/completions", "bare-host");
        Eq("http://localhost:1234/", "http://localhost:1234/v1/chat/completions", "bare-host-slash");
        Eq("  http://localhost:1234//  ", "http://localhost:1234/v1/chat/completions", "bare-host-padded");

        Eq("http://localhost:1234/v1", "http://localhost:1234/v1/chat/completions", "v1");
        Eq("http://localhost:1234/v1/", "http://localhost:1234/v1/chat/completions", "v1-slash");
        Eq("http://localhost:1234/V1//", "http://localhost:1234/V1/chat/completions", "v1-upper-slashes");
        Eq("https://openrouter.ai/api/v1", "https://openrouter.ai/api/v1/chat/completions", "nested-v1");
        Eq("https://openrouter.ai/api/v1/", "https://openrouter.ai/api/v1/chat/completions", "nested-v1-slash");

        Eq("https://api.example.com/v1/chat/completions", "https://api.example.com/v1/chat/completions", "full-endpoint");
        Eq("https://api.example.com/v1/chat/completions/", "https://api.example.com/v1/chat/completions", "full-endpoint-slash");
        Eq("https://api.example.com/openai/chat/completions", "https://api.example.com/openai/chat/completions", "full-endpoint-other-prefix");
        Eq("https://api.openai.com/v1/responses", "https://api.openai.com/v1/responses", "responses-endpoint-kept");

        Eq("https://api.example.com/v10", "https://api.example.com/v10", "v10-is-not-v1");
        Eq("https://api.example.com/custom", "https://api.example.com/custom", "other-path-kept");

        Eq("https://api.example.com/v1?key=a", "https://api.example.com/v1/chat/completions?key=a", "v1-query-kept-last");
        Eq("https://api.example.com/?key=a", "https://api.example.com/v1/chat/completions?key=a", "bare-host-query-kept-last");

        Eq("not a url/", "not a url", "unparsable-trimmed");

        return n;
    }
}
