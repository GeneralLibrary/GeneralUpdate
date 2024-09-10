using System;
using System.Collections.Concurrent;

namespace GeneralUpdate.Common.Internal.Pipeline;

public class PipelineContext
{
    private ConcurrentDictionary<string, object?> _context = new();
    
    public TValue? Get<TValue>(string key)
    {
        if (_context.TryGetValue(key, out var value))
        {
            return value is TValue typedValue ? typedValue : default;
        }
        return default;
    }   
    
    public void Add<TValue>(string key, TValue? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        _context[key] = value;
    }

    public bool Remove(string key) => _context.TryRemove(key, out _);

    public bool ContainsKey(string key) => _context.ContainsKey(key);
}