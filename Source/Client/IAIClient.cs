using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.AI;

namespace Ustas.RimAI.Communication.Client
{
    public interface IAIClient
    {
        /// <summary>
        /// Gets a chat completion from the AI model
        /// </summary>
        /// <param name="prefixMessages">Initial messages to prepend (can include system, user, assistant roles)</param>
        /// <param name="messages">List of conversation messages with roles</param>
        /// <param name="onRequestPrepared">Callback invoked as soon as the request JSON is built</param>
        /// <returns>AI response text and token usage</returns>
        Task<Payload> GetChatCompletionAsync(List<(Role role, string message)> prefixMessages, 
            List<(Role role, string message)> messages, 
            Action<Payload> onRequestPrepared = null);

        /// <summary>
        /// The same, at a chosen scheduling priority. An overload rather than an optional
        /// parameter on the method above: Art, Memory, Voices and Quests call that one from
        /// assemblies deployed on their own, and a changed signature is a
        /// MissingMethodException in every one of them until each is rebuilt.
        /// </summary>
        /// <param name="priority">Scheduling priority, or null for the module's own</param>
        Task<Payload> GetChatCompletionAsync(List<(Role role, string message)> prefixMessages,
            List<(Role role, string message)> messages,
            Action<Payload> onRequestPrepared,
            AiRequestPriority? priority);

        /// <summary>
        /// Streams chat completion and invokes a callback for each response chunk.
        /// </summary>
        /// <param name="prefixMessages">Initial messages</param>
        /// <param name="messages">Conversation messages</param>
        /// <param name="onResponseParsed">Callback for each parsed JSON object</param>
        /// <param name="onRequestPrepared">Callback invoked as soon as the request JSON is built</param>
        Task<Payload> GetStreamingChatCompletionAsync<T>(List<(Role role, string message)> prefixMessages, 
            List<(Role role, string message)> messages, 
            Action<T> onResponseParsed,
            Action<Payload> onRequestPrepared = null) where T : class;
    }
}
