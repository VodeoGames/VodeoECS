using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// JsonConverter for serializing and deserializing NamedRegistry indices by name.
    /// </summary>
    /// <typeparam name="T">The type stored in the NamedRegistry to support serializing and deserializing by name.</typeparam>
    public class NamedRegistryConverter<T> : JsonConverter<RegistryIndex<T>>
    {
        protected NamedRegistry<T> registry;
        public NamedRegistryConverter ( NamedRegistry<T> registry )
        {
            this.registry = registry;
        }

        public override void WriteJson ( JsonWriter writer, RegistryIndex<T> value, JsonSerializer serializer )
        {
            if ( registry.Count > 0 )
                writer.WriteValue( registry.GetName( value ) );
            else
                writer.WriteValue( "Registry of type " + registry.GetType() + " is empty" );
        }

        public override RegistryIndex<T> ReadJson ( JsonReader reader, Type objectType, RegistryIndex<T> existingValue, bool hasExistingValue, JsonSerializer serializer )
        {
            return registry.GetIndexByName( ( string )reader.Value );
        }
    }
}