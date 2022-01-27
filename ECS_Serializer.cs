using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using VodeoECS.Internal;

namespace VodeoECS
{
    /// <summary>
    /// ECS Serializer class. For saving and loading the ECS World state as a json string
    /// </summary>
    public class ECS_Serializer
    {
        private World world;
        private ScheduleQueueManager queuesManager;
        private JsonSerializer serializer;

        private EventEmitter<WorldLoadedEvent> eventEmitter;

        /// <summary>
        /// Constructs an ECS Serializer.
        /// </summary>
        /// <param name="world">The World to serialize.</param>
        public ECS_Serializer ( World world )
        {
            this.world = world;
            this.queuesManager = world.ScheduleQueues;
            this.serializer = world.Prototypes.GetSerializer();
            this.eventEmitter = world.Events.GetEmitter<WorldLoadedEvent>( null );
        }

        /// <summary>
        /// Serializes the ECS World state to a json string.
        /// </summary>
        /// <returns>The serialized World state as a json string.</returns>
        public string SerializeWorld ( )
        {
            StringBuilder sb = new StringBuilder( );
            StringWriter sw = new StringWriter( sb );
            JsonWriter writer = new JsonTextWriter( sw );
            writer.Formatting = Formatting.Indented;

            writer.WriteStartObject( );
            {
                writer.WritePropertyName( "ECS_Entities" );
                writer.WriteStartArray( );
                {
                    SerializedWorldData data = world.SerializeToBytes( );
                    serializer.Serialize( writer, this.Compress( data.entities ) );
                    serializer.Serialize( writer, data.nextfree );
                    serializer.Serialize( writer, data.recyclenext );
                    serializer.Serialize( writer, Time.time + world.timeOffset );
                }
                writer.WriteEndArray( );

                NamedRegistry<Type> types = world.GetComponentTypeRegistry( );
                foreach ( RegistryIndex<Type> type in types.ByIndex )
                {
                    writer.WritePropertyName( types.GetName( type ) );
                    IComponentPool pool = world.GetComponentPoolDynamic( type );

                    SerializedPoolData bytes = pool.SerializeToBytes( );
                    writer.WriteStartArray( );
                    {
                        if ( bytes.filterIndices != null )
                        {
                            serializer.Serialize( writer, this.Compress( bytes.filterIndices ) );
                        }
                        if ( bytes.elementCounts != null )
                        {
                            serializer.Serialize( writer, this.Compress( bytes.elementCounts ) );
                        }
                        serializer.Serialize( writer, this.Compress( bytes.entities ) );
                        serializer.Serialize( writer, this.Compress( bytes.components ) );
                    }
                    writer.WriteEndArray( );

                }

                writer.WritePropertyName( "ECS_ScheduleQueues" );
                writer.WriteStartObject( );
                {
                    NamedRegistry<NativePriorityQueue<Entity>> queues = queuesManager.GetQueueRegistry( );
                    foreach ( RegistryIndex<NativePriorityQueue<Entity>> index in queues.ByIndex )
                    {
                        writer.WritePropertyName( queues.GetName( index ) );
                        serializer.Serialize( writer, this.Compress( queues[index].SerializeToBytes( ) ) );
                    }
                }
                writer.WriteEndObject( );
            }
            writer.WriteEndObject( );

            return sb.ToString( );
        }

        /// <summary>
        /// Deserializes an ECS World state from a json string.
        /// </summary>
        /// <param name="data">The serialized ECS World state as json string to deserialize.</param>
        public void DeserializeWorld ( string data )
        {
            JObject json = JObject.Parse( data );


            SerializedWorldData worldData = new SerializedWorldData( );

            JsonReader worldReader = json["ECS_Entities"][0].CreateReader( );
            worldData.entities = this.Decompress( serializer.Deserialize<byte[]>( worldReader ) );
            worldData.nextfree = serializer.Deserialize<int>( json["ECS_Entities"][1].CreateReader( ) );
            worldData.recyclenext = serializer.Deserialize<int>( json["ECS_Entities"][2].CreateReader( ) );
            world.timeOffset = serializer.Deserialize<float>( json["ECS_Entities"][3].CreateReader( ) ) - Time.time;

            world.DeserializeFromBytes( worldData );

            NamedRegistry<Type> types = world.GetComponentTypeRegistry( );
            foreach ( RegistryIndex<Type> type in types.ByIndex )
            {
                IComponentPool pool = world.GetComponentPoolDynamic( type );

                SerializedPoolData bytes = new SerializedPoolData( );
                JArray jarray = ( JArray )json[types.GetName( type )];

                Type poolType = pool.GetType( );

                if ( poolType.GetGenericTypeDefinition( ) == typeof( DataComponentPool<> ) )
                {
                    bytes.entities = this.Decompress( serializer.Deserialize<byte[]>( jarray[0].CreateReader( ) ) );
                    bytes.components = this.Decompress( serializer.Deserialize<byte[]>( jarray[1].CreateReader( ) ) );
                }
                else if ( poolType.GetGenericTypeDefinition( ) == typeof( FilterComponentPool<> ) )
                {
                    bytes.filterIndices = this.Decompress( serializer.Deserialize<byte[]>( jarray[0].CreateReader( ) ) );
                    bytes.entities = this.Decompress( serializer.Deserialize<byte[]>( jarray[1].CreateReader( ) ) );
                    bytes.components = this.Decompress( serializer.Deserialize<byte[]>( jarray[2].CreateReader( ) ) );
                }
                else if ( poolType.GetGenericTypeDefinition( ) == typeof( ListComponentPool<> ) )
                {
                    bytes.elementCounts = this.Decompress( serializer.Deserialize<byte[]>( jarray[0].CreateReader( ) ) );
                    bytes.entities = this.Decompress( serializer.Deserialize<byte[]>( jarray[1].CreateReader( ) ) );
                    bytes.components = this.Decompress( serializer.Deserialize<byte[]>( jarray[2].CreateReader( ) ) );
                }
                else
                {
                    throw new Exception( "Unknown pool type" );
                }

                pool.DeserializeFromBytes( bytes );
            }

            NamedRegistry<NativePriorityQueue<Entity>> queues = queuesManager.GetQueueRegistry( );
            foreach ( RegistryIndex<NativePriorityQueue<Entity>> index in queues.ByIndex )
            {
                JsonReader queueReader = json["ECS_ScheduleQueues"][queues.GetName( index )].CreateReader( );
                byte[] bytes = this.Decompress( serializer.Deserialize<byte[]>( queueReader ) );
                queues[index].DeserializeFromBytes( bytes );
            }

            eventEmitter.CreateEvent( new WorldLoadedEvent( ) );
        }


        private byte[] Compress ( byte[] data )
        {
            MemoryStream stream = new MemoryStream( data.Length );
            DeflateStream zipper = new DeflateStream( stream, System.IO.Compression.CompressionLevel.Fastest );
            zipper.Write( data, 0, data.Length );
            zipper.Dispose( );
            stream.Dispose( );
            return stream.ToArray( );
        }

        private byte[] Decompress ( byte[] data )
        {
            MemoryStream input = new MemoryStream( data );
            MemoryStream output = new MemoryStream( data.Length );
            DeflateStream unzipper = new DeflateStream( input, CompressionMode.Decompress );
            unzipper.CopyTo( output );
            unzipper.Dispose( );
            input.Dispose( );
            output.Dispose( );
            return output.ToArray( );
        }

    }
}