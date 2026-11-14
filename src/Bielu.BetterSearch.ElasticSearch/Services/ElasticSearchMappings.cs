using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch.Mapping;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Turns the configured field mappings into Elasticsearch properties. Document values are stored under
/// <c>fields</c>, so the mapped fields are properties of that object.
/// </summary>
internal static class ElasticSearchMappings
{
    private const string FieldsProperty = "fields";

    /// <summary>The <c>properties</c> for the mappings, or <c>null</c> when there are none.</summary>
    public static Properties? Create(IReadOnlyCollection<ElasticSearchFieldMapping>? mappings)
    {
        if (mappings is not { Count: > 0 })
        {
            return null;
        }

        var fields = new Properties();
        foreach (var mapping in mappings)
        {
            fields.Add(mapping.Field!, Property(mapping));
        }

        return new Properties { { FieldsProperty, new ObjectProperty { Properties = fields } } };
    }

    private static IProperty Property(ElasticSearchFieldMapping mapping) => mapping.Type switch
    {
        ElasticSearchFieldType.Text => new TextProperty
        {
            Analyzer = mapping.Analyzer,
            Fields = new Properties
            {
                { FieldMappings.KeywordSubField, new KeywordProperty { IgnoreAbove = FieldMappings.KeywordIgnoreAbove } }
            }
        },
        ElasticSearchFieldType.Keyword => new KeywordProperty(),
        ElasticSearchFieldType.Date => new DateProperty { Format = mapping.Format },
        ElasticSearchFieldType.Long => new LongNumberProperty(),
        ElasticSearchFieldType.Integer => new IntegerNumberProperty(),
        ElasticSearchFieldType.Short => new ShortNumberProperty(),
        ElasticSearchFieldType.Byte => new ByteNumberProperty(),
        ElasticSearchFieldType.Double => new DoubleNumberProperty(),
        ElasticSearchFieldType.Float => new FloatNumberProperty(),
        ElasticSearchFieldType.Boolean => new BooleanProperty(),
        ElasticSearchFieldType.GeoPoint => new GeoPointProperty(),
        _ => throw new ArgumentException($"Field {mapping.Field} has no mapping type.")
    };
}
