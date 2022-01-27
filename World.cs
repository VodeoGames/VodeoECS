using VodeoECS.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using VodeoECS.Standard;
using UnityEngine;

namespace VodeoECS
{
    /// <summary>
    /// ECS World class. A World manages general ECS state, including tracking entities and component pools.
    /// </summary>
    public class World
    {
#if DEBUG
        public static World singleton;
        public ObjectRenderSystem objectRenderSystem;
#endif
        /// <summary>
        /// Have the archetypes been initialized yet?
        /// </summary>
        public bool Initialized { get { return this.initializedArchetypes; } }
        /// <summary>
        /// The Prototypes Manager associated with this World.
        /// </summary>
        public PrototypesManager Prototypes { get { return this.prototypesManager; } }
        /// <summary>
        /// The System Manager associated with this World.
        /// </summary>
        public SystemManager Systems { get { return this.systemManager; } }
        /// <summary>
        /// The Schedule Queue Manager associated with this World.
        /// </summary>
        public ScheduleQueueManager ScheduleQueues { get { return this.queuesManager; } }
        /// <summary>
        /// The Event Manager associated with this World.
        /// </summary>
        public EventManager Events { get { return this.eventManager; } }

        public float timeOffset = 0;

        private Unity.Mathematics.Random random;

        private EventManager eventManager;
        private PrototypesManager prototypesManager;
        private SystemManager systemManager;
        private ScheduleQueueManager queuesManager;

        private int startingPoolCapacity;
        private int recycleNext = 0;
        private int nextFree = 1;
        private NativeList<Entity> entities;
        private List<HashSet<RegistryIndex<Type>>> componentRegistry;
        private HashSet<Entity> dirtyEntities;

        private List<IComponentPool> componentPools;
        private NamedRegistry<Type> componentTypeRegistry;

        private Registry<IFilterComponent> filterRegistry;
        private Registry<FilterCombination> filterCombinationRegistry;
        private Dictionary<RegistryIndex<FilterCombination>, HashSet<RegistryIndex<FilterCombination>>> superFilters;

        private Registry<ArchetypeData> archetypeRegistry;
        private Registry<ArchetypeData> metaArchetypeRegistry;
        private Dictionary<HashSet<RegistryIndex<Type>>, HashSet<RegistryIndex<ArchetypeData>>> archetypeCache;
        private Dictionary<RegistryIndex<ArchetypeData>, HashSet<RegistryIndex<ArchetypeData>>> superArchetypes;
        private Dictionary<RegistryIndex<Type>, HashSet<RegistryIndex<ArchetypeData>>> metaArchetypeByType;

        private Dictionary<RegistryIndex<ArchetypeData>, Dictionary<RegistryIndex<FilterCombination>, Taxon>> taxa;

        private List<LinkedList<RegistryIndex<ArchetypeData>>> archetypeMap;
        private List<LinkedList<RegistryIndex<ArchetypeData>>> metaArchetypeMap;
        private RegistryIndex<ArchetypeData> defaultMetaArchetype;
        private List<RegistryIndex<FilterCombination>> filterCombinationMap;
        private RegistryIndex<FilterCombination> defaultFilterCombination;

        static private int taxonCount = 2; //start at 2 to skip null ID and prototype ID

        private bool initializedArchetypes = false;

#if DEBUG
        public Entity instantiatedEntity { get; private set; }
#endif

        /// <summary>
        /// ECS World constructor.
        /// </summary>
        /// <param name="entityCapacity">Initial capacity for total of entities.</param>
        /// <param name="startingPoolCapacity">Initial capacity for components in pools.</param>
        public World ( int entityCapacity = ushort.MaxValue, int startingPoolCapacity = 1)
        {
#if DEBUG
            World.singleton = this;
#endif
            random = new Unity.Mathematics.Random( 394362 );

            eventManager = new EventManager( this );
            queuesManager = new ScheduleQueueManager( 64 );
            prototypesManager = new PrototypesManager( this );
            systemManager = new SystemManager( this );

            entities = new NativeList<Entity>( entityCapacity, Allocator.Persistent );
            this.componentRegistry = new List<HashSet<RegistryIndex<Type>>>( entityCapacity );
            this.componentRegistry.Add( new HashSet<RegistryIndex<Type>>( ) );
            this.dirtyEntities = new HashSet<Entity>( );
            this.startingPoolCapacity = startingPoolCapacity;

            this.componentPools = new List<IComponentPool>( );
            this.componentTypeRegistry = new NamedRegistry<Type>( );

            this.filterRegistry = new NamedRegistry<IFilterComponent>( );
            this.filterCombinationRegistry = new Registry<FilterCombination>( );
            this.superFilters = new Dictionary<RegistryIndex<FilterCombination>, HashSet<RegistryIndex<FilterCombination>>>( );
            this.defaultFilterCombination = this.GetFilterCombinationIndex( FilterCombination.Default );

            archetypeRegistry = new Registry<ArchetypeData>( );
            metaArchetypeRegistry = new Registry<ArchetypeData>( );
            archetypeCache = new Dictionary<HashSet<RegistryIndex<Type>>, HashSet<RegistryIndex<ArchetypeData>>>( HashSet<RegistryIndex<Type>>.CreateSetComparer( ) );
            superArchetypes = new Dictionary<RegistryIndex<ArchetypeData>, HashSet<RegistryIndex<ArchetypeData>>>( );
            metaArchetypeByType = new Dictionary<RegistryIndex<Type>, HashSet<RegistryIndex<ArchetypeData>>>( );

            archetypeMap = new List<LinkedList<RegistryIndex<ArchetypeData>>>( entityCapacity );
            archetypeMap.Add( new LinkedList<RegistryIndex<ArchetypeData>>( ) );
            metaArchetypeMap = new List<LinkedList<RegistryIndex<ArchetypeData>>>( entityCapacity );
            metaArchetypeMap.Add( new LinkedList<RegistryIndex<ArchetypeData>>( ) );

            filterCombinationMap = new List<RegistryIndex<FilterCombination>>( entityCapacity );
            filterCombinationMap.Add( defaultFilterCombination );

            taxa = new Dictionary<RegistryIndex<ArchetypeData>, Dictionary<RegistryIndex<FilterCombination>, Taxon>>( );
        }

        /// <summary>
        /// Creates a new prototype Entity.
        /// </summary>
        /// <returns>The prototype Entity that was created.</returns>
        public Entity CreatePrototype ( )
        {
            return CreateEntity( true );
        }

        /// <summary>
        /// Creates a new Entity based on the given prototype. The new Entity will have the same component data as the prototype.
        /// </summary>
        /// <param name="prototype">The prototype to use as template.</param>
        /// <returns>The created Entity.</returns>
        public Entity InstantiatePrototype ( Entity prototype )
        {
            Entity newEntity = this.CreateEntity( false );
#if DEBUG
            this.instantiatedEntity = newEntity;
#endif
            HashSet<RegistryIndex<Type>> prototypeTypes = this.componentRegistry[prototype.ID];

            this.SetArchetypesForNewEntity( prototype, newEntity, prototypeTypes );

            this.componentRegistry[newEntity.ID] = new HashSet<RegistryIndex<Type>>( this.componentRegistry[prototype.ID] );

            UpdateFilterCombination( prototype );
            RegistryIndex<FilterCombination> combination = this.filterCombinationMap[prototype.ID];

            Taxon defaultTaxon = this.GetExactTaxon( defaultMetaArchetype, combination );
            foreach ( RegistryIndex<Type> t in prototypeTypes )
            {
                bool inArchetype = false;
                foreach ( RegistryIndex<ArchetypeData> metaArchetype in this.metaArchetypeMap[newEntity.ID] )
                {
                    Taxon taxon = this.GetExactTaxon( metaArchetype, combination );
                    if ( this.metaArchetypeByType[t].Contains( metaArchetype ) ) //Component is in Archetype
                    {
                        this.componentPools[t.ID].InstantiatePrototype( prototype, newEntity, taxon ); //Add to Filtered Taxon
                        inArchetype = true;
                        break;
                    }
                }
                if ( !inArchetype )
                {
                    this.componentPools[t.ID].InstantiatePrototype( prototype, newEntity, defaultTaxon ); //Add to Default Taxon
                }
            }
            return newEntity;
        }

        /// <summary>
        /// For internal use by ComponentPools. Registers the addition of a new component.
        /// </summary>
        /// <param name="entity">The Entity the component was added to.</param>
        /// <param name="type">The type of the new component.</param>
        public void RegisterNewComponent ( Entity entity, RegistryIndex<Type> type )
        {
            this.componentRegistry[entity.ID].Add( type );

            if ( !entity.prototype )
                this.dirtyEntities.Add( entity );
        }
        /// <summary>
        /// For internal use by FilterComponentPools. Registers a change of filter component.
        /// </summary>
        /// <param name="entity">The Entity associated with the changed Filter Component.</param>
        public void RegisterFilterChange ( Entity entity )
        {
            if ( !entity.prototype )
                this.dirtyEntities.Add( entity );
        }

        /// <summary>
        /// For internal use by ComponentPools. Registers the removal of a component.
        /// </summary>
        /// <param name="entity">The Entity the component was removed from.</param>
        /// <param name="type">The type of the removed component.</param>
        public void RegisterComponentRemoval ( Entity entity, RegistryIndex<Type> type )
        {
            this.componentRegistry[entity.ID].Remove( type );

            if ( !entity.prototype )
                this.dirtyEntities.Add( entity );
        }

        /// <summary>
        /// For internal use by the System Manager. Emit a DestroyEvent instead when you with to destroy an Entity.
        /// Destroys an Entity. This will invalidate component indices and accessors.
        /// </summary>
        /// <param name="entity">The Entity to destroy.</param>
        public void DestroyEntity ( Entity entity )
        {
            HashSet<RegistryIndex<Type>> components = componentRegistry[entity.ID];
            while ( components.Count > 0 )
            {
                RegistryIndex<Type> type = components.First( );
                this.componentPools[type.ID].DestroyComponent( entity );
            }
            this.UnregisterEntity( entity );

            entities[entity.ID] = new Entity( recycleNext, false );
            recycleNext = entity.ID;
        }

        /// <summary>
        /// Does the Entity exist?
        /// </summary>
        /// <param name="entity">The Entity to check for existence.</param>
        /// <returns>True if the Entity requested exists. False if it does not.</returns>
        public bool HasEntity ( Entity entity )
        {
            return ( entity.ID < nextFree && entity.ID < entities.Length && entities[entity.ID].Equals( entity ) );
        }

        /// <summary>
        /// Returns the Component types associated with the Entity, in RegistryIndex form.
        /// </summary>
        /// <param name="entity">The Component types of this Entity will be returned.</param>
        /// <returns>Set of Comoponent type indices associated with the Entity.</returns>
        public HashSet<RegistryIndex<Type>> GetTypes ( Entity entity )
        {
            if ( HasEntity( entity ) )
            {
                HashSet<RegistryIndex<Type>> types = this.componentRegistry[entity.ID];
                return types;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the registered ComponentPool associated with the Component type index, as an Interface. (Use the generic Component Pool getters instead for most uses.)
        /// </summary>
        /// <param name="typeIndex">The index of the Component type of the pool.</param>
        /// <returns>The Component pool requested, as an Interface.</returns>
        public IComponentPool GetComponentPoolDynamic ( RegistryIndex<Type> typeIndex )
        {
            return this.componentPools[typeIndex.ID];
        }

        /// <summary>
        /// Returns the Component Type Registry.
        /// </summary>
        /// <returns>The NamedRegistry where ComponentTypes are registered.</returns>
        public NamedRegistry<Type> GetComponentTypeRegistry ( )
        {
            return this.componentTypeRegistry;
        }

        /// <summary>
        /// Request the Component Pool for a given type of Data Component. A new pool is created if it does not exist yet.
        /// </summary>
        /// <typeparam name="T">The Data Component type requested.</typeparam>
        /// <returns>The Component Pool for the Data Component type requested.</returns>
        public DataComponentPool<T> GetDataComponentPool<T> ( ) where T : unmanaged, IDataComponent
        {
            RegistryIndex<Type> pool;
            if ( this.componentTypeRegistry.TryGetIndex( typeof( T ), out pool ) )
            {
                return ( DataComponentPool<T> )componentPools[pool.ID];
            }
            else
            {
                return this.RegisterDataComponentType<T>( startingPoolCapacity );
            }
        }
        /// <summary>
        /// Request the Component Pool for a given type of List Element. A new pool is created if it does not exist yet.
        /// </summary>
        /// <typeparam name="T">The List Element type requested.</typeparam>
        /// <returns>The Component Pool for the List Element type requested.</returns>
        public ListComponentPool<T> GetListComponentPool<T> ( ) where T : unmanaged, IElementComponent
        {
            RegistryIndex<Type> pool;
            if ( this.componentTypeRegistry.TryGetIndex( typeof( T ), out pool ) )
            {
                return ( ListComponentPool<T> )componentPools[pool.ID];
            }
            else
            {
                return this.RegisterListComponentType<T>( startingPoolCapacity );
            }
        }
        /// <summary>
        /// Request the Component Pool for a given type of Filter Component. A new pool is created if it does not exist yet.
        /// </summary>
        /// <typeparam name="T">The Filer Component type requested.</typeparam>
        /// <returns>The Component Pool for the Filter Component type requested.</returns>
        public FilterComponentPool<T> GetFilterComponentPool<T> ( ) where T : unmanaged, IFilterComponent<T>
        {
            RegistryIndex<Type> pool;
            if ( this.componentTypeRegistry.TryGetIndex( typeof( T ), out pool ) )
            {
                return ( FilterComponentPool<T> )componentPools[pool.ID];
            }
            else
            {
                return this.RegisterFilterComponentType<T>( startingPoolCapacity );
            }
        }
        /// <summary>
        /// Initialize the Archetype system. This should be called after all Systems have been created and registered their Archetypes, but before the game starts.
        /// </summary>
        public void InitializeSystems ( )
        {
            //Archetypes
            {
                if ( this.initializedArchetypes ) throw new Exception( "Archetypes already initialized" );

                this.archetypeRegistry.Register( new ArchetypeData( new HashSet<RegistryIndex<Type>>( ), new HashSet<RegistryIndex<Type>>( ) ) );
                foreach ( RegistryIndex<ArchetypeData> inputArchetype in this.archetypeRegistry.ByIndex )
                {
                    this.superArchetypes.Add( inputArchetype, new HashSet<RegistryIndex<ArchetypeData>>( ) );
                }
                foreach ( ArchetypeData inputArchetype in this.archetypeRegistry )
                {
                    RegisterMetaArchetype( inputArchetype );
                }
                //register default taxon
                defaultMetaArchetype = this.metaArchetypeRegistry.GetIndex( new ArchetypeData( new RegistryIndex<Type>[] { }, new RegistryIndex<Type>[] { } ) );
                taxa[defaultMetaArchetype][defaultFilterCombination] = Taxon.Default;
                taxonCount--; //decrement taxonCount because we manually set the taxon to Taxon.Default

                this.initializedArchetypes = true;
            }

            //Prototypes
            {
                prototypesManager.LoadPrototypes( );
#if UNITY_EDITOR
                prototypesManager.DumpFormatting( );
#endif
            }

            //Events
            {
                this.eventManager.Initialize( );
            }

            //Systems
            {
                this.systemManager.Initialize( );
            }

            this.timeOffset = - Time.time;
        }

        /// <summary>
        /// Define a new Archetype based on the provided Component types. This must be called before the InitializeArchetypes is called.
        /// </summary>
        /// <param name="types">The Component Pools associated with each Component Type to be included in the new Archetype.</param>
        /// <returns>The Archetype identifier of the new Archetype.</returns>
        public Archetype DefineArchetype ( params IComponentPool[] types )
        {
#if DEBUG
            if ( this.initializedArchetypes ) throw new Exception( "Trying to define an archetype after archetype initialization" );
            if ( types.Length <= 0 ) throw new Exception( "Archetypes must include at least 2 types" );
#endif

            List<RegistryIndex<Type>> components = new List<RegistryIndex<Type>>( );
            List<RegistryIndex<Type>> filters = new List<RegistryIndex<Type>>( );
            for ( int i = 0; i < types.Length; i++ )
            {
                Type componentType = this.componentTypeRegistry[types[i].ComponentType];
                if ( typeof( IFilterComponent ).IsAssignableFrom( componentType ) )
                {
                    filters.Add( types[i].ComponentType );
                }
                else
                {
                    components.Add( types[i].ComponentType );
                }
            }
            ArchetypeData archetype = new ArchetypeData( components, filters );

            RegistryIndex<ArchetypeData> index;
            if ( !this.archetypeRegistry.TryGetIndex( archetype, out index ) )
            {
                index = this.archetypeRegistry.Register( archetype );
            }

            return new Archetype( index );
        }

        /// <summary>
        /// Query a single random Entity matching the given Query, with a component in the given Component Pool.
        /// </summary>
        /// <param name="pool">A Component Pool for a Component Type which is part of the Archetype.</param>
        /// <param name="query">The Query to get a random Entity from.</param>
        /// <returns>A random Entity matching the Query.</returns>
        public Entity GetRandomEntityOfArchetype( IComponentPool pool, Query query )
        {
            List<int> lengths = new List<int>( );
            int totalLength = 0;
            Entity randomEntity = Entity.Null;
            foreach ( Taxon taxon in query )
            {
                int length = pool.GetEntitySlice(taxon).Length;
                lengths.Add( length );
                totalLength += length;
            }
            int productIndex = random.NextInt( totalLength );
            for ( int j = 0; j < query.Count; j++ )
            {
                if ( lengths[j] <= productIndex )
                {
                    productIndex -= lengths[j];
                }
                else
                {
                    randomEntity = pool.GetEntitySlice( query[j] )[productIndex];
                }
            }
            return randomEntity;
        }

        /// <summary>
        /// Make a Query for Components matching an Archetype.
        /// </summary>
        /// <param name="archetype">The Archetype Queried.</param>
        /// <returns>The resulting Query, for use in accessing Component Pools.</returns>
        public Query MakeQuery ( Archetype archetype )
        {
#if DEBUG
            if ( !this.initializedArchetypes ) throw new Exception( "Requesting Taxa before Archetype initialization" );
#endif
            ProcessComponentChanges( );

            List<Taxon> list = new List<Taxon>( taxa.Count );
            HashSet<RegistryIndex<ArchetypeData>> metaArchetypes = this.superArchetypes[archetype.index];
            foreach ( RegistryIndex<ArchetypeData> metaArchetype in metaArchetypes )
            {
                foreach ( Taxon taxon in taxa[metaArchetype].Values )
                {
                    list.Add( taxon );
                }
            }
            return new Query( list );
        }
        /// <summary>
        /// Make a Query for Components matching an Archetype and a number of specified Filters to match exactly.
        /// </summary>
        /// <param name="archetype">The Archetype Queried.</param>
        /// <param name="filters">The Filter Components to match exactly. The Filter types must be part of the Archetype, but any can be omitted.</param>
        /// <returns>The resulting Query, for use in accessing Component Pools.</returns>
        public Query MakeQuery ( Archetype archetype, params IFilterComponent[] filters )
        {
#if DEBUG
            if ( !this.initializedArchetypes ) throw new Exception( "Requesting Taxa before Archetype initialization" );
            ArchetypeData a = this.archetypeRegistry[archetype.index];
#endif
            ProcessComponentChanges( );

            RegistryIndex<IFilterComponent>[] indexes = new RegistryIndex<IFilterComponent>[filters.Length];
            for ( int i = 0; i < indexes.Length; i++ )
            {
                if ( this.filterRegistry.TryGetIndex( filters[i], out indexes[i] ) )
                {
#if DEBUG
                    RegistryIndex<Type> type = this.componentTypeRegistry.GetIndex( filters[i].GetType( ) );
                    if ( !a.filters.Contains( type ) )
                    {
                        throw new ArgumentException( "Archetype does not contain filter of type " + filters[i].GetType( ) + " but one was passed as argument to GetTaxa" );
                    }
#endif
                }
                else
                {
                    return Query.Null; // one of the filters is unknown, return empty query
                }
            }

            FilterCombination filterCombination = new FilterCombination( indexes );
            List<Taxon> list = new List<Taxon>( taxa.Count );

            RegistryIndex<FilterCombination> filterIndex = this.GetFilterCombinationIndex( filterCombination );
            HashSet<RegistryIndex<ArchetypeData>> metaArchetypes = this.superArchetypes[archetype.index];
            foreach ( RegistryIndex<ArchetypeData> metaArchetype in metaArchetypes )
            {
                Taxon taxon;
                if ( taxa[metaArchetype].TryGetValue( filterIndex, out taxon ) )
                {
                    list.Add( taxa[metaArchetype][filterIndex] );
                }
                foreach ( RegistryIndex<FilterCombination> index in superFilters[filterIndex] )
                {
                    if ( taxa[metaArchetype].TryGetValue( index, out taxon ) )
                    {
                        list.Add( taxon );
                    }
                }
            }

            return new Query( list );
        }

        /// <summary>
        /// Find out if an Entity is of a given Archetype.
        /// </summary>
        /// <param name="entity">The Entity to check.</param>
        /// <param name="archetype">The Archetype to test the Entity for.</param>
        /// <returns>Returns true if the Entity matches the Archetype, false otherwise.</returns>
        public bool IsEntityOfArchetype(Entity entity, Archetype archetype)
        {
            foreach ( RegistryIndex<ArchetypeData> index in archetypeMap[entity.ID] )
            {
                if ( archetype.index == index ) return true;
            }
            return false;
        }

        /// <summary>
        /// For internal use by the ECS Serializer. Serializes World-specific data. Component Pools and other data must be serialized separately.
        /// </summary>
        /// <returns>A data structure containing the world-specific data in serialized form.</returns>
        public SerializedWorldData SerializeToBytes ( )
        {
            SerializedWorldData data = new SerializedWorldData( );

            int entitySize = Marshal.SizeOf( default( Entity ) );
            data.entities = new byte[( this.entities.Length ) * entitySize];
            this.entities.AsArray( ).Reinterpret<byte>( entitySize ).CopyTo( data.entities );

            data.nextfree = this.nextFree;
            data.recyclenext = this.recycleNext;

            return data;
        }

        /// <summary>
        /// For internal use by the ECS Serializer. Deserializes World-specific data. Component Pools and other data must be deserialized separately.
        /// </summary>
        /// <param name="data">A data structure containing the world-specific data in serialized form.</param>
        public void DeserializeFromBytes ( SerializedWorldData data )
        {
            this.ResetEntities( );

            int entitySize = Marshal.SizeOf( default( Entity ) );
            NativeArray<byte> bytes = new NativeArray<byte>( data.entities, Allocator.Temp );

            this.entities.Resize( bytes.Length / entitySize, NativeArrayOptions.UninitializedMemory );
            bytes.Reinterpret<Entity>( sizeof( byte ) ).CopyTo( entities );

            this.nextFree = data.nextfree;
            this.recycleNext = data.recyclenext;

            for ( int i = 0; i < entities.Length; i++ )
            {
                this.componentRegistry.Add( new HashSet<RegistryIndex<Type>>( ) );
                this.filterCombinationMap.Add( this.defaultFilterCombination );
                this.archetypeMap.Add( new LinkedList<RegistryIndex<ArchetypeData>>( ) );
                this.metaArchetypeMap.Add( new LinkedList<RegistryIndex<ArchetypeData>>( ) );
            }

            bytes.Dispose( );
        }

        /// <summary>
        /// Dispose the native memory reserved by the World.
        /// </summary>
        public void Dispose ( )
        {
            this.initializedArchetypes = false;
            foreach ( IComponentPool pool in componentPools )
                pool.Dispose( );

            entities.Dispose( );
            systemManager.Dispose( );
            queuesManager.Dispose( );
            eventManager.Dispose( );
        }


        private void ResetEntities ( )
        {
            this.entities.Clear( );

            this.componentRegistry.Clear( );
            this.filterCombinationMap.Clear( );
            this.archetypeMap.Clear( );
            this.metaArchetypeMap.Clear( );
        }

        private DataComponentPool<T> RegisterDataComponentType<T> ( int capacity ) where T : unmanaged, IDataComponent
        {
            RegistryIndex<Type> type = componentTypeRegistry.Register( typeof( T ), typeof( T ).Name );
            this.metaArchetypeByType.Add( this.componentTypeRegistry.GetIndex( typeof( T ) ), new HashSet<RegistryIndex<ArchetypeData>>( ) );
            DataComponentPool<T> pool = new DataComponentPool<T>( this, type, capacity, this.entities.Capacity );

            componentPools.Add( pool );
            return pool;
        }
        private ListComponentPool<T> RegisterListComponentType<T> ( int capacity ) where T : unmanaged, IElementComponent
        {
            RegistryIndex<Type> type = componentTypeRegistry.Register( typeof( T ), typeof( T ).Name );
            this.metaArchetypeByType.Add( this.componentTypeRegistry.GetIndex( typeof( T ) ), new HashSet<RegistryIndex<ArchetypeData>>( ) );
            ListComponentPool<T> pool = new ListComponentPool<T>( this, type, capacity, this.entities.Capacity );
            componentPools.Add( pool );
            return pool;
        }
        private FilterComponentPool<T> RegisterFilterComponentType<T> ( int capacity ) where T : unmanaged, IFilterComponent<T>
        {
            RegistryIndex<Type> type = componentTypeRegistry.Register( typeof( T ), typeof( T ).Name );
            this.metaArchetypeByType.Add( this.componentTypeRegistry.GetIndex( typeof( T ) ), new HashSet<RegistryIndex<ArchetypeData>>( ) );
            FilterComponentPool<T> pool = new FilterComponentPool<T>( this, type, capacity, this.entities.Capacity );
            componentPools.Add( pool );
            return pool;
        }



        private RegistryIndex<ArchetypeData> RegisterMetaArchetype ( ArchetypeData metaArchetype )
        {
            RegistryIndex<ArchetypeData> index = this.metaArchetypeRegistry.Register( metaArchetype );

            foreach ( RegistryIndex<Type> type in metaArchetype.components )
            {
                this.metaArchetypeByType[type].Add( index );
            }
            foreach ( RegistryIndex<Type> type in metaArchetype.filters )
            {
                this.metaArchetypeByType[type].Add( index );
            }
            Taxon taxon = new Taxon( ( ushort )taxonCount );
            taxonCount++;
            taxa.Add( index, new Dictionary<RegistryIndex<FilterCombination>, Taxon>( ) { { this.defaultFilterCombination, taxon } } );

            //update super / sub archetypes:
            foreach ( RegistryIndex<ArchetypeData> index2 in this.archetypeRegistry.ByIndex )
            {
                ArchetypeData archetype2 = this.archetypeRegistry[index2];
                if ( archetype2.components.IsSubsetOf( metaArchetype.components ) && archetype2.filters.IsSubsetOf( archetype2.filters ) )
                {
                    this.superArchetypes[index2].Add( index );
                }
            }

            return index;
        }

        private bool UpdateFilterCombination ( Entity entity )
        {
            HashSet<RegistryIndex<Type>> types = this.componentRegistry[entity.ID];
            FilterCombination combination = FilterCombination.NewEmpty;
            foreach ( RegistryIndex<Type> typeIndex in types )
            {
                if ( this.componentPools[typeIndex.ID] is IFilterComponentPool )
                {
                    IFilterComponent boxedFilter = ( ( IFilterComponentPool )this.componentPools[typeIndex.ID] ).ReadBoxedFilterComponent( entity );
                    RegistryIndex<IFilterComponent> filterIndex;
                    if ( filterRegistry.TryGetIndex( boxedFilter, out filterIndex ) )
                    {
                        combination.filterComponentInstances.Add( filterIndex );
                    }
                    else
                    {
                        combination.filterComponentInstances.Add( filterRegistry.Register( boxedFilter ) );
                    }
                }
            }

            RegistryIndex<FilterCombination> combinationIndex = this.GetFilterCombinationIndex( combination );

            if ( combinationIndex != this.filterCombinationMap[entity.ID] )
            {
                this.filterCombinationMap[entity.ID] = combinationIndex;
                return true;
            }
            else return false;
        }

        private RegistryIndex<FilterCombination> GetFilterCombinationIndex ( FilterCombination combination )
        {
            RegistryIndex<FilterCombination> combinationIndex;
            if ( !this.filterCombinationRegistry.TryGetIndex( combination, out combinationIndex ) )
            {
                combinationIndex = this.filterCombinationRegistry.Register( combination );
                this.superFilters[combinationIndex] = new HashSet<RegistryIndex<FilterCombination>>( );

                foreach ( RegistryIndex<FilterCombination> testIndex in filterCombinationRegistry.ByIndex )
                {
                    if ( filterCombinationRegistry[testIndex].filterComponentInstances.IsProperSupersetOf( filterCombinationRegistry[combinationIndex].filterComponentInstances ) )
                    {
                        this.superFilters[combinationIndex].Add( testIndex );
                    }
                    else if ( filterCombinationRegistry[testIndex].filterComponentInstances.IsProperSubsetOf( filterCombinationRegistry[combinationIndex].filterComponentInstances ) )
                    {
                        this.superFilters[testIndex].Add( combinationIndex );
                    }
                }
            }
            return combinationIndex;
        }

        private Taxon GetExactTaxon ( RegistryIndex<ArchetypeData> metaArchetype, RegistryIndex<FilterCombination> combination )
        {
#if DEBUG
            if ( !this.initializedArchetypes ) throw new Exception( "Requesting Taxa before Archetype initialization" );
#endif
            //only consider filters in archetype
            ArchetypeData a = this.metaArchetypeRegistry[metaArchetype];
            FilterCombination c = this.filterCombinationRegistry[combination];
            FilterCombination newCombination = new FilterCombination( c.filterComponentInstances.ToArray( ) );
            foreach ( RegistryIndex<IFilterComponent> filter in c.filterComponentInstances )
            {
                IFilterComponent f = this.filterRegistry[filter];
                RegistryIndex<Type> type = this.componentTypeRegistry.GetIndex( f.GetType( ) );
                if ( !a.filters.Contains( type ) )
                {
                    newCombination.filterComponentInstances.Remove( filter );
                }
            }

            RegistryIndex<FilterCombination> newCombinationIndex = this.GetFilterCombinationIndex( newCombination );

            //Add combination if not present already
            if ( !this.taxa[metaArchetype].ContainsKey( newCombinationIndex ) )
            {
                Taxon taxon = new Taxon( ( ushort )taxonCount );
                taxonCount++;
                this.taxa[metaArchetype].Add( newCombinationIndex, taxon );
            }

            //Return Taxon for Archetype+Combination
            return this.taxa[metaArchetype][newCombinationIndex];
        }

        private bool UpdateArchetype ( Entity entity, HashSet<RegistryIndex<Type>> types )
        {
            HashSet<RegistryIndex<ArchetypeData>> newArchetypesIndices = this.GetArchetypesWithTypes( types );

            if ( !( newArchetypesIndices.SetEquals( this.archetypeMap[entity.ID] ) ) )
            {
                this.archetypeMap[entity.ID].Clear( );
                this.archetypeMap[entity.ID] = new LinkedList<RegistryIndex<ArchetypeData>>( newArchetypesIndices );

                //find meta archetypes
                HashSet<RegistryIndex<ArchetypeData>> newMetaArchetypes = new HashSet<RegistryIndex<ArchetypeData>>( );
                ArchetypeData[] newArchetypes = new ArchetypeData[newArchetypesIndices.Count];
                int i = 0;
                foreach ( RegistryIndex<ArchetypeData> index in newArchetypesIndices )
                {
                    newArchetypes[i] = this.archetypeRegistry[index];
                    newMetaArchetypes.Add( this.metaArchetypeRegistry.GetIndex( newArchetypes[i] ) );
                    i++;
                }

                bool added;
                do
                {
                    added = false;
                    foreach ( ArchetypeData inputArchetype in newArchetypes )
                    {
                        foreach ( RegistryIndex<ArchetypeData> processedIndex in newMetaArchetypes )
                        {
                            ArchetypeData processedArchetype = this.metaArchetypeRegistry[processedIndex];
                            if ( inputArchetype.components.Overlaps( processedArchetype.components ) )
                            {
                                if ( inputArchetype.components.IsSubsetOf( processedArchetype.components ) && inputArchetype.filters.IsSubsetOf( processedArchetype.filters ) )
                                {
                                    break;
                                }
                                else if ( inputArchetype.components.IsSupersetOf( processedArchetype.components ) && inputArchetype.filters.IsSupersetOf( processedArchetype.filters ) )
                                {
                                    newMetaArchetypes.Remove( processedIndex );
                                    added = true;
                                    break;
                                }
                                else
                                {
                                    HashSet<RegistryIndex<Type>> union = new HashSet<RegistryIndex<Type>>( inputArchetype.components );
                                    union.UnionWith( processedArchetype.components );
                                    HashSet<RegistryIndex<Type>> filters = new HashSet<RegistryIndex<Type>>( inputArchetype.filters );
                                    filters.UnionWith( processedArchetype.filters );
                                    ArchetypeData unionArchetype = new ArchetypeData( union, filters );

                                    RegistryIndex<ArchetypeData> unionIndex;
                                    if ( !this.metaArchetypeRegistry.TryGetIndex( unionArchetype, out unionIndex ) )
                                    {
                                        unionIndex = this.RegisterMetaArchetype( unionArchetype );
                                    }
                                    newMetaArchetypes.Remove( processedIndex );
                                    newMetaArchetypes.Add( unionIndex );
                                    added = true;
                                    break;
                                }
                            }
                        }
                    }
                } while ( added == true );

                this.metaArchetypeMap[entity.ID].Clear( );
                this.metaArchetypeMap[entity.ID] = new LinkedList<RegistryIndex<ArchetypeData>>( newMetaArchetypes );
                return true;
            }
            else return false;

        }

        private void SetArchetypesForNewEntity ( Entity prototype, Entity newEntity, HashSet<RegistryIndex<Type>> componentTypes )
        {
            // Set Prototype's Archetype if not already set 
            if ( archetypeMap[prototype.ID].Count == 0 )
            {
                archetypeMap[prototype.ID] = new LinkedList<RegistryIndex<ArchetypeData>>( );
                metaArchetypeMap[prototype.ID] = new LinkedList<RegistryIndex<ArchetypeData>>( );
                this.UpdateArchetype( prototype, componentTypes );
            }
            // Set the new Entity's Archetype to the Prototype's Archetype
            archetypeMap[newEntity.ID] = new LinkedList<RegistryIndex<ArchetypeData>>( archetypeMap[prototype.ID] );
            metaArchetypeMap[newEntity.ID] = new LinkedList<RegistryIndex<ArchetypeData>>( metaArchetypeMap[prototype.ID] );
        }

        private void ExpandEntities ( )
        {
            this.componentRegistry.Add( new HashSet<RegistryIndex<Type>>( ) );
            this.filterCombinationMap.Add( this.defaultFilterCombination );
            this.archetypeMap.Add( new LinkedList<RegistryIndex<ArchetypeData>>( ) );
            this.metaArchetypeMap.Add( new LinkedList<RegistryIndex<ArchetypeData>>( ) );
        }
        private void UnregisterEntity ( Entity entity )
        {
            this.componentRegistry[entity.ID].Clear( );
            this.filterCombinationMap[entity.ID] = this.defaultFilterCombination;
            this.archetypeMap[entity.ID].Clear( );
            this.metaArchetypeMap[entity.ID].Clear( );
        }

        private Entity CreateEntity ( bool prototype )
        {
            if ( !this.initializedArchetypes ) throw new Exception( "Attempting to create Entity before Archetype initialization" );
            this.ProcessComponentChanges( );

            Entity entity;
            if ( recycleNext != 0 )
            {
                entity = new Entity( recycleNext, prototype );
                recycleNext = entities[recycleNext].ID;
                this.componentRegistry[entity.ID] = new HashSet<RegistryIndex<Type>>( );
            }
            else
            {
                if ( nextFree >= int.MaxValue - 1 )
                    throw new IndexOutOfRangeException( "Maximum number of entities reached: " + nextFree );
                entity = new Entity( nextFree, prototype );
                if ( entities.Length <= nextFree ) entities.ResizeUninitialized( nextFree + 1 );
                nextFree++;

                this.ExpandEntities( );
            }
            entities[entity.ID] = entity;

            return entity;
        }
        private HashSet<RegistryIndex<ArchetypeData>> GetArchetypesWithTypes ( HashSet<RegistryIndex<Type>> componentTypes )
        {
            //tentative archetypes list
            HashSet<RegistryIndex<ArchetypeData>> bests;

            if ( archetypeCache.TryGetValue( componentTypes, out bests ) )
            {
                return bests;
            }
            else
            {
                bests = new HashSet<RegistryIndex<ArchetypeData>>( );

                foreach ( RegistryIndex<ArchetypeData> index in this.archetypeRegistry.ByIndex )
                {
                    ArchetypeData archetype = this.archetypeRegistry[index];
                    if ( archetype.components.IsSubsetOf( componentTypes ) ) // taxon archetype is subset of Entity's components
                    {
                        bests.Add( index );
                    }
                }

                archetypeCache.Add( componentTypes, bests );
                return bests;
            }
        }
        private void ProcessComponentChanges ( )
        {
            foreach ( Entity entity in this.dirtyEntities )
            {
                HashSet<RegistryIndex<Type>> types = this.componentRegistry[entity.ID];

                bool filterTest = this.UpdateFilterCombination( entity );
                bool archetypeTest = this.UpdateArchetype( entity, types );
                RegistryIndex<FilterCombination> combination = this.filterCombinationMap[entity.ID];

                Taxon defaultTaxon = this.GetExactTaxon( defaultMetaArchetype, combination );
                foreach ( RegistryIndex<Type> t in types )
                {
                    bool inArchetype = false;
                    foreach ( RegistryIndex<ArchetypeData> metaArchetype in this.metaArchetypeMap[entity.ID] )
                    {
                        Taxon taxon = this.GetExactTaxon( metaArchetype, combination );
                        if ( this.metaArchetypeByType[t].Contains( metaArchetype ) ) //Component is in Archetype
                        {
                            this.componentPools[t.ID].UpdateTaxon( entity, taxon ); //Add to Filtered Taxon
                            inArchetype = true;
                            break;
                        }
                    }
                    if ( !inArchetype )
                    {
                        this.componentPools[t.ID].UpdateTaxon( entity, defaultTaxon ); //Add to Default Taxon
                    }
                }
            }

            this.dirtyEntities.Clear( );
        }
    }
}