using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using VodeoECS.Internal;

namespace VodeoECS
{
    /// <summary>
    /// ECS Component Pool for managing Data Components of a given type.
    /// </summary>
    /// <typeparam name="T">The Data Component type associated with this pool.</typeparam>
    public class DataComponentPool<T> : IComponentPool, IEnumerable<DataAccessor<T>> where T : unmanaged, IDataComponent
    {
        /// <summary>
        /// The RegistryIndex of the Data Component type associated with this pool.
        /// </summary>
        public RegistryIndex<Type> ComponentType { get; }
        /// <summary>
        /// The number of Components in this pool.
        /// </summary>
        public int Count { get { return count; } }

        private List<NativeList<T>> components;
        private List<NativeList<Entity>> entityMap;
        private NativeList<ComponentIndex> indexMap;
        private NativeList<Taxon> taxa;

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
        /// <param name="world">The ECS World to register this Data Component Pool with.</param>
        /// <param name="type">The type index for the Data Component type associated with this pool.</param>
        /// <param name="capacity">The initial Component capacity of the Data Component Pool.</param>
        /// <param name="maxID">The maximum entity ID.</param>
        public DataComponentPool ( World world, RegistryIndex<Type> type, int capacity, int maxID )
        {
            this.capacity = capacity;
            this.world = world;
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
            ComponentIndex protoIndex = this.indexMap[prototype.ID];
            PrepareTaxon( taxon );
            this.instantiatedIndex = this.CreateDataComponent( this.components[protoIndex.taxonID][protoIndex.entry], newEntity, taxon );
#if DEBUG
            this.instantiatedEntity = newEntity;
#endif
        }
        /// <summary>
        /// Adds a new Data Component of this pool's type to the given Entity.
        /// </summary>
        /// <param name="entity">The Entity to add the new Data Component to.</param>
        /// <param name="component">The Data Component to add to the pool.</param>
        /// <returns>The ComponentIndex of the added Data Component.</returns>
        public ComponentIndex AddComponent ( Entity entity, T component )
        {
            if ( HasComponent( entity ) )
                throw new ArgumentException( "Entity with ID " + entity.ID + " already has a component in this pool" );

            world.RegisterNewComponent( entity, this.ComponentType );

            return this.CreateDataComponent( component, entity, Taxon.Prototype );
        }
        /// <summary>
        /// Destroy the Component in this pool associated with the given Entity.
        /// </summary>
        /// <param name="entity">The Entity to remove the Component from.</param>
        public void DestroyComponent ( Entity entity )
        {
            if ( !HasComponent( entity ) )
                throw new ArgumentException( "Entity with ID " + entity.ID + " does not have a component in this pool" );

            ComponentIndex indexOfDestroyed = indexMap[entity.ID];
            int a = indexOfDestroyed.taxonID;
            int e = indexOfDestroyed.entry;
            int l = components[a].Length - 1;
            Entity lastEntity = entityMap[a][l];

            if ( this.destructionEvents )
            {
                this.destructionEmitter.CreateEvent( new ComponentDestructionEvent<T>( ) { entity = entity, component = this[indexOfDestroyed].Value } );
            }

            indexMap[lastEntity.ID] = indexOfDestroyed;
            indexMap[entity.ID] = ComponentIndex.Null;
            entityMap[a].RemoveAtSwapBack( e );
            components[a].RemoveAtSwapBack( e );
            if ( !entity.prototype ) this.count--;
            else this.prototypeCount--;

            this.world.RegisterComponentRemoval( entity, this.ComponentType );
        }
        /// <summary>
        /// Write to the last Data Component instantiated from a prototype.
        /// </summary>
        /// <param name="value">The value to write to the Data Component.</param>
        public void WriteInstantiated ( T value )
        {
#if DEBUG
            if ( world.instantiatedEntity != this.instantiatedEntity ) throw new Exception( "Last prototype instantiated does not have a " + typeof( T ) + " component." );
#endif
            DataAccessor<T> a = this[instantiatedIndex];
            a.Write( value );
        }
        /// <summary>
        /// Read the last Data Component instantiated from a prototype.
        /// </summary>
        /// <returns>The value of the instantiated Data Component.</returns>
        public T ReadInstantiated ()
        {
#if DEBUG
            if ( world.instantiatedEntity != this.instantiatedEntity ) throw new Exception( "Last prototype instantiated does not have a " + typeof( T ) + " component." );
#endif
            return this[instantiatedIndex].Value;
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
        /// Get access to a Data Component in this pool, by ComponentIndex.
        /// This is more efficient than by Entity if the same ComponentIndex is used repeatedly.
        /// </summary>
        /// <param name="index">The ComponentIndex of the Data Component requested.</param>
        /// <returns>An accessor for the requested Data Component.</returns>
        public DataAccessor<T> this[ComponentIndex index]
        {
            get
            {
                return this[index.taxonID, index.entry];
            }
        }
        /// <summary>
        /// Get access to a Data Component in this pool, by Entity.
        /// </summary>
        /// <param name="entity">The Entity associated with the Data Component requested.</param>
        /// <returns>An accessor for the requested Data Component.</returns>
        public DataAccessor<T> this[Entity entity]
        {
            get
            {
                ComponentIndex index = this.indexMap[entity.ID];
                return this[index];
            }
        }
        /// <summary>
        /// Enumerates through all Entities with a Data Component in this pool.
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
        /// Enumerates through the values of all Data Components in this pool.
        /// </summary>
        public IEnumerable<T> Values
        {
            get
            {
                foreach ( Taxon taxon in this.taxa )
                {
                    foreach ( T value in components[taxon.ID] )
                    {
                        yield return value;
                    }
                }
            }
        }
        /// <summary>
        /// Enumerates through access to all components in this pool.
        /// </summary>
        /// <returns>An accessor to each component in this pool.</returns>
        public IEnumerator<DataAccessor<T>> GetEnumerator ( )
        {
            foreach ( Taxon taxon in this.taxa )
            {
                for ( int i = 0; i < this.components[taxon.ID].Length; i++ )
                {
                    yield return this[taxon.ID, i];
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }

        /// <summary>
        /// Enumerate through the values of all Data Components in this pool that match a Query.
        /// </summary>
        /// <param name="query">The provided Query to match.</param>
        /// <returns>An IEnumerable enumerating the values requested.</returns>
        public IEnumerable<T> MatchingValues ( Query query )
        {
            foreach ( Taxon taxon in query )
            {
                if ( this.components.Count > taxon.ID && this.components[taxon.ID].IsCreated )
                    foreach ( T value in this.components[taxon.ID] )
                    {
                        yield return value;
                    }
            }
        }
        /// <summary>
        /// Enumerate through all Entities that have a Data Component in this pool and match a Query.
        /// </summary>
        /// <param name="query">The provided Query to match.</param>
        /// <returns>An IEnumerable enumerating the entities requested.</returns>        
        public IEnumerable<Entity> MatchingEntities ( Query query )
        {
            foreach ( Taxon taxon in query )
            {
                if ( this.components.Count > taxon.ID && this.components[taxon.ID].IsCreated )
                    foreach ( Entity entity in this.entityMap[taxon.ID] )
                    {
                        yield return entity;
                    }
            }
        }
        /// <summary>
        /// Enumerate access through all Data Components in this pool that match a Query.
        /// </summary>
        /// <param name="query">The provided Query to match.</param>
        /// <returns>An IEnumerable enumerating accessors to the Data Components requested.</returns>        
        public IEnumerable<DataAccessor<T>> Matching ( Query query )
        {
            foreach ( Taxon taxon in query )
            {
                if ( this.components.Count > taxon.ID && this.components[taxon.ID].IsCreated )
                    for ( int i = 0; i < this.components[taxon.ID].Length; i++ )
                    {
                        yield return this[taxon.ID, i];
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
        /// Get the Slice of all Data Components in this pool, in a given Taxon.
        /// </summary>
        /// <param name="taxon">The Taxon requested.</param>
        /// <returns>A Taxon Slice of Data Components.</returns>
        public DataTaxonSlice<T> GetDataSlice ( Taxon taxon )
        {
            this.PrepareTaxon( taxon );
            return new DataTaxonSlice<T>( components[taxon.ID].AsArray( ).Slice( ) );
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
        /// Get all Taxon Slices of Data Components in this pool, matching a given Query.  
        /// </summary>
        /// <param name="query">The query to match against.</param>
        /// <returns>An IEnumerable enumerating the Taxon Slices requested.</returns>
        public IEnumerable<DataTaxonSlice<T>> MatchingDataSlices ( Query query )
        {
            foreach ( Taxon taxon in query ) { yield return this.GetDataSlice( taxon ); }
        }

        /// <summary>
        /// For use with Burst Compiled jobs. 
        /// Creates a new Data Pool Accessor, which can be passed to a job and can read Data Components and write to them, but not destroy them or create new ones. 
        /// Native memory is allocated for the Pool Accessor, so it will have to be Disposed.
        /// </summary>
        /// <param name="allocator">The memory Allocator to use.</param>
        /// <returns>A newly created Data Pool Accessor. It will have to be Disposed manually.</returns>
        public DataPoolAccessor<T> NewDataPoolAccessor ( Allocator allocator )
        {
            return new DataPoolAccessor<T>( this.components, this.indexMap.AsArray( ).Slice( ), this.taxa.AsArray( ).Slice( ), allocator );
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

                T component = components[a][e];
                ComponentIndex newIndex = new ComponentIndex( this.components[taxonID].Length, taxonID );
                components[taxonID].Add( component );

                components[a].RemoveAtSwapBack( e );
                SetEntityIndex( entity, newIndex );

                entityMap[a].RemoveAtSwapBack( e );
                if ( entityMap[a].Length > 0 && entityMap[a].Length > e ) indexMap[entityMap[a][e].ID] = oldIndex;
            }
        }

        /// <summary>
        /// Enable the emission of ComponentCreationEvent<T> events upon creation of components in this pool, where T is the component type of the pool.
        /// </summary>
        public void EnableCreationEvents()
        {
            if (!this.creationEvents)
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
            byte[] componentBytes = new byte[( this.count + this.prototypeCount ) * componentSize];
            byte[] entityBytes = new byte[( this.count + this.prototypeCount ) * entitySize];
            int l = 0;
            foreach ( NativeList<T> list in components )
            {
                if ( list.IsCreated )
                {
                    list.AsArray( ).Reinterpret<byte>( componentSize ).ToArray( ).CopyTo( componentBytes, l );
                    l += list.Length * componentSize;
                }
            }

            l = 0;
            foreach ( NativeList<Entity> list in entityMap )
            {
                if ( list.IsCreated )
                {
                    list.AsArray( ).Reinterpret<byte>( entitySize ).ToArray( ).CopyTo( entityBytes, l );
                    int length = list.Length;
                    l += length * entitySize;
                }
            }
            return new SerializedPoolData( ) { entities = entityBytes, components = componentBytes };
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

            Entity[] entities = new Entity[data.entities.Length / entitySize];
            NativeArray<byte> entityBytes = new NativeArray<byte>( data.entities, Allocator.Temp );
            entityBytes.Reinterpret<Entity>( sizeof( byte ) ).CopyTo( entities );

            T[] components = new T[data.components.Length / componentSize];
            NativeArray<byte> componentBytes = new NativeArray<byte>( data.components, Allocator.Temp );
            componentBytes.Reinterpret<T>( sizeof( byte ) ).CopyTo( components );

            for ( int i = 0; i < entities.Length; i++ )
            {
                world.RegisterNewComponent( entities[i], this.ComponentType );
                this.CreateDataComponent( components[i], entities[i], Taxon.Prototype );
            }

            entityBytes.Dispose( );
            componentBytes.Dispose( );
        }

        /// <summary>
        /// Dispose the native memory reserved by this Pool.
        /// </summary>
        public void Dispose ( )
        {
            for ( int i = 0; i < 2; i++ )
            {
                components[i].Dispose( );
                entityMap[i].Dispose( );
            }
            for ( int i = 0; i < taxa.Length; i++ )
            {
                int id = taxa[i].ID;
                components[id].Dispose( );
                entityMap[id].Dispose( );
            }
            indexMap.Dispose( );
            taxa.Dispose( );
        }

        private void Initialize ( )
        {
            taxa = new NativeList<Taxon>( capacity, Allocator.Persistent );
            components = new List<NativeList<T>>( 2 );
            entityMap = new List<NativeList<Entity>>( 2 );
            for ( int i = 0; i < 2; i++ )
            {
                components.Add( new NativeList<T>( capacity, Allocator.Persistent ) );
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
        private DataAccessor<T> this[int t, int i]
        {
            get
            {
                return new DataAccessor<T>( components[t].AsArray( ).Slice( i, 1 ) );
            }
        }
        private ComponentIndex CreateDataComponent ( T component, Entity entity, Taxon taxon )
        {
            if ( creationEvents && !entity.prototype )
            {
                this.creationEmitter.CreateEvent( new ComponentCreationEvent<T>( ) { entity = entity } );
            }

            ComponentIndex index = new ComponentIndex( this.components[taxon.ID].Length, taxon.ID );
            SetEntityIndex( entity, index );
            components[taxon.ID].Add( component );
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
                if ( taxon.ID >= components.Capacity ) components.Capacity = taxon.ID + 1;
                if ( taxon.ID >= entityMap.Capacity ) entityMap.Capacity = taxon.ID + 1;

                int n = entityMap.Count;
                for ( int i = 0; i <= taxon.ID - n; i++ )
                {
                    entityMap.Add( new NativeList<Entity>( ) );
                    components.Add( new NativeList<T>( ) );
                }
            }
            if ( !this.entityMap[taxon.ID].IsCreated )
            {
                this.taxa.Add( taxon );
                this.components[taxon.ID] = new NativeList<T>( capacity, Allocator.Persistent );
                this.entityMap[taxon.ID] = new NativeList<Entity>( capacity, Allocator.Persistent );
            }
        }
    }
}