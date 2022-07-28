using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace VodeoECS.Standard
{
    using RenderObjectType = RegistryIndex<PrefabPool>;

    /// <summary>
    /// Vodeo ECS Standard GameObject Rendering System.
    /// Dynamically creates and updates GameObjects for rendering Entities that have either:
    /// - a StaticPositionComponent, RotationComponent, and RenderLayerFilter, and ObjectRendererComponent component.
    /// - a TrajectoryComponent, RotationComponent, and RenderLayerFilter, and ObjectRendererComponent component.
    /// - a RenderParentComponent, RotationComponent, and RenderLayerFilter, and ObjectRendererComponent component.
    /// Responds to WorldLoadedEvent by refreshing the objects with a StaticPositionComponent.
    /// Responds to DestroyEntityEvent by destroying any GameObject associated with the Entity.
    /// Responds to ComponentCreationEvent<ObjectRendererComponent>> by creating a GameObject to render the Entity.
    /// </summary>
    public class ObjectRenderSystem : FrameSystemECS
    {
        public static Quaternion GetQuaternion ( int rotation )
        {
            rotation = rotation % 4;
            switch ( rotation )
            {
                case 0:
                    return Quaternion.AngleAxis( 0.0f, Vector3.up );
                case 1:
                    return Quaternion.AngleAxis( 90.0f, Vector3.up );
                case 2:
                    return Quaternion.AngleAxis( 180.0f, Vector3.up );
                default:
                    return Quaternion.AngleAxis( 270.0f, Vector3.up );
            }
        }
        private JobHandle renderHandle;
        private Archetype dynamicRenderableArchetype;
        private Archetype staticRenderableArchetype;
        private Archetype parentedRenderableArchetype;

        private DataComponentPool<ObjectRendererComponent> renderers;
        
        private TransformAccessArray[] DynamicTransformArrays; //each array represents a render set in the current map
        private NativeList<Entity>[] DynamicEntityArrays; //entities associated with each transform in the previous array
        private Dictionary<Entity, ushort>[] DynamicIndices; //indices into the transform array

        private TransformAccessArray[] StaticTransformArrays;
        private NativeList<Entity>[] StaticEntityArrays;
        private Dictionary<Entity, ushort>[] StaticIndices;

        private TransformAccessArray[] ParentedTransformArrays;
        private NativeList<Entity>[] ParentedEntityArrays;
        private Dictionary<Entity, ushort>[] ParentedIndices;

        private readonly DataComponentPool<StaticPositionComponent> positions;
        private readonly DataComponentPool<TrajectoryComponent> trajectories;
        private readonly DataComponentPool<RenderParentComponent> parents;
        private readonly DataComponentPool<RotationComponent> rotations;
        private readonly FilterComponentPool<RenderLayerFilter> renderFilters;

        private NamedRegistry<PrefabPool> prefabPoolRegistry;

        private World world;

        private List<InterpolateTrajectories> jobs;

        private EventListener<ComponentCreationEvent<ObjectRendererComponent>> creationEvents;
        private EventListener<ComponentDestructionEvent<ObjectRendererComponent>> componentDestructionEvents;
        private EventListener<DestroyEntityEvent> destructionEvents;
        private EventListener<WorldLoadedEvent> loadedEvents;

        public ObjectRenderSystem ( World world, int maxSets = 16, int transformCapacity = 64 ) : base( world )
        {
            this.world = world;

            this.prefabPoolRegistry = new NamedRegistry<PrefabPool>( new PrefabLoader( ) );
            world.Prototypes.GetSerializer( ).Converters.Add( new NamedRegistryConverter<PrefabPool>( this.prefabPoolRegistry ) ); //add custom converter to serializer

            this.renderers = world.GetDataComponentPool<ObjectRendererComponent>( );
            this.positions = world.GetDataComponentPool<StaticPositionComponent>( );
            this.trajectories = world.GetDataComponentPool<TrajectoryComponent>( );
            this.parents = world.GetDataComponentPool<RenderParentComponent>( );
            this.rotations = world.GetDataComponentPool<RotationComponent>( );

            this.DynamicTransformArrays = new TransformAccessArray[maxSets];
            this.StaticTransformArrays = new TransformAccessArray[maxSets];
            this.ParentedTransformArrays = new TransformAccessArray[maxSets];

            this.DynamicEntityArrays = new NativeList<Entity>[maxSets];
            this.StaticEntityArrays = new NativeList<Entity>[maxSets];
            this.ParentedEntityArrays = new NativeList<Entity>[maxSets];

            this.DynamicIndices = new Dictionary<Entity, ushort>[maxSets];
            this.StaticIndices = new Dictionary<Entity, ushort>[maxSets];
            this.ParentedIndices = new Dictionary<Entity, ushort>[maxSets];

            this.renderFilters = world.GetFilterComponentPool<RenderLayerFilter>( );

            this.jobs = new List<InterpolateTrajectories>( );

            dynamicRenderableArchetype = world.DefineArchetype( trajectories, rotations, renderers, renderFilters );
            staticRenderableArchetype = world.DefineArchetype( positions, rotations, renderers, renderFilters );
            parentedRenderableArchetype = world.DefineArchetype( parents, rotations, renderers, renderFilters );

            for ( int i = 0; i < maxSets; i++ )
            {
                this.DynamicTransformArrays[i] = new TransformAccessArray( transformCapacity );
                this.DynamicEntityArrays.SetValue( new NativeList<Entity>( transformCapacity, Allocator.Persistent ), i );
                this.DynamicIndices[i] = new Dictionary<Entity, ushort>( );

                this.StaticTransformArrays[i] = new TransformAccessArray( transformCapacity );
                this.StaticEntityArrays.SetValue( new NativeList<Entity>( transformCapacity, Allocator.Persistent ), i );
                this.StaticIndices[i] = new Dictionary<Entity, ushort>( );

                this.ParentedTransformArrays[i] = new TransformAccessArray( transformCapacity );
                this.ParentedEntityArrays.SetValue( new NativeList<Entity>( transformCapacity, Allocator.Persistent ), i );
                this.ParentedIndices[i] = new Dictionary<Entity, ushort>( );
            }

            creationEvents = world.Events.GetListener<ComponentCreationEvent<ObjectRendererComponent>>( this );
            componentDestructionEvents = world.Events.GetListener<ComponentDestructionEvent<ObjectRendererComponent>>( this );
            destructionEvents = world.Events.GetListener<DestroyEntityEvent>( this );
            loadedEvents = world.Events.GetListener<WorldLoadedEvent>( this );

#if DEBUG
            world.objectRenderSystem = this;
#endif

    }


    public override void ProcessEvents ( )
        {
            foreach (WorldLoadedEvent loadedEvent in this.loadedEvents )
            {

                foreach ( PrefabPool pool in prefabPoolRegistry ) pool.DespawnAll( );
                int length = DynamicTransformArrays.Length;
                for ( int i = 0; i < length; i++ )
                {
                    int c = DynamicTransformArrays[i].capacity;

                    this.DynamicEntityArrays[i].Clear( );
                    this.DynamicIndices[i].Clear( );
                    this.DynamicTransformArrays[i].Dispose( );
                    this.DynamicTransformArrays[i] = new TransformAccessArray( c );

                    this.StaticEntityArrays[i].Clear( );
                    this.StaticIndices[i].Clear( );
                    this.StaticTransformArrays[i].Dispose( );
                    this.StaticTransformArrays[i] = new TransformAccessArray( c );

                    this.ParentedEntityArrays[i].Clear( );
                    this.ParentedIndices[i].Clear( );
                    this.ParentedTransformArrays[i].Dispose( );
                    this.ParentedTransformArrays[i] = new TransformAccessArray( c );
                }

                for ( byte l = 0; l < length; l++ )
                {
                    RenderLayerFilter layerFilter = new RenderLayerFilter( ) { layer = l };

                    Query staticQuery = this.world.MakeQuery( staticRenderableArchetype, layerFilter );
                    IEnumerator<DataAccessor<StaticPositionComponent>> positionComponents = positions.Matching( staticQuery ).GetEnumerator( );
                    IEnumerator<DataAccessor<RotationComponent>> rotationComponentsStatic = rotations.Matching( staticQuery ).GetEnumerator( );
                    foreach ( Entity entity in renderers.MatchingEntities( staticQuery ) )
                    {
                        positionComponents.MoveNext( );
                        rotationComponentsStatic.MoveNext( );
                        StaticPositionComponent position = positionComponents.Current.Value;
                        RotationComponent rotation = rotationComponentsStatic.Current.Value;

                        GameObject o = GetObject( entity, StaticTransformArrays[layerFilter.layer], StaticEntityArrays[layerFilter.layer], StaticIndices[layerFilter.layer] );
                        o.transform.localPosition = new Vector3( position.position.x, position.position.y, position.position.z );
                        o.transform.localRotation = new Quaternion( rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w );
                    }

                    Query dynamicQuery = this.world.MakeQuery( dynamicRenderableArchetype, layerFilter );
                    IEnumerator<DataAccessor<TrajectoryComponent>> trajectoryComponents = trajectories.Matching( dynamicQuery ).GetEnumerator( );
                    IEnumerator<DataAccessor<RotationComponent>> rotationComponentsDynamic = rotations.Matching( dynamicQuery ).GetEnumerator( );
                    foreach ( Entity entity in renderers.MatchingEntities( dynamicQuery ) )
                    {
                        trajectoryComponents.MoveNext( );
                        rotationComponentsDynamic.MoveNext( );
                        RotationComponent rotation = rotationComponentsDynamic.Current.Value;
                        TrajectoryComponent trajectory = trajectoryComponents.Current.Value;

                        GameObject o = GetObject( entity, DynamicTransformArrays[layerFilter.layer], DynamicEntityArrays[layerFilter.layer], DynamicIndices[layerFilter.layer] );
                        o.transform.localPosition = new Vector3( trajectory.start.x, trajectory.start.y, trajectory.start.z );
                        o.transform.localRotation = new Quaternion( rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w );
                    }

                    Query parentedQuery = this.world.MakeQuery( parentedRenderableArchetype, layerFilter );
                    IEnumerator<DataAccessor<ObjectRendererComponent>> renderComponentsParented = renderers.Matching( parentedQuery ).GetEnumerator( );
                    IEnumerator<DataAccessor<RenderParentComponent>> parentComponents = parents.Matching( parentedQuery ).GetEnumerator( );
                    IEnumerator<DataAccessor<RotationComponent>> rotationComponentsParented = rotations.Matching( parentedQuery ).GetEnumerator( );
                    foreach ( Entity entity in renderers.MatchingEntities( parentedQuery ) )
                    {
                        renderComponentsParented.MoveNext( );
                        parentComponents.MoveNext( );
                        rotationComponentsParented.MoveNext( );
                        ObjectRendererComponent renderer = renderComponentsParented.Current.Value;
                        RotationComponent rotation = rotationComponentsParented.Current.Value;
                        RenderParentComponent parent = parentComponents.Current.Value;

                        GameObject o = GetObject( entity, ParentedTransformArrays[layerFilter.layer], ParentedEntityArrays[layerFilter.layer], ParentedIndices[layerFilter.layer] );
                        o.transform.localPosition = new Vector3( parent.offset.x, parent.offset.y, parent.offset.z );
                        o.transform.localRotation = new Quaternion( rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w );

                        if ( world.IsEntityOfArchetype( parent.parent, dynamicRenderableArchetype ) )
                        {
                            ParentObject( o, entity, parent.parent, DynamicTransformArrays[layerFilter.layer], DynamicIndices[layerFilter.layer] );
                        }
                        else if ( world.IsEntityOfArchetype( parent.parent, staticRenderableArchetype ) )
                        {
                            ParentObject( o, entity, parent.parent, StaticTransformArrays[layerFilter.layer], StaticIndices[layerFilter.layer] );
                        }
                        else if ( world.IsEntityOfArchetype( parent.parent, parentedRenderableArchetype ) )
                        {
                            ParentObject( o, entity, parent.parent, ParentedTransformArrays[layerFilter.layer], ParentedIndices[layerFilter.layer] );
                        }
                    }
                }
            }
            foreach ( ComponentDestructionEvent<ObjectRendererComponent> componentDestructionEvent in this.componentDestructionEvents )
            {
                if ( world.IsEntityOfArchetype( componentDestructionEvent.entity, dynamicRenderableArchetype ) )
                {
                    int layer = renderFilters[componentDestructionEvent.entity].layer;
                    RemoveRenderer( componentDestructionEvent.entity, componentDestructionEvent.component, DynamicTransformArrays[layer], DynamicEntityArrays[layer], DynamicIndices[layer] );
                }
                else if ( world.IsEntityOfArchetype( componentDestructionEvent.entity, staticRenderableArchetype ) )
                {
                    int layer = renderFilters[componentDestructionEvent.entity].layer;
                    RemoveRenderer( componentDestructionEvent.entity, componentDestructionEvent.component, StaticTransformArrays[layer], StaticEntityArrays[layer], StaticIndices[layer] );
                }
                else if ( world.IsEntityOfArchetype( componentDestructionEvent.entity, parentedRenderableArchetype ) )
                {
                    int layer = renderFilters[componentDestructionEvent.entity].layer;
                    RemoveRenderer( componentDestructionEvent.entity, componentDestructionEvent.component, ParentedTransformArrays[layer], ParentedEntityArrays[layer], ParentedIndices[layer] );
                }
            }
            foreach (ComponentCreationEvent<ObjectRendererComponent> creationEvent in this.creationEvents)
            {
                if (world.IsEntityOfArchetype(creationEvent.entity, dynamicRenderableArchetype))
                {
                    RenderLayerFilter layerFilter = renderFilters[creationEvent.entity];
                    GameObject o = GetObject( creationEvent.entity, DynamicTransformArrays[layerFilter.layer], DynamicEntityArrays[layerFilter.layer], DynamicIndices[layerFilter.layer] );
                    TrajectoryComponent trajectory = trajectories[creationEvent.entity].Value;
                    RotationComponent rotation = rotations[creationEvent.entity].Value;
                    o.transform.localPosition = new Vector3( trajectory.start.x, trajectory.start.y, trajectory.start.z );
                    o.transform.localRotation = new Quaternion(rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w);
                }
                else if ( world.IsEntityOfArchetype( creationEvent.entity, staticRenderableArchetype ) )
                {
                    RenderLayerFilter layerFilter = renderFilters[creationEvent.entity];
                    GameObject o = GetObject( creationEvent.entity, StaticTransformArrays[layerFilter.layer], StaticEntityArrays[layerFilter.layer], StaticIndices[layerFilter.layer] );
                    StaticPositionComponent position = positions[creationEvent.entity].Value;
                    RotationComponent rotation = rotations[creationEvent.entity].Value;
                    o.transform.localPosition = new Vector3( position.position.x, position.position.y, position.position.z );
                    o.transform.localRotation = new Quaternion( rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w );
                }
                else if ( world.IsEntityOfArchetype( creationEvent.entity, parentedRenderableArchetype ) )
                {
                    RenderLayerFilter layerFilter = renderFilters[creationEvent.entity];
                    GameObject o = GetObject( creationEvent.entity, ParentedTransformArrays[layerFilter.layer], ParentedEntityArrays[layerFilter.layer], ParentedIndices[layerFilter.layer] );
                    RenderParentComponent parent = parents[creationEvent.entity].Value;
                    RotationComponent rotation = rotations[creationEvent.entity].Value;
                    o.transform.localPosition = new Vector3( parent.offset.x, parent.offset.y, parent.offset.z );
                    o.transform.localRotation = new Quaternion( rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w );

                    if ( world.IsEntityOfArchetype( parent.parent, dynamicRenderableArchetype ) )
                    {
                        ParentObject( o, creationEvent.entity, parent.parent, DynamicTransformArrays[layerFilter.layer], DynamicIndices[layerFilter.layer] );
                    }
                    else if ( world.IsEntityOfArchetype( parent.parent, staticRenderableArchetype ) )
                    {
                        ParentObject( o, creationEvent.entity, parent.parent, StaticTransformArrays[layerFilter.layer], StaticIndices[layerFilter.layer] );
                    }
                    else if ( world.IsEntityOfArchetype( parent.parent, parentedRenderableArchetype ) )
                    {
                        ParentObject( o, creationEvent.entity, parent.parent, ParentedTransformArrays[layerFilter.layer], ParentedIndices[layerFilter.layer] );
                    }
                }
            }
            foreach ( DestroyEntityEvent destructionEvent in this.destructionEvents )
            {
                if ( world.IsEntityOfArchetype( destructionEvent.entity, dynamicRenderableArchetype ) )
                {
                    int layer = renderFilters[destructionEvent.entity].layer;
                    RemoveRenderer( destructionEvent.entity, DynamicTransformArrays[layer], DynamicEntityArrays[layer], DynamicIndices[layer] );
                }
                else if ( world.IsEntityOfArchetype( destructionEvent.entity, staticRenderableArchetype ) )
                {
                    int layer = renderFilters[destructionEvent.entity].layer;
                    RemoveRenderer( destructionEvent.entity, StaticTransformArrays[layer], StaticEntityArrays[layer], StaticIndices[layer] );
                }
                else if ( world.IsEntityOfArchetype( destructionEvent.entity, parentedRenderableArchetype ) )
                {
                    int layer = renderFilters[destructionEvent.entity].layer;
                    RemoveRenderer( destructionEvent.entity, ParentedTransformArrays[layer], ParentedEntityArrays[layer], ParentedIndices[layer] );
                }
            }
        }

        public Entity getObjectEntity ( Transform transform )
        {
            while ( transform.parent != null && transform.parent.GetComponent<PrefabPool>( ) == null ) 
            {
                transform = transform.parent;
            }
            if ( transform.parent == null || transform.parent.GetComponent<PrefabPool>( ) == null ) return Entity.Null;

            for ( int i = 0; i < this.StaticTransformArrays.Length; i++ )
            {
                for ( int j = 0; j < this.StaticTransformArrays[i].length; j++ )
                {
                    Transform t = this.StaticTransformArrays[i][j];
                    if ( transform == t )
                    {
                        return this.StaticEntityArrays[i][j];
                    }
                }
            }
            for ( int i = 0; i < this.DynamicTransformArrays.Length; i++ )
            {
                for ( int j = 0; j < this.DynamicTransformArrays[i].length; j++ )
                {
                    Transform t = this.DynamicTransformArrays[i][j];
                    if ( transform == t )
                    {
                        return this.DynamicEntityArrays[i][j];
                    }
                }
            }
            return Entity.Null;
        }

        public Entity PickObject ( Vector2 screenPos, Camera camera )
        {
            Ray ray = camera.ScreenPointToRay( screenPos );
            RaycastHit hit;
            Physics.Simulate( 0.01f );
            if ( Physics.Raycast( ray, out hit ) )
            {
                Transform transform = hit.transform;
                return this.getObjectEntity( transform );
            }
            return Entity.Null;
        }

        public GameObject GetPrefab ( RenderObjectType objectType )
        {
            return this.prefabPoolRegistry[objectType].prefab;
        }

        public Transform GetTransform (Entity entity)
        {
            int layer = renderFilters[entity].layer;
            if ( world.IsEntityOfArchetype( entity, dynamicRenderableArchetype ) )
            {
                return this.GetObject( entity, DynamicTransformArrays[layer], DynamicEntityArrays[layer], DynamicIndices[layer] ).transform;
            }
            else if ( world.IsEntityOfArchetype( entity, staticRenderableArchetype ) )
            {
                return this.GetObject( entity, StaticTransformArrays[layer], StaticEntityArrays[layer], StaticIndices[layer] ).transform;
            }
            else if ( world.IsEntityOfArchetype( entity, parentedRenderableArchetype ) )
            {
                return this.GetObject( entity, ParentedTransformArrays[layer], ParentedEntityArrays[layer], ParentedIndices[layer] ).transform;
            }
            else return null;
        }

        public override void UpdateFrame ( float time )
        {
            for ( byte l = 0; l <= DynamicEntityArrays.Length; l++ )
            {
                RenderLayerFilter layerFilter = new RenderLayerFilter( ) { layer = l };
                foreach ( Taxon taxon in world.MakeQuery( dynamicRenderableArchetype, layerFilter ) )
                {
                    if ( DynamicEntityArrays[l].Length > 1 )
                    {
                        InterpolateTrajectories job = new InterpolateTrajectories( )
                        {
                            currentTime = time,
                            entities = this.DynamicEntityArrays[l].AsArray( ).Slice( ),
                            trajectories = trajectories.NewDataPoolAccessor( Allocator.TempJob ),
                            rotations = rotations.NewDataPoolAccessor( Allocator.TempJob )
                        };

                        this.jobs.Add( job );
                        JobHandle handle = job.Schedule( this.DynamicTransformArrays[l] );
                        renderHandle = JobHandle.CombineDependencies( renderHandle, handle );
                    }
                }
            }
            JobHandle.ScheduleBatchedJobs( );
        }

        public override void CompleteUpdate ( ) 
        {
            renderHandle.Complete( );

            foreach ( InterpolateTrajectories job in jobs )
            {
                job.trajectories.Dispose( );
                job.rotations.Dispose( );
            }
            jobs.Clear( );
        }


        [BurstCompile]
        private struct InterpolateTrajectories : IJobParallelForTransform
        {
            [ReadOnly] [NoAlias] public DataPoolAccessor<TrajectoryComponent> trajectories;
            [ReadOnly] [NoAlias] public DataPoolAccessor<RotationComponent> rotations;

            [ReadOnly] public float currentTime;

            [ReadOnly] [NoAlias] public NativeSlice<Entity> entities;

            public void Execute ( int i, TransformAccess transform )
            {
                Entity entity = entities[i];

                ComponentIndex index = rotations.GetIndex( entity );

                RotationComponent rotation = rotations[index].Value;

                TrajectoryComponent trajectory = trajectories[index].Value;
                float4 start = trajectory.start;
                float4 end = trajectory.end;

                float denominator = end.w - start.w;
                if ( denominator > 0 )
                {
                    float numerator = currentTime - start.w;
                    float ratio = numerator / denominator;

                    float3 position = math.lerp(
                    new float3( start.x, start.y, start.z ),
                    new float3( end.x, end.y, end.z ),
                    ratio );

                    transform.localPosition = new Vector3( position.x, position.y, position.z );
                }
                transform.rotation = new Quaternion( rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w );
            }
        }

        public override void Initialize ( ) { }
        public override void Dispose ( )
        {
            int l = this.DynamicTransformArrays.Length;
            for ( int i = 0; i < l; i++ )
            {
                this.DynamicTransformArrays[i].Dispose( );
                this.DynamicEntityArrays[i].Dispose( );
                this.StaticTransformArrays[i].Dispose( );
                this.StaticEntityArrays[i].Dispose( );
                this.ParentedEntityArrays[i].Dispose( );
                this.ParentedTransformArrays[i].Dispose( );
            }
        }

        private void RemoveRenderer ( Entity toRemove, TransformAccessArray transformArrays, NativeList<Entity> entityArrays, Dictionary<Entity, ushort> indices )
        {
            ObjectRendererComponent renderer = renderers[toRemove].Value;
            RemoveRenderer(toRemove, renderer, transformArrays, entityArrays, indices);
        }
        
        private void RemoveRenderer ( Entity toRemove, ObjectRendererComponent rendererToRemove, TransformAccessArray transformArrays, NativeList<Entity> entityArrays, Dictionary<Entity, ushort> indices )
        {
            ushort index = indices[toRemove];

            prefabPoolRegistry[rendererToRemove.objectType].Despawn( transformArrays[index].gameObject );

            Entity swapEntity = entityArrays[entityArrays.Length - 1];
            indices[swapEntity] = index;

            transformArrays.RemoveAtSwapBack( index );
            entityArrays.RemoveAtSwapBack( index );

            indices.Remove(toRemove);
        }

        private GameObject GetObject ( Entity toAdd, TransformAccessArray transformArrays, NativeList<Entity> entityArrays, Dictionary<Entity, ushort> indices )
        {
            ObjectRendererComponent renderer = renderers[toAdd].Value;

            GameObject o;

            ushort index;
            if (indices.TryGetValue(toAdd, out index))
            {
                o = transformArrays[index].gameObject;
            }
            else
            {
                o = prefabPoolRegistry[renderer.objectType].Spawn( );

                indices.Add( toAdd, ( ushort )transformArrays.length );
 
                transformArrays.Add( o.transform );
                entityArrays.Add( toAdd );
            }

            return o;
        }

        private void ParentObject (GameObject o, Entity child, Entity parent, TransformAccessArray transformArray, Dictionary<Entity, ushort> indices )
        {
            RenderParentComponent renderParent = parents[child].Value;
            o.transform.localPosition = new Vector3( renderParent.offset.x, renderParent.offset.y, renderParent.offset.z );

            o.transform.SetParent( transformArray[indices[parent]], false );
        }

    }
}
