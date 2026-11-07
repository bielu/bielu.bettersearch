namespace Bielu.BetterSearch.Abstractions.Models;

public interface IEntity<T>
{
    T Id { get; set; }
}
