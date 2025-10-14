using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
namespace Shared
{
    public class ChatGPTCosmosSerializer : CosmosSerializer
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public override T FromStream<T>(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (stream.CanSeek && stream.Length == 0)
                return default!;

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                // 若呼叫者希望直接取得原始 Stream
                return (T)(object)new MemoryStream(Encoding.UTF8.GetBytes(json));
            }

            return JsonSerializer.Deserialize<T>(json, _options)!;
        }

        public override Stream ToStream<T>(T input)
        {
            var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                JsonSerializer.Serialize(writer, input, _options);
            }

            stream.Position = 0;
            return stream;
        }
    }
}