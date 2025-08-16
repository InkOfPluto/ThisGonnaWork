using System;
using Newtonsoft.Json;
using UnityEngine;

public class Vector3Converter : JsonConverter
{
    public override bool CanConvert(Type objectType)
        => objectType == typeof(Vector3);

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var v = (Vector3)value;
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(v.x);
        writer.WritePropertyName("y"); writer.WriteValue(v.y);
        writer.WritePropertyName("z"); writer.WriteValue(v.z);
        writer.WriteEndObject();
    }

    // ���ֻ����д�ļ����������л����Ǳ��裻������ʵ���Է���δ��Ҫ����
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        float x = 0f, y = 0f, z = 0f;
        if (reader.TokenType == JsonToken.Null) return Vector3.zero;

        // ���� { x:..., y:..., z:... }
        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName)
            {
                var name = (string)reader.Value;
                if (!reader.Read()) break;
                switch (name)
                {
                    case "x": x = Convert.ToSingle(reader.Value); break;
                    case "y": y = Convert.ToSingle(reader.Value); break;
                    case "z": z = Convert.ToSingle(reader.Value); break;
                }
            }
            else if (reader.TokenType == JsonToken.EndObject)
            {
                break;
            }
        }
        return new Vector3(x, y, z);
    }
}
