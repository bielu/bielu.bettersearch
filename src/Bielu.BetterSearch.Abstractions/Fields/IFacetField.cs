namespace Bielu.BetterSearch.Abstractions.Fields;

/// <summary>A field to count facet values on.</summary>
public interface IFacetField
{
    /// <summary>The name of the field whose values are counted.</summary>
    public string FieldName { get; set; }
    /// <summary>The name the facet's results are grouped under.</summary>
    public string FacetGroupName { get; set; }
    /// <summary>The most facet values to return.</summary>
    public int MaxSize { get; set; }
}
