namespace Bielu.BetterSearch.Services;

[Obsolete("No longer used by BetterSearch; dispose the handle Subscribe returns.")]
public class Unsubscriber<T>(List<IObserver<T>>? observers, IObserver<T>? observer) : IDisposable
{
#pragma warning disable CA1816
    public void Dispose()
#pragma warning restore CA1816
    {
        if (observers != null && observer != null)
        {
            observers.Remove(observer);
        }
    }
}
