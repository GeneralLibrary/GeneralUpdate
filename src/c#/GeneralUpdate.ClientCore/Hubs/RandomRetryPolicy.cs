using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace GeneralUpdate.ClientCore.Hubs;

public class RandomRetryPolicy : IRetryPolicy
{
    private readonly Random _random = new();

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // If we've been reconnecting for less than 60 seconds so far,
        // wait between 0 and 10 seconds before the next reconnect attempt.
        if (retryContext.ElapsedTime < TimeSpan.FromSeconds(60))
            return TimeSpan.FromSeconds(_random.NextDouble() * 10);
        // If we've been reconnecting for more than 60 seconds so far, stop reconnecting.
        return null;
    }
}