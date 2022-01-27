using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    public class StringRegistryConverter : JsonConverter<RegistryIndex<string>>
    {
        protected Registry<string> registry;
        public StringRegistryConverter ( Registry<string> registry )
        {
            this.registry = registry;
        }

        public override void WriteJson ( JsonWriter writer, RegistryIndex<string> value, JsonSerializer serializer )
        {
            if ( registry.Count > 0 )
                writer.WriteValue( registry[value] );
            else
                writer.WriteValue( "Registry of type " + registry.GetType( ) + " is empty" );
        }

        public override RegistryIndex<string> ReadJson ( JsonReader reader, Type objectType, RegistryIndex<string> existingValue, bool hasExistingValue, JsonSerializer serializer )
        {
            RegistryIndex<string> index;
            if ( !registry.TryGetIndex( ( string )reader.Value, out index ) )
            {
                index = registry.Register( ( string )reader.Value );
            }
            return index;
        }
    }
}