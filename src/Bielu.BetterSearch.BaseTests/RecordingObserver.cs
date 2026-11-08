namespace Bielu.BetterSearch.Tests;

/// <summary>Records every notification an observable sends, in order.</summary>
public sealed class RecordingObserver<T> : IObserver<T>
{
    public List<T> Values { get; } = new();
    public int CompletedCount { get; private set; }
    public List<Exception> Errors { get; } = new();

    public void OnNext(T value) => Values.Add(value);
    public void OnCompleted() => CompletedCount++;
    public void OnError(Exception error) => Errors.Add(error);
}
