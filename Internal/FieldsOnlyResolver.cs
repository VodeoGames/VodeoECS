using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace VodeoECS.Internal
{
    /// <summary>
    /// For internal use by the PrototypeManager class.
    /// </summary>
    class FieldsOnlyResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties ( Type type, MemberSerialization memberSerialization )
        {
            IList<JsonProperty> props = base.CreateProperties( type, memberSerialization );
            List<JsonProperty> newprops = new List<JsonProperty>( );
            IEnumerable<FieldInfo> fields = type.GetRuntimeFields( );
            foreach ( JsonProperty prop in props )
            {
                foreach ( FieldInfo field in fields )
                {
                    if ( prop.PropertyName == field.Name )
                    {
                        newprops.Add( prop );
                    }
                }
            }
            return newprops;
        }
    }
}