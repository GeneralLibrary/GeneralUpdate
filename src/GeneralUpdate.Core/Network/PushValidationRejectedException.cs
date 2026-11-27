using System;

namespace GeneralUpdate.Core.Network;

internal sealed class PushValidationRejectedException : Exception
{
    public PushValidationRejectedException() : base("Server did not approve the push record before file application.") { }
}
