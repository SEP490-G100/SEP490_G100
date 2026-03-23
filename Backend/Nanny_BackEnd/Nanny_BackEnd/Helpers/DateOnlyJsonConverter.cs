using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nanny_BackEnd.Helpers;

public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            throw new JsonException("DateOnly value cannot be null or empty");

        if (DateTime.TryParseExact(value, DateFormat, null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return DateOnly.FromDateTime(date);
        }

        throw new JsonException($"Unable to convert \"{value}\" to type DateOnly with format \"{DateFormat}\"");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat));
    }
}
