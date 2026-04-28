using System.Collections.Immutable;

namespace Zebl.Application.Services;

public static class CorrelationContext
{
    private static readonly AsyncLocal<ImmutableStack<string>> Stack = new();

    public static string CurrentId => Stack.Value is { IsEmpty: false } s ? s.Peek() : string.Empty;

    public static IDisposable Push(string correlationId)
    {
        var safe = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;
        var current = Stack.Value ?? ImmutableStack<string>.Empty;
        Stack.Value = current.Push(safe);
        return new PopWhenDisposed();
    }

    private sealed class PopWhenDisposed : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            var current = Stack.Value;
            if (current is { IsEmpty: false })
                Stack.Value = current.Pop();
        }
    }
}

