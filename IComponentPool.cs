using System;
using System.Collections.Generic;
using VodeoECS.Internal;

namespace VodeoECS
{
    /// <summary>
    /// For internal use by ECS framework. All Component Pools implement this interface.
    /// </summary>
    public interface IComponentPool
    {
        /// <summary>
        /// The RegistryIndex of the Data Component type associated with this pool.
        /// </summary>
        public RegistryIndex<Type> ComponentType { get; }
        /// <summary>
        /// Enumerate through all Entities that have a Component in this pool.
        /// </summary>
        public IEnumerable<Entity> Entities { get; }
        /// <summary>
        /// Enumerate through all Entities that have a Component in this pool and match a Query.
        /// </summary>
        /// <param name="query">The provided Query to match.</param>
        /// <returns>An IEnumerable enumerating the entities requested.</returns>        
        public IEnumerable<Entity> MatchingEntities ( Query query );
        /// <summary>
        /// Get the Slice of all Entities with components in this pool, in a given Taxon.
        /// </summary>
        /// <param name="taxon">The Taxon requested.</param>
        /// <returns>A Taxon Slice of Entities.</returns>
        public EntityTaxonSlice GetEntitySlice ( Taxon taxon );
        /// <summary>
        /// Get all Taxon Slices of Entities with components this pool, matching a given Query.  
        /// </summary>
        /// <param name="query">The query to match against.</param>
        /// <returns>An IEnumerable enumerating the Taxon Slices requested.</returns>
        public IEnumerable<EntityTaxonSlice> MatchingEntitySlices ( Query query );
        /// <summary>
        /// Get the Component Index for a given Entity. Components within an Archetype share the same ComponentIndex if their Entities implement the Archetype! 
        /// Component Indices should not be stored permanently as they are invalidated by Component removal, and Archetype or Filter changes. Accessing a Component through a Component Index is more efficient than through an Entity.
        /// </summary>
        /// <param name="entity">The ComponentIndex for this Entity will be returned.</param>
        /// <returns>The ComponentIndex requested.</returns>
        public ComponentIndex GetIndex ( Entity entity );
        /// <summary>
        /// For internal use by the World class. Prototypes are normally instantiated by calling the instantiating method on the World class.
        /// </summary>
        /// <param name="prototype">The Prototype Entity to instantiate.</param>
        /// <param name="newEntity">The newly instantiated Entity.</param>
        /// <param name="taxon">The Taxon to add the component to.</param>
        public void InstantiatePrototype ( Entity prototype, Entity newEntity, Taxon taxon );
        /// <summary>
        /// Does the given Entity have a Component in this pool?
        /// </summary>
        /// <param name="entity">The Entity to test.</param>
        /// <returns>Returns true if the Entity has a Component in this Pool, false if not.</returns>
        public bool HasComponent ( Entity entity );
        /// <summary>
        /// Destroy the Component in this pool associated with the given Entity.
        /// </summary>
        /// <param name="entity">The Entity to remove the Component from.</param>
        public void DestroyComponent ( Entity entity );
        /// <summary>
        /// For internal use by the World class. Updates the Taxon of the component in this pool associated with a given Entity.
        /// </summary>
        /// <param name="entity">The Entity with the component to update.</param>
        /// <param name="newTaxon">The new Taxon.</param>
        public void UpdateTaxon ( Entity entity, Taxon newTaxon );
        /// <summary>
        /// Enable the emission of ComponentCreationEvent<T> events upon creation of components in this pool, where T is the component type of the pool.
        /// </summary>
        public void EnableCreationEvents ( );
        /// <summary>
        /// Enable the emission of ComponentDestructionEvent<T> events upon destruction of components in this pool, where T is the component type of the pool.
        /// </summary>
        public void EnableDestructionEvents ( );
        /// <summary>
        /// For use by the ECS Serializer. Serializes this Pool.
        /// </summary>
        /// <returns>The serialized data for this pool.</returns>
        public SerializedPoolData SerializeToBytes ( );
        /// <summary>
        /// For use by the ECS Serialized. Deserializes pool data into this Pool.
        /// </summary>
        /// <param name="data">The serialized data to deserialize.</param>
        public void DeserializeFromBytes ( SerializedPoolData data );
        /// <summary>
        /// Dispose the native memory reserved by this Pool.
        /// </summary>
        public void Dispose ( );
    }
}