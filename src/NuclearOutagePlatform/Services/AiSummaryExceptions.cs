using System;

namespace MVC_EF_Start_8.Services
{
    /// <summary>
    /// Thrown when the Groq API key isn't configured. Distinct from
    /// AiProviderUnavailableException so the controller can tell "this
    /// feature was never turned on" (503, config problem) apart from
    /// "the feature is on but Groq is down/rate-limiting/erroring right
    /// now" (502, transient problem) -- same distinction the plan asks for
    /// when it says to "handle rate limits gracefully."
    /// </summary>
    public class AiSummaryNotConfiguredException : Exception
    {
        public AiSummaryNotConfiguredException(string message) : base(message) { }
    }

    public class AiProviderUnavailableException : Exception
    {
        public AiProviderUnavailableException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
