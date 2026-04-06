using System;
using System.Collections.Generic;

public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> _handlers = new();

    public static void Subscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var existing))
            _handlers[type] = Delegate.Combine(existing, handler);
        else
            _handlers[type] = handler;
    }

    public static void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var existing))
        {
            var result = Delegate.Remove(existing, handler);
            if (result == null) _handlers.Remove(type);
            else _handlers[type] = result;
        }
    }

    public static void Publish<T>(T evt) where T : struct
    {
        if (_handlers.TryGetValue(typeof(T), out var handler))
            ((Action<T>)handler)?.Invoke(evt);
    }

    public static void Clear() => _handlers.Clear();
}
