using System;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     Update parameter configuration class for external API callers.
    ///     Designed specifically for external consumers to configure the core parameters required for update behavior.
    ///     Inherits from <see cref="UpdateConfiguration" /> to reuse common fields, reducing duplication and improving maintainability.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>UpdateRequest</c> is the entry-point configuration object for the update workflow, constructed by
    ///         <see cref="UpdateRequestBuilder" /> using the builder pattern. Once built, it is mapped to the internal
    ///         runtime configuration <see cref="UpdateContext" /> via <see cref="ConfigurationMapper.MapToUpdateContext" />
    ///         for use by the update pipeline.
    ///     </para>
    ///     <para>
    ///         Calling the <see cref="Validate" /> method performs completeness validation on all required fields,
    ///         ensuring that key parameters such as <c>UpdateUrl</c>, <c>MainAppName</c>, and <c>ClientVersion</c>
    ///         are not empty or incorrectly formatted.
    ///     </para>
    /// </remarks>
    /// <seealso cref="UpdateConfiguration" />
    /// <seealso cref="UpdateContext" />
    /// <seealso cref="UpdateRequestBuilder" />
    /// <seealso cref="ConfigurationMapper" />
    public class UpdateRequest : UpdateConfiguration
    {
        /// <summary>
        ///     Validates that all required fields of the configuration object are present and correctly formatted.
        /// </summary>
        /// <remarks>
        ///     <para>The method performs validation on the following fields:</para>
        ///     <list type="bullet">
        ///         <item>
        ///             <c>UpdateUrl</c>: Must not be empty and must be a valid absolute URI.</item>
        ///         <item>
        ///             <c>UpdateLogUrl</c>: If set, must be a valid absolute URI.</item>
        ///         <item>
        ///             <c>UpdateAppName</c>: Must not be empty.</item>
        ///         <item>
        ///             <c>MainAppName</c>: Must not be empty.</item>
        ///         <item>
        ///             <c>AppSecretKey</c>: Must not be empty.</item>
        ///         <item>
        ///             <c>ClientVersion</c>: Must not be empty.</item>
        ///     </list>
        ///     <para>
        ///         This method is typically called at the end of the <see cref="UpdateRequestBuilder.Build" /> method
        ///         to ensure the constructed configuration object is complete and valid.
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     Thrown when any required field is null, empty, consists only of whitespace, or is malformed.
        ///     The exception message indicates which specific field failed validation.
        /// </exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(UpdateUrl) || !Uri.IsWellFormedUriString(UpdateUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateUrl");

            if (!string.IsNullOrWhiteSpace(UpdateLogUrl) && !Uri.IsWellFormedUriString(UpdateLogUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateLogUrl");

            if (string.IsNullOrWhiteSpace(UpdateAppName))
                throw new ArgumentException("UpdateAppName cannot be empty");

            if (string.IsNullOrWhiteSpace(MainAppName))
                throw new ArgumentException("MainAppName cannot be empty");

            if (string.IsNullOrWhiteSpace(AppSecretKey))
                throw new ArgumentException("AppSecretKey cannot be empty");

            if (string.IsNullOrWhiteSpace(ClientVersion))
                throw new ArgumentException("ClientVersion cannot be empty");
        }
    }
}
