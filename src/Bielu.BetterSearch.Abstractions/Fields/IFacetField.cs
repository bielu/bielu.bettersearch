﻿namespace Bielu.BetterSearch.Abstractions.Fields;

public interface IFacetField
{
    public string FieldName { get; set; }
    public string FacetGroupName { get; set; }
    public int MaxSize { get; set; }
}
