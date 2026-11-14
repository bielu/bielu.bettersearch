using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.Services;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Turns the configured field mappings into OpenSearch properties. Document values are stored under
/// <c>fields</c>, so the mapped fields are properties of that object.
/// </summary>
internal static class OpenSearchMappings
{
    private const string FieldsProperty = "fields";

    /// <summary>The <c>properties</c> for the mappings, or <c>null</c> when there are none.</summary>
    public static Properties? Create(IReadOnlyCollection<OpenSearchFieldMapping>? mappings)
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

    private static IProperty Property(OpenSearchFieldMapping mapping) => mapping.Type switch
    {
        OpenSearchFieldType.Text => new TextProperty
        {
            Analyzer = mapping.Analyzer,
            Fields = new Properties
            {
                { FieldMappings.KeywordSubField, new KeywordProperty { IgnoreAbove = FieldMappings.KeywordIgnoreAbove } }
            }
        },
        OpenSearchFieldType.Keyword => new KeywordProperty(),
        OpenSearchFieldType.Date => new DateProperty { Format = mapping.Format },
        OpenSearchFieldType.Long => new NumberProperty(NumberType.Long),
        OpenSearchFieldType.Integer => new NumberProperty(NumberType.Integer),
        OpenSearchFieldType.Short => new NumberProperty(NumberType.Short),
        OpenSearchFieldType.Byte => new NumberProperty(NumberType.Byte),
        OpenSearchFieldType.Double => new NumberProperty(NumberType.Double),
        OpenSearchFieldType.Float => new NumberProperty(NumberType.Float),
        OpenSearchFieldType.Boolean => new BooleanProperty(),
        OpenSearchFieldType.GeoPoint => new GeoPointProperty(),
        _ => throw new ArgumentException($"Field {mapping.Field} has no mapping type.")
    };
}
