using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using VodeoECS.Internal;

namespace VodeoECS
{    
    /// <summary>
    /// ECS Component Pool for managing Filter Components of a given type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FilterComponentPool<T> : IFilterComponentPool, IComponentPool, IEnumerable<T> where T : unmanaged, IFilterComponent<T>
    {
        /// <summary>
        /// The RegistryIndex of the Filter Component type associated with this pool.
        /// </summary>
        public RegistryIndex<Type> ComponentType { get; }
        /// <summary>
        /// The number of Components in this pool.
        /// </summary>
        public int Count { get { return count; } }

        private List<NativeList<int>> filterIDs;
        private List<NativeList<Entity>> entityMap;
        private NativeList<ComponentIndex> indexMap;
        private NativeList<Taxon> taxa;

        private NativeList<int> filterCounts;
        private NativeList<T> uniqueFilterByID;
        private NativeHashMap<T, int> uniqueFilters;

        private World world;
        private int capacity;
#if DEBUG
        private Entity instantiatedEntity;
#endif
        private ComponentIndex instantiatedIndex;
        private int count;
        private int prototypeCount;

        private bool creationEvents = false;
        private bool destructionEvents = false;
        private EventEmitter<ComponentCreationEvent<T>> creationEmitter;
        private EventEmitter<ComponentDestructionEvent<T>> destructionEmitter;

        /// <summary>
        /// For internal use by the World class. Component Pools are normally created by requesting them from the World class.
        /// </summary>
        /// <param name="world">The ECS World to register this Filter Component Pool with.</param>
        /// <param name="type">The type index for the Filter Component type associated with this pool.</param>
        /// <param name="capacity">The initial Component capacity of the Filter Component Pool.</param>
        /// <param name="maxID">The maximum entity ID.</param>
        public FilterComponentPool ( World world, RegistryIndex<Type> type, int capacity, int maxID )
        {
            this.world = world;
            this.capacity = capacity;
            this.ComponentType = type;
            this.indexMap = new NativeList<ComponentIndex>( maxID, Allocator.Persistent );
            this.count = 0;
            this.prototypeCount = 0;
            this.Initialize( );
        }

        /// <summary>
        /// For internal use by the World class. Prototypes are normally instantiated by calling the instantiating method on the World class.
        /// </summary>
        /// <param name="prototype">The Prototype Entity to instantiate.</param>
        /// <param name="newEntity">The newly instantiated Entity.</param>
        /// <param name="taxon">The Taxon to add the component to.</param>
        public void InstantiatePrototype ( Entity prototype, Entity newEntity, Taxon taxon )
        {
            ComponentIndex protoIndex = indexMap[prototype.ID];
            PrepareTaxon( taxon );
            int filterID = filterIDs[protoIndex.taxonID][protoIndex.entry];
            T filter = uniqueFilterByID[filterID];
            instantiatedIndex = this.CreateFilterComponent( filter, newEntity, taxon );
#if DEBUG
            instantiatedEntity = newEntity;
#endif
        }
        /// <summary>
        /// Adds a new Filter Component of this pool's type to the given Entity.
        /// </summary>
        /// <param name="entity">The Entity to add the new Filter Component to.</param>
        /// <param name="filter">The Filter Component to add to the pool.</param>
        /// <returns>The ComponentIndex of the added Filter Component.</returns>
        public ComponentIndex AddComponent ( Entity entity, T filter )
        {
            if ( HasComponent( entity ) )
                throw new ArgumentException( "Entity with ID " + entity.ID + " already has a component in this pool" );

            world.RegisterNewComponent( entity, this.ComponentType );

            return this.CreateFilterComponent( filter, entity, Taxon.Prototype );
        }
        /// <summary>
        /// Destroy the Component in this pool associated with the given Entity.
        /// </summary>
        /// <param name="entity">The Entity to remove the Component from.</param>
        public void DestroyComponent ( Entity entity )
        {
            if ( !HasComponent( entity ) )
                throw new ArgumentException( "Entity with ID " + entity.ID + " does not have a component in this pool" );

            this.RemoveComponent( entity );

            this.world.RegisterComponentRemoval( entity, this.ComponentType );
        }
        /// <summary>
        /// Set the last Filter Component instantiated from a prototype.
        /// </summary>
        /// <param name="value">The new value for the Filter Component.</param>
        public void SetInstantiatedFilter ( T value )
        {
#if DEBUG
            if ( world.instantiatedEntity != this.instantiatedEntity ) throw new Exception( "Last prototype instantiated does not have a " + typeof( T ) + " component." );
#endif
            this.SetFilter( instantiatedIndex, value );
        }
        /// <summary>
        /// Read the last Filter Component instantiated from a prototype.
        /// </summary>
        /// <returns>The value of the instantiated Filter Component.</returns>
        public T ReadInstantiatedFilter ( )
        {
#if DEBUG
            if ( world.instantiatedEntity != this.instantiatedEntity ) throw new Exception( "Last prototype instantiated does not have a " + typeof( T ) + " component." );
#endif
            return this[this.instantiatedIndex];
        }
        /// <summary>
        /// Does the given Entity have a Component in this pool?
        /// </summary>
        /// <param name="entity">The Entity to test.</param>
        /// <returns>Returns true if the Entity has a Component in this Pool, false if not.</returns>
        public bool HasComponent ( Entity entity )
        {
            if ( entity.ID >= indexMap.Length ) return false;
            return indexMap[entity.ID] != ComponentIndex.Null;
        }
        /// <summary>
        /// Get the Component Index for a given Entity. Components within an Archetype share the same ComponentIndex if their Entities implement the Archetype! 
        /// Component Indices should not be stored permanently as they are invalidated by Component removal, and Archetype or Filter changes. Accessing a Component through a Component Index is more efficient than through an Entity.
        /// </summary>
        /// <param name="entity">The ComponentIndex for this Entity will be returned.</param>
        /// <returns>The ComponentIndex requested.</returns>
        public ComponentIndex GetIndex ( Entity entity )
        {
            return this.indexMap[entity.ID];
        }

        /// <summary>
        /// Set the value of a Filter Component in this pool, by Index.
        /// This is more efficient than by Entity if the same ComponentIndex is used repeatedly.
        /// </summary>
        /// <param name="index">The Component Index of the Filter Component to set.</param>
        /// <param name="filter">The new value for the Filter Component.</param>
        public void SetFilter ( ComponentIndex index, T filter )
        {
            Entity entity = entityMap[index.taxonID][index.entry];
            this.RemoveComponent( entity );
            this.CreateFilterComponent( filter, entity, Taxon.Prototype );
            world.RegisterFilterChange( entity );
        }
        /// <summary>
        /// Set the value of a Filter Component in this pool, by Entity.
        /// </summary>
        /// <param name="entity">The Entity associated with the Filter Component to set.</param>
        /// <param name="filter">The new value for the Filter Component.</param>
        public void SetFilter ( Entity entity, T filter )
        {
            ComponentIndex index = this.indexMap[entity.ID];
            this.SetFilter( index, filter );
        }
        public IFilterComponent ReadBoxedFilterComponent ( Entity entity )
        {
            return this[entity];
        }


        /// <summary>
        /// Read the value of a Filter Component in this pool, by ComponentIndex.
        /// This is more efficient than by Entity if the same ComponentIndex is used repeatedly.
        /// </summary>
        /// <param name="index">The ComponentIndex of the Filter Component requested.</param>
        /// <returns>The value of the requested Filter Component.</returns>
        public T this[ComponentIndex index]
        {
            get
            {
                return this.uniqueFilterByID[this.filterIDs[index.taxonID][index.entry]];
            }
        }
        /// <summary>
        /// Read the value of a Filter Component in this pool, by Entity.
        /// </summary>
        /// <param name="entity">The Entity associated with the Filter Component requested.</param>
        /// <returns>The value of the requested Filter Component.</returns>
        public T this[Entity entity]
        {
            get
            {
                ComponentIndex index = this.indexMap[entity.ID];
                return this[index];
            }
        }
        /// <summary>
        /// Enumerates through all Entities with a Filter Component in this pool.
        /// </summary>
        public IEnumerable<Entity> Entities
        {
            get
            {
                foreach ( Taxon taxon in this.taxa )
                {
                    foreach ( Entity entity in entityMap[taxon.ID] )
                    {
                        yield return entity;
                    }
                }
            }
        }
        /// <summary>
        /// Enumerates through the unique Filter values of all Filter Components in this pool.
        /// </summary>
        public IEnumerable<T> UniqueValues
        {
            get
            {
                foreach ( T value in uniqueFilterByID )
                {
                    yield return value;
                }
            }
        }
        /// <summary>
        /// Enumerates through the values of all Filter Components in this pool.
        /// </summary>
        /// <returns>The value of each Filter Component in this pool.</returns>
        public IEnumerator<T> GetEnumerator ( )
        {
            foreach ( Taxon taxon in this.taxa )
            {
                foreach ( int id in filterIDs[taxon.ID] )
                {
                    yield return uniqueFilterByID[id];
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }
        /// <summary>
        /// Enumerates through all unique Filter Component values in this pool. Filter Component values will not be repeated.
        /// </summary>
        /// <returns>Each unique Filter Component value in this pool.</returns>
        public IEnumerable<T> UniqueFilters
        {
            get
            {
                foreach ( T value in this.uniqueFilterByID )
                {
                    yield return value;
                }
            }
        }

        /// <summary>
        /// Enumerate through the values of all Filter Components in this pool that match a Query.
        /// </summary>
        /// <param name="query">The provided Query to match.</param>
        /// <returns>An IEnumerable enumerating the values requested.</returns>
        public IEnumerable<T> MatchingValues ( Query query )
        {
            foreach ( Taxon taxon in query )
            {
                if ( this.filterIDs.Count > taxon.ID && this.filterIDs[taxon.ID].IsCreated )
                    foreach ( int id in filterIDs[taxon.ID] )
                    {
                        yield return uniqueFilterByID[id];
                    }
            }
        }
        /// <summary>
        /// Enumerate through all Entities that have a Filter Component in this pool and match a Query.
        /// </summary>
        /// <param name="query">The provided Query to match.</param>
        /// <returns>An IEnumerable enumerating the entities requested.</returns>        
        public IEnumerable<Entity> MatchingEntities ( Query query )
        {
            foreach ( Taxon taxon in query )
            {
                if ( this.filterIDs.Count > taxon.ID && this.filterIDs[taxon.ID].IsCreated )
                    foreach ( Entity entity in this.entityMap[taxon.ID] )
                    {
                        yield return entity;
                    }
            }
        }

        /// <summary>
        /// Get the Slice of all Entities with components in this pool, in a given Taxon.
        /// </summary>
        /// <param name="taxon">The Taxon requested.</param>
        /// <returns>A Taxon Slice of Entities.</returns>
        public EntityTaxonSlice GetEntitySlice ( Taxon taxon )
        {
            this.PrepareTaxon( taxon );
            return new EntityTaxonSlice( entityMap[taxon.ID].AsArray( ).Slice( ) );
        }
        /// <summary>
        /// Get the Slice of all Filter Component values in this pool, in a given Taxon.
        /// </summary>
        /// <param name="taxon">The Taxon requested.</param>
        /// <returns>A Taxon Slice of Filter Component values.</returns>
        public FilterTaxonSlice<T> GetFilterSlice ( Taxon taxon )
        {
            PrepareTaxon( taxon );
            return new FilterTaxonSlice<T>( this.uniqueFilterByID.AsArray( ).Slice( ), this.filterIDs[taxon.ID].AsArray( ).Slice( ) );
        }
        /// <summary>
        /// Get all Taxon Slices of Entities with components this pool, matching a given Query.  
        /// </summary>
        /// <param name="query">The query to match against.</param>
        /// <returns>An IEnumerable enumerating the Taxon Slices requested.</returns>
        public IEnumerable<EntityTaxonSlice> MatchingEntitySlices ( Query query )
        {
            foreach ( Taxon taxon in query ) { yield return this.GetEntitySlice( taxon ); }
        }
        /// <summary>
        /// Get all Taxon Slices of Filter Component values in this pool, matching a given Query.  
        /// </summary>
        /// <param name="query">The query to match against.</param>
        /// <returns>An IEnumerable enumerating the Taxon Slices requested.</returns>
        public IEnumerable<FilterTaxonSlice<T>> MatchingFilterSlices ( Query query )
        {
            foreach ( Taxon taxon in query ) { yield return this.GetFilterSlice( taxon ); }
        }

        /// <summary>
        /// For use with Burst Compiled jobs. 
        /// Creates a new Filter Pool Reader, which can be passed to a job and can read Filter Component Values; but not change, create, or destroy Filter Components. 
        /// Native memory is allocated for the Pool Accessor, so it will have to be Disposed.
        /// </summary>
        /// <param name="allocator">The memory Allocator to use.</param>
        /// <returns>A newly created Filter Pool Accessor. It will have to be Disposed manually.</returns>
        public FilterPoolReader<T> NewFilterPoolReader ( Allocator allocator )
        {
            return new FilterPoolReader<T>( this.filterIDs, this.uniqueFilterByID.AsArray( ).Slice( ), this.indexMap.AsArray( ).Slice( ), this.taxa.AsArray( ).Slice( ), allocator );
        }


        /// <summary>
        /// For internal use by the World class. Updates the Taxon of the component in this pool associated with a given Entity.
        /// </summary>
        /// <param name="entity">The Entity with the component to update.</param>
        /// <param name="newTaxon">The new Taxon.</param>
        public void UpdateTaxon ( Entity entity, Taxon newTaxon )
        {
            if ( !HasComponent( entity ) )
                throw new ArgumentException( "Entity with ID " + entity.ID + " does not have a component in this pool" );
            if ( entity.prototype ) throw new Exception( "Error: UpdateTaxon should not be called on prototype" );

            if ( this.indexMap[entity.ID].taxonID != newTaxon.ID )
            {
                int taxonID = newTaxon.ID;
                this.PrepareTaxon( newTaxon );

                ComponentIndex oldIndex = this.indexMap[entity.ID];
                int a = oldIndex.taxonID;
                int e = oldIndex.entry;

                int filterID = this.filterIDs[a][e];
                ComponentIndex newIndex = new ComponentIndex( this.filterIDs[taxonID].Length, taxonID );
                filterIDs[taxonID].Add( filterID );

                filterIDs[a].RemoveAtSwapBack( e );
                SetEntityIndex( entity, newIndex );

                entityMap[a].RemoveAtSwapBack( e );
                if ( entityMap[a].Length > 0 && entityMap[a].Length > e ) indexMap[entityMap[a][e].ID] = oldIndex;
            }
        }

        /// <summary>
        /// Enable the emission of ComponentCreationEvent<T> events upon creation of components in this pool, where T is the component type of the pool.
        /// </summary>
        public void EnableCreationEvents ( )
        {
            if ( !this.creationEvents )
            {
                this.creationEvents = true;
                this.creationEmitter = world.Events.GetEmitter<ComponentCreationEvent<T>>( null );
            }
        }

        /// <summary>
        /// Enable the emission of ComponentDestructionEvent<T> events upon destruction of components in this pool, where T is the component type of the pool.
        /// </summary>
        public void EnableDestructionEvents ( )
        {
            if ( !this.destructionEvents )
            {
                this.destructionEvents = true;
                this.destructionEmitter = world.Events.GetEmitter<ComponentDestructionEvent<T>>( null );
            }
        }

        /// <summary>
        /// For use by the ECS Serializer. Serializes this Pool.
        /// </summary>
        /// <returns>The serialized data for this pool.</returns>
        public SerializedPoolData SerializeToBytes ( )
        {
            int componentSize = Marshal.SizeOf( default( T ) );
            int entitySize = Marshal.SizeOf( default( Entity ) );
            int intSize = Marshal.SizeOf( default( int ) );
            byte[] componentBytes = new byte[( this.uniqueFilterByID.Length ) * componentSize];
            byte[] entityBytes = new byte[( this.count + this.prototypeCount ) * entitySize];
            byte[] filterIndexBytes = new byte[( this.count + this.prototypeCount ) * intSize];

            uniqueFilterByID.AsArray( ).Reinterpret<byte>( componentSize ).ToArray( ).CopyTo( componentBytes, 0 );

            int l = 0;
            int n = 0;
            foreach ( NativeList<Entity> list in entityMap )
            {
                if ( list.IsCreated )
                {
                    list.AsArray( ).Reinterpret<byte>( entitySize ).ToArray( ).CopyTo( entityBytes, l );
                    int length = list.Length;
                    l += length * entitySize;

                    foreach ( Entity entity in list )
                    {
                        ComponentIndex index = this.indexMap[entity.ID];
                        int filterID = this.filterIDs[index.taxonID][index.entry];
                        BitConverter.GetBytes( filterID ).CopyTo( filterIndexBytes, n );
                        n += intSize;
                    }
                }
            }

            return new SerializedPoolData( ) { filterIndices = filterIndexBytes, entities = entityBytes, components = componentBytes };
        }
        /// <summary>
        /// For use by the ECS Serialized. Deserializes pool data into this Pool.
        /// </summary>
        /// <param name="data">The serialized data to deserialize.</param>
        public void DeserializeFromBytes ( SerializedPoolData data )
        {
            this.ResetPool( );

            int componentSize = Marshal.SizeOf( default( T ) );
            int entitySize = Marshal.SizeOf( default( Entity ) );
            int intSize = Marshal.SizeOf( default( int ) );

            Entity[] entities = new Entity[data.entities.Length / entitySize];
            NativeArray<byte> entityBytes = new NativeArray<byte>( data.entities, Allocator.Temp );
            entityBytes.Reinterpret<Entity>( sizeof( byte ) ).CopyTo( entities );

            T[] components = new T[data.components.Length / componentSize];
            NativeArray<byte> componentBytes = new NativeArray<byte>( data.components, Allocator.Temp );
            componentBytes.Reinterpret<T>( sizeof( byte ) ).CopyTo( components );

            int[] indices = new int[data.filterIndices.Length / intSize];
            NativeArray<byte> indexBytes = new NativeArray<byte>( data.filterIndices, Allocator.Temp );
            indexBytes.Reinterpret<int>( sizeof( byte ) ).CopyTo( indices );

            for ( int i = 0; i < entities.Length; i++ )
            {
                this.AddComponent( entities[i], components[indices[i]] );
            }

            entityBytes.Dispose( );
            componentBytes.Dispose( );
            indexBytes.Dispose( );
        }

        /// <summary>
        /// Dispose the native memory reserved by this Pool.
        /// </summary>
        public void Dispose ( )
        {
            for ( int i = 0; i < 2; i++ )
            {
                filterIDs[i].Dispose( );
                entityMap[i].Dispose( );
            }
            for ( int i = 0; i < taxa.Length; i++ )
            {
                int id = taxa[i].ID;
                filterIDs[id].Dispose( );
                entityMap[id].Dispose( );
            }
            indexMap.Dispose( );
            uniqueFilters.Dispose( );
            filterCounts.Dispose( );
            uniqueFilterByID.Dispose( );
            taxa.Dispose( );
        }

        private void RemoveComponent ( Entity entity )
        {
            ComponentIndex indexOfDestroyed = indexMap[entity.ID];
            int a = indexOfDestroyed.taxonID;
            int e = indexOfDestroyed.entry;
            int l = entityMap[a].Length - 1;
            Entity lastEntity = entityMap[a][l];

            if ( this.destructionEvents )
            {
                this.destructionEmitter.CreateEvent( new ComponentDestructionEvent<T>( ) { entity = entity, component = this[indexOfDestroyed] } );
            }

            indexMap[lastEntity.ID] = indexOfDestroyed;
            indexMap[entity.ID] = ComponentIndex.Null;
            int filterIndex = filterIDs[a][e];
            DecrementFilter( filterIndex );
            entityMap[a].RemoveAtSwapBack( e );
            filterIDs[a].RemoveAtSwapBack( e );
            if ( !entity.prototype ) this.count--;
            else this.prototypeCount--;
        }

        private void Initialize ( )
        {
            this.uniqueFilters = new NativeHashMap<T, int>( capacity, Allocator.Persistent );
            this.uniqueFilterByID = new NativeList<T>( capacity, Allocator.Persistent );
            this.filterCounts = new NativeList<int>( capacity, Allocator.Persistent );

            T nullFilter = new T( );
            this.uniqueFilters.Add( nullFilter, 0 );
            this.uniqueFilterByID.Add( nullFilter );
            this.filterCounts.Add( 0 );

            taxa = new NativeList<Taxon>( capacity, Allocator.Persistent );
            filterIDs = new List<NativeList<int>>( 2 );
            entityMap = new List<NativeList<Entity>>( 2 );
            for ( int i = 0; i < 2; i++ )
            {
                filterIDs.Add( new NativeList<int>( capacity, Allocator.Persistent ) );
                entityMap.Add( new NativeList<Entity>( capacity, Allocator.Persistent ) );
            }
        }
        private void ResetPool ( )
        {
            int capacity = indexMap.Capacity;
            this.Dispose( );
            indexMap = new NativeList<ComponentIndex>( capacity, Allocator.Persistent );
            this.count = 0;
            this.prototypeCount = 0;
            this.Initialize( );
        }
        private void DecrementFilter ( int filterIndex )
        {
            int count = this.filterCounts[filterIndex];
            if ( count > 1 )
            {
                this.filterCounts[filterIndex] = this.filterCounts[filterIndex] - 1;
            }
        }
        private int GetNewFilterIndex ( T filter )
        {
            int filterIndex;
            if ( this.uniqueFilters.ContainsKey( filter ) )
            {
                filterIndex = uniqueFilters[filter];
                int count = this.filterCounts[filterIndex];
                this.filterCounts[filterIndex] = count + 1;
            }
            else
            {
                filterIndex = uniqueFilterByID.Length;
                uniqueFilters.Add( filter, filterIndex );
                this.uniqueFilterByID.Add( filter );
                this.filterCounts.Add( 1 );
            }
            return filterIndex;
        }
        private ComponentIndex CreateFilterComponent ( T filter, Entity entity, Taxon taxon )
        {
            if ( creationEvents && !entity.prototype )
            {
                this.creationEmitter.CreateEvent( new ComponentCreationEvent<T>( ) { entity = entity } );
            }

            int filterID = GetNewFilterIndex( filter );

            PrepareTaxon( taxon );

            ComponentIndex index = new ComponentIndex( this.entityMap[taxon.ID].Length, taxon.ID );
            SetEntityIndex( entity, index );
            filterIDs[taxon.ID].Add( filterID );
            if ( !entity.prototype ) this.count++;
            else this.prototypeCount++;
            return index;
        }
        private void SetEntityIndex ( Entity entity, ComponentIndex index )
        {
            if ( indexMap.Length <= entity.ID )
            {
                indexMap.Resize( 1 + entity.ID, NativeArrayOptions.ClearMemory );
            }
            if ( entityMap[index.taxonID].Length <= index.entry )
            {
                entityMap[index.taxonID].Resize( 1 + index.entry, NativeArrayOptions.ClearMemory );
            }
            indexMap[entity.ID] = index;
            NativeSlice<Entity> list = entityMap[index.taxonID].AsArray( ).Slice( );
            list[index.entry] = entity;
        }
        private void PrepareTaxon ( Taxon taxon )
        {
            if ( taxon.ID >= entityMap.Count )
            {
                if ( taxon.ID >= filterIDs.Capacity ) filterIDs.Capacity = taxon.ID + 1;
                if ( taxon.ID >= entityMap.Capacity ) entityMap.Capacity = taxon.ID + 1;

                int n = entityMap.Count;
                for ( int i = 0; i <= taxon.ID - n; i++ )
                {
                    entityMap.Add( new NativeList<Entity>( ) );
                    filterIDs.Add( new NativeList<int>( ) );
                }
            }
            if ( !this.entityMap[taxon.ID].IsCreated )
            {
                this.taxa.Add( taxon );
                this.filterIDs[taxon.ID] = new NativeList<int>( capacity, Allocator.Persistent );
                this.entityMap[taxon.ID] = new NativeList<Entity>( capacity, Allocator.Persistent );
            }
        }
    }
}