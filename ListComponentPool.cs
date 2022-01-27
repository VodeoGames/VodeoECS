using VodeoECS.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace VodeoECS
{
    /// <summary>
    /// ECS Component Pool for managing List Components of a given Element type.
    /// </summary>
    /// <typeparam name="T">The List Component Element type associated with this pool.</typeparam>
    public class ListComponentPool<T> : IComponentPool, IEnumerable<ListAccessor<T>> where T : unmanaged, IElementComponent
    {
        /// <summary>
        /// The RegistryIndex of the List Component Element type associated with this pool.
        /// </summary>
        public RegistryIndex<Type> ComponentType { get; }
        /// <summary>
        /// The number of Components in this pool.
        /// </summary>
        public int Count { get { return count; } }

        private List<NativeNested<T>> components;
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
        /// <param name="world">The ECS World to register this List Component Pool with.</param>
        /// <param name="type">The type index for the List Component Element type associated with this pool.</param>
        /// <param name="capacity">The initial Component capacity of the List Component Pool (number of list components).</param>
        /// <param name="maxID">The maximum entity ID.</param>
        public ListComponentPool ( World world, RegistryIndex<Type> type, int capacity, int maxID )
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
            ComponentIndex protoIndex = this.indexMap[prototype.ID];
            PrepareTaxon( taxon );

            ListTaxonSlice<T> prototypeSlice = this.GetListSlice( Taxon.Prototype );
            ListTaxonSlice<T> instanceSlice = this.GetListSlice( taxon );
            int l = prototypeSlice[protoIndex.entry].Length;

            this.instantiatedIndex = this.CreateListComponent( newEntity, taxon, l );
            for ( int i = 0; i < l; i++ )
            {
                T element = prototypeSlice[protoIndex.entry][i];
                instanceSlice[instantiatedIndex.entry].AppendElement( element );
            }
#if DEBUG
            this.instantiatedEntity = newEntity;
#endif
        }
        /// <summary>
        /// Adds a new List Component of this pool's Element type to the given Entity.
        /// </summary>
        /// <param name="entity">The Entity to add the new List Component to.</param>
        /// <param name="initialCapacity">The initial Element capacity for the List Component (number of elements).</param>
        /// <returns>The ComponentIndex of the added List Component.</returns>
        public ComponentIndex AddComponent ( Entity entity, int initialCapacity = 0 )
        {
            if ( HasComponent( entity ) )
                throw new ArgumentException( "Entity with ID " + entity.ID + " already has a component in this pool" );

            world.RegisterNewComponent( entity, this.ComponentType );

            return this.CreateListComponent( entity, Taxon.Prototype, initialCapacity );
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
                this.destructionEmitter.CreateEvent( new ComponentDestructionEvent<T>( ) { entity = entity } );
            }

            indexMap[lastEntity.ID] = indexOfDestroyed;
            indexMap[entity.ID] = ComponentIndex.Null;
            entityMap[a].RemoveAtSwapBack( e );
            components[a].DestroyAtSwapBack( indexOfDestroyed.entry );
            if ( !entity.prototype ) this.count--;
            else this.prototypeCount--;

            this.world.RegisterComponentRemoval( entity, this.ComponentType );
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
        /// Get access to the last instantiated List Component List in this pool.
        /// </summary>
        /// <returns>An accessor to the requested List Component.</returns>
        public ListAccessor<T> GetInstantiatedList ( )
        {
#if DEBUG
            if ( world.instantiatedEntity != this.instantiatedEntity ) throw new Exception( "Last prototype instantiated does not have a " + typeof( T ) + " component." );
#endif
            return this[instantiatedIndex];
        }
        /// <summary>
        /// Append a new Element to the last instantiated List Component List in this pool.
        /// </summary>
        /// <param name="element">The new Element to append.</param>
        public void AppendElementToInstantiatedList ( T element )
        {
#if DEBUG
            if ( world.instantiatedEntity != this.instantiatedEntity ) throw new Exception( "Last prototype instantiated does not have a " + typeof( T ) + " component." );
#endif
            this[instantiatedIndex].AppendElement( element );
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
        /// Get access to a List Component in this pool, by ComponentIndex.
        /// This is more efficient than by Entity if the same ComponentIndex is used repeatedly.
        /// </summary>
        /// <param name="index">The ComponentIndex of the List Component requested.</param>
        /// <returns>An accessor for the requested List Component.</returns>
        public ListAccessor<T> this[ComponentIndex index]
        {
            get
            {
                return this[index.taxonID, index.entry];
            }
        }
        /// <summary>
        /// Get access to a List Component in this pool, by Entity.
        /// </summary>
        /// <param name="entity">The Entity associated with the List Component requested.</param>
        /// <returns>An accessor for the requested List Component.</returns>
        public ListAccessor<T> this[Entity entity]
        {
            get
            {
                ComponentIndex index = this.indexMap[entity.ID];
                return this[index];
            }
        }
        /// <summary>
        /// Enumerates through all Entities with a List Component in this pool.
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
        /// Enumerates through access to all components in this pool.
        /// </summary>
        /// <returns>An accessor to each component in this pool.</returns>
        public IEnumerator<ListAccessor<T>> GetEnumerator ( )
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
        /// Enumerate through all Entities that have a List Component in this pool and match a Query.
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
        /// Enumerate access through all List Components in this pool that match a Query.
        /// </summary>
        /// <param name="query">The provided Query to match.</param>
        /// <returns>An IEnumerable enumerating accessors to the List Components requested.</returns>        
        public IEnumerable<ListAccessor<T>> Matching ( Query query )
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
        /// Get the Slice of all List Components in this pool, in a given Taxon.
        /// </summary>
        /// <param name="taxon">The Taxon requested.</param>
        /// <returns>A Taxon Slice of List Components.</returns>
        public ListTaxonSlice<T> GetListSlice ( Taxon taxon )
        {
            this.PrepareTaxon( taxon );
            return new ListTaxonSlice<T>( this.components[taxon.ID] );
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
        /// Get all Taxon Slices of List Components in this pool, matching a given Query.  
        /// </summary>
        /// <param name="query">The query to match against.</param>
        /// <returns>An IEnumerable enumerating the Taxon Slices requested.</returns>
        public IEnumerable<ListTaxonSlice<T>> MatchingListSlices ( Query query )
        {
            foreach ( Taxon taxon in query ) { yield return this.GetListSlice( taxon ); }
        }

        /// <summary>
        /// For use with Burst Compiled jobs. 
        /// Creates a new List Pool Accessor, which can be passed to a job and can read and write the Elements of List Components, destroy or append them, but not destroy or create new List Components. 
        /// Native memory is allocated for the Pool Accessor, so it will have to be Disposed.
        /// </summary>
        /// <param name="allocator">The memory Allocator to use.</param>
        /// <returns>A newly created List Pool Accessor. It will have to be Disposed manually.</returns>
        public ListPoolAccessor<T> NewListPoolAccessor ( Allocator allocator )
        {
            return new ListPoolAccessor<T>( this.components, indexMap.AsArray( ).Slice( ), this.taxa.AsArray( ).Slice( ), allocator );
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

                NativeNested<T> nested = components[taxonID];
                ComponentIndex newIndex = new ComponentIndex( this.components[taxonID].Length, taxonID );

                components[a].MoveNested( e, ref nested );
                components[taxonID] = nested;
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
            int elementSize = Marshal.SizeOf( default( T ) );
            int entitySize = Marshal.SizeOf( default( Entity ) );
            byte[] elementCountBytes = new byte[( this.count + this.prototypeCount ) * sizeof( int )];
            byte[] entityBytes = new byte[( this.count + this.prototypeCount ) * entitySize];

            int e = 0;
            foreach ( NativeNested<T> list in components )
            {
                if ( list.IsCreated )
                {
                    foreach ( NativeSlice<T> slice in list )
                    {
                        e += slice.Length;
                    }
                }
            }
            byte[] componentBytes = new byte[e * elementSize];

            int l = 0;
            int n = 0;

            foreach ( NativeNested<T> list in components )
            {
                if ( list.IsCreated )
                {
                    foreach ( NativeSlice<T> slice in list )
                    {
                        slice.SliceConvert<byte>( ).ToArray( ).CopyTo( componentBytes, l );
                        l += slice.Length * elementSize;

                        BitConverter.GetBytes( slice.Length ).CopyTo( elementCountBytes, n );
                        n += sizeof( int );
                    }
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
            return new SerializedPoolData( ) { elementCounts = elementCountBytes, entities = entityBytes, components = componentBytes };
        }
        /// <summary>
        /// For use by the ECS Serialized. Deserializes pool data into this Pool.
        /// </summary>
        /// <param name="data">The serialized data to deserialize.</param>
        public void DeserializeFromBytes ( SerializedPoolData data )
        {
            this.ResetPool( );

            int elementSize = Marshal.SizeOf( default( T ) );
            int entitySize = Marshal.SizeOf( default( Entity ) );

            Entity[] entities = new Entity[data.entities.Length / entitySize];
            NativeArray<byte> entityBytes = new NativeArray<byte>( data.entities, Allocator.Temp );
            entityBytes.Reinterpret<Entity>( sizeof( byte ) ).CopyTo( entities );

            T[] elements = new T[data.components.Length / elementSize];
            NativeArray<byte> elementBytes = new NativeArray<byte>( data.components, Allocator.Temp );
            elementBytes.Reinterpret<T>( sizeof( byte ) ).CopyTo( elements );

            int[] counts = new int[data.elementCounts.Length / sizeof( int )];
            NativeArray<byte> countBytes = new NativeArray<byte>( data.elementCounts, Allocator.Temp );
            countBytes.Reinterpret<int>( sizeof( byte ) ).CopyTo( counts );

            int n = 0;
            for ( int i = 0; i < entities.Length; i++ )
            {
                world.RegisterNewComponent( entities[i], this.ComponentType );

                ComponentIndex index = this.CreateListComponent( entities[i], Taxon.Prototype, counts[i] * elementSize );

                for ( int j = 0; j < counts[i]; j++ )
                {
                    this[index].AppendElement( elements[n + j] );
                }

                n += counts[i];
            }

            entityBytes.Dispose( );
            elementBytes.Dispose( );
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
            components = new List<NativeNested<T>>( 2 );
            entityMap = new List<NativeList<Entity>>( 2 );
            for ( int i = 0; i < 2; i++ )
            {
                components.Add( new NativeNested<T>( capacity, Allocator.Persistent ) );
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
        private ListAccessor<T> this[int t, int i]
        {
            get
            {
                return new ListAccessor<T>( this.components[t].GetNestedSlice( ), i );
            }
        }
        private ComponentIndex CreateListComponent ( Entity entity, Taxon taxon, int initialCapacity = 0 )
        {
            if ( HasComponent( entity ) )
                throw new ArgumentException( "Entity with ID " + entity.ID + " already has a component in this pool" );

            if ( creationEvents && !entity.prototype )
            {
                this.creationEmitter.CreateEvent( new ComponentCreationEvent<T>( ) { entity = entity } );
            }

            ComponentIndex index = new ComponentIndex( this.entityMap[taxon.ID].Length, taxon.ID );
            SetEntityIndex( entity, index );

            NativeNested<T> nested = components[taxon.ID];
            nested.CreateNestedList( initialCapacity );
            components[taxon.ID] = nested;

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
                    components.Add( new NativeNested<T>( ) );
                }
            }
            if ( !this.entityMap[taxon.ID].IsCreated )
            {
                this.taxa.Add( taxon );
                this.components[taxon.ID] = new NativeNested<T>( capacity, Allocator.Persistent );
                this.entityMap[taxon.ID] = new NativeList<Entity>( capacity, Allocator.Persistent );
            }
        }
    }
}