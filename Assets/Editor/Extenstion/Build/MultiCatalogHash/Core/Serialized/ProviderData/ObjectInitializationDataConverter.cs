using System;
using Editor.Extenstion.Build.MultiCatalogHash.Tools;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEngine.ResourceManagement.Util;

namespace Editor.Extenstion.Build.MultiCatalogHash.Core.Serialized.ProviderData
{
    public class ObjectInitializationDataConverter : JsonConverter<ObjectInitializationData>
    {
        public override void WriteJson(JsonWriter writer, ObjectInitializationData value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectTypeValue");
            writer.WriteValue(value.ObjectType.Value.AssemblyQualifiedName);
            writer.WritePropertyName("Data");
            writer.WriteValue(value.Data);
            writer.WriteEndObject();
        }

        public override ObjectInitializationData ReadJson(JsonReader reader, Type objectType, ObjectInitializationData existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var ret =  ObjectInitializationData.CreateSerializedInitializationData(
                Type.GetType((obj["ObjectTypeValue"] ?? throw new InvalidOperationException()).Value<string>()));
            ReflectionHelper.SetFieldValue(ret, "m_Data", (obj["Data"] ?? throw new InvalidOperationException()).Value<string>());
            return ret;
        }
    }
}