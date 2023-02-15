using System;
using Newtonsoft.Json;

namespace SonarQube.Client.Helpers
{
    public class MillisecondUnixTimestampDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override void WriteJson(JsonWriter writer,
            DateTimeOffset value,
            JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override DateTimeOffset ReadJson(JsonReader reader,
            Type objectType,
            DateTimeOffset existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var timestamp = (long?)reader?.Value;

            return timestamp.HasValue 
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value) 
                : default(DateTimeOffset);
        }
    }
}
