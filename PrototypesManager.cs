using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using UnityEngine;
using VodeoECS.Internal;

namespace VodeoECS
{
    /// <summary>
    /// The Prototypes Manager is used to load and track prototype definitions.
    /// </summary>
    public class PrototypesManager
    {
        private World world;
        private NamedRegistry<Entity> prototypeRegistry;
        private JsonSerializer serializer;
        private EventEmitter<PrototypeLoadedEvent> prototypeEvents;

        /// <summary>
        /// For internal use by the World Class. Normally Prototypes Managers are created by a World and requested from it.
        /// </summary>
        /// <param name="world">The World associated with this Prototypes Manager.</param>
        public PrototypesManager ( World world )
        {
            this.world = world;
            this.serializer = new JsonSerializer( );
            this.serializer.ContractResolver = new FieldsOnlyResolver( );
            this.serializer.NullValueHandling = NullValueHandling.Include;
            this.prototypeRegistry = new NamedRegistry<Entity>( );
            this.serializer.Converters.Add( new NamedRegistryConverter<Entity>( this.prototypeRegistry ) );
            this.prototypeEvents = world.Events.GetEmitter<PrototypeLoadedEvent>( null );
        }

        public JsonSerializer GetSerializer ( )
        {
            return serializer;
        }


        public class FileSorter : IComparer<FileInfo>
        {
            int IComparer<FileInfo>.Compare ( FileInfo x, FileInfo y )
            {
                return ( ( new CaseInsensitiveComparer( ) ).Compare( y.FullName, x.FullName ) );
            }
        }

        /// <summary>
        /// For internal use by the World class. Load and register all json prototype definitions in the Prototypes data folder.
        /// </summary>
        public void LoadPrototypes ( )
        {
            DirectoryInfo directoryInfo = new DirectoryInfo( Application.streamingAssetsPath + "/Prototypes" );

            {
                FileInfo[] allFiles = directoryInfo.GetFiles( "*.json" );
                Array.Sort( allFiles, new FileSorter( ) );

                foreach ( FileInfo file in allFiles )
                {
                    Entity entity = world.CreatePrototype( );
                    this.prototypeRegistry.Register( entity, Path.GetFileNameWithoutExtension( file.Name ) );
                }


                DirectoryInfo[] allFolders = directoryInfo.GetDirectories( );
                foreach ( DirectoryInfo directory in allFolders )
                {
                    allFiles = directory.GetFiles( "*.json" );
                    Array.Sort( allFiles, new FileSorter( ) );

                    foreach ( FileInfo file in allFiles )
                    {
                        Entity entity = world.CreatePrototype( );
                        this.prototypeRegistry.Register( entity, Path.GetFileNameWithoutExtension( file.Name ) );
                    }
                }
            }

            {
                FileInfo[] allFiles = directoryInfo.GetFiles( "*.json" );
                Array.Sort( allFiles, new FileSorter( ) );

                foreach ( FileInfo file in allFiles )
                {
                    Entity proto = LoadFromJSON( file );
                    this.prototypeEvents.CreateEvent( new PrototypeLoadedEvent( ) { prototype = proto } );
                }

                DirectoryInfo[] allFolders = directoryInfo.GetDirectories( );
                foreach ( DirectoryInfo directory in allFolders )
                {
                    allFiles = directory.GetFiles( "*.json" );
                    Array.Sort( allFiles, new FileSorter( ) );

                    foreach ( FileInfo file in allFiles )
                    {
                        Entity proto = LoadFromJSON( file );
                        this.prototypeEvents.CreateEvent( new PrototypeLoadedEvent( ) { prototype = proto } );
                    }
                }
            }

        }

        /// <summary>
        /// Writes reference json definitions for each Component type to the ComponentFormats data folder.
        /// </summary>
        public void DumpFormatting ( )
        {
            foreach ( Type type in world.GetComponentTypeRegistry( ) )
            {
                string path = Application.streamingAssetsPath + "/ComponentFormats/" + type.Name + ".json";
                StreamWriter writer = new StreamWriter( path, false );

                object thing = FormatterServices.GetUninitializedObject( type );
                JToken json = JToken.FromObject( thing, serializer );
                writer.WriteLine( json.ToString( Formatting.Indented ) );
                writer.WriteLine( );
                writer.Dispose( );
            }
        }

        /// <summary>
        /// Get a registered Prototype Entity by its Prototype name.
        /// </summary>
        /// <param name="name">The name of the Prototype, as defined in the data folder.</param>
        /// <returns>The Prototype Entity.</returns>
        public Entity GetPrototype ( string name )
        {
            return this.prototypeRegistry[this.prototypeRegistry.GetIndexByName( name )];
        }
        /// <summary>
        /// Get a registered Prototype Entity by its Registry Index. Used when loading prototypes that refer to other prototypes
        /// </summary>
        /// <param name="index">The Registry Index of the Prototype. Usually from a field in a loaded prototype.</param>
        /// <returns>The Prototype Entity.</returns>
        public Entity GetPrototype ( RegistryIndex<Entity> index )
        {
            return this.prototypeRegistry[index];
        }

        /// <summary>
        /// Get the name of a registered Prototype Entity.
        /// </summary>
        /// <param name="entity">The Prototype Entity.</param>
        /// <returns>The name of the Prototype Entity.</returns>
        public string GetName ( Entity entity )
        {
            RegistryIndex<Entity> index;
            if ( this.prototypeRegistry.TryGetIndex( entity, out index ) )
            {
                return this.prototypeRegistry.GetName( index );
            }
            else
            {
                return "Unnamed Prototype";
            }
        }

        private Entity LoadFromJSON ( FileInfo file )
        {
            Entity entity = this.prototypeRegistry[this.prototypeRegistry.GetIndexByName( Path.GetFileNameWithoutExtension( file.Name ) )];
            FileStream stream = file.Open( FileMode.Open, FileAccess.Read );
            StreamReader reader = new StreamReader( stream );
            JObject json = JObject.Parse( reader.ReadToEnd( ) );
            stream.Close( );

            try
            {
                HashSet<string> loopList = new HashSet<string>( );
                while ( json.ContainsKey( "ParentPrototype" ) )
                {
                    JToken token = json.SelectToken( "ParentPrototype" );
                    string parentName = token.Value<string>( );
                    json.Remove( "ParentPrototype" );
                    if ( loopList.Contains( parentName ) ) throw new FormatException( "Prototype inheritance loop detected with prototype " + parentName + " in " + file.Name );
                    loopList.Add( parentName );

                    FileInfo parentFile = new FileInfo( Application.streamingAssetsPath + "/Prototypes/" + parentName + ".json" );
                    if ( !parentFile.Exists ) throw new FormatException( "Could not load parent prototype " + parentName + " in " + file.Name );

                    stream = parentFile.Open( FileMode.Open, FileAccess.Read );
                    reader = new StreamReader( stream );
                    JObject parentjson = JObject.Parse( reader.ReadToEnd( ) );
                    stream.Close( );

                    parentjson.Merge( json, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace } );
                    json = parentjson;
                }

                NamedRegistry<Type> componentTypeRegistry = world.GetComponentTypeRegistry( );

                foreach ( KeyValuePair<string, JToken> pair in json )
                {
                    RegistryIndex<Type> typeID = componentTypeRegistry.GetIndexByName( pair.Key );
                    IComponentPool pool = world.GetComponentPoolDynamic( typeID );
                    Type poolType = pool.GetType( );
                    Type tocheck = poolType.GetGenericTypeDefinition( );

                    if ( tocheck == typeof( DataComponentPool<> ) )
                    {
                        dynamic data = pair.Value.ToObject( componentTypeRegistry[typeID], serializer );
                        poolType.GetMethod( "AddComponent" ).Invoke( pool, new object[] { entity, data } );
                    }
                    else if ( tocheck == typeof( ListComponentPool<> ) )
                    {
                        JArray array = pair.Value as JArray;
                        if ( array == null ) throw new FormatException( "Token " + pair.Key + " does not have a properly formatted array as value" );
                        ComponentIndex index = ( ComponentIndex )poolType.GetMethod( "AddComponent" ).Invoke( pool, new object[] { entity, array.Count } );
                        foreach ( JToken token in array )
                        {
                            dynamic data = token.ToObject( componentTypeRegistry[typeID], serializer );
                            object listAccessor = poolType.GetMethod( "get_Item", new Type[] { typeof( ComponentIndex ) } ).Invoke( pool, new object[] { index } );
                            listAccessor.GetType( ).GetMethod( "AppendElement" ).Invoke( listAccessor, new object[] { data } );
                        }
                    }
                    else if ( tocheck == typeof( FilterComponentPool<> ) )
                    {
                        dynamic data = pair.Value.ToObject( componentTypeRegistry[typeID], serializer );
                        poolType.GetMethod( "AddComponent" ).Invoke( pool, new object[] { entity, data } );
                    }
                    else
                    {
                        throw new ArgumentException( "invalid component pool" );
                    }
                }

                return entity;
            }
            catch ( Exception exception )
            {
                throw new Exception( "Error while loading prototype " + file.Name + " : " + exception.Message );
            }
        }
    }
}