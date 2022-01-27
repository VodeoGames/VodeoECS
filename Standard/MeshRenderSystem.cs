using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VodeoECS.Standard
{
    /// <summary>
    /// Vodeo ECS Standard Mesh Rendering System.
    /// Renders dynamic meshes for Entities that have a TrajectoryComponent, ScaleComponent, RotationComponent, MeshRenderFilter, and MaterialFilter.
    /// Renders static meshes for Entities that have a StaticPositionComponent, ScaleComponent, RotationComponent, MeshRenderFilter, and MaterialFilter.
    /// Responds to UpdateStaticMeshesEvent by updating static meshes.
    /// </summary>
    public class MeshRenderSystem : FrameSystemECS
    {
        private World world;

        private NamedRegistry<Mesh> meshRegistry;
        private NamedRegistry<Material> materialRegistry;

        private List<NativeList<Matrix4x4>> outputMatrices;
        private List<RegistryIndex<Mesh>> outputMeshes;
        private List<RegistryIndex<Material>> outputMaterials;

        private JobHandle job;

        private readonly int batchSize;
        private readonly Matrix4x4[] matricesSlice;
        private readonly DataComponentPool<TrajectoryComponent> trajectories;
        private readonly DataComponentPool<StaticPositionComponent> positions;
        private readonly DataComponentPool<ScaleComponent> scales;
        private readonly DataComponentPool<RotationComponent> rotations;
        private readonly FilterComponentPool<MeshRenderFilter> meshFilters;
        private readonly FilterComponentPool<MaterialFilter> materialFilters;

        private EventListener<UpdateStaticMeshesEvent> updateStaticEvents;

        private int outputCount = 0;
        private int staticCount = 0;

        Archetype dynamicMeshArchetype;
        Archetype staticMeshArchetype;
        public MeshRenderSystem ( World world, int batchSize = 64 ) : base( world )
        {
            this.world = world;

            this.meshRegistry = new NamedRegistry<Mesh>( new MeshLoader( ) );
            this.materialRegistry = new NamedRegistry<Material>( new MaterialLoader( ) );
            world.Prototypes.GetSerializer( ).Converters.Add( new NamedRegistryConverter<Mesh>( this.meshRegistry ) );
            world.Prototypes.GetSerializer( ).Converters.Add( new NamedRegistryConverter<Material>( this.materialRegistry ) );

            this.trajectories = world.GetDataComponentPool<TrajectoryComponent>( );
            this.positions = world.GetDataComponentPool<StaticPositionComponent>( );
            this.scales = world.GetDataComponentPool<ScaleComponent>( );
            this.rotations = world.GetDataComponentPool<RotationComponent>( );
            this.meshFilters = world.GetFilterComponentPool<MeshRenderFilter>( );
            this.materialFilters = world.GetFilterComponentPool<MaterialFilter>( );

            this.updateStaticEvents = world.Events.GetListener<UpdateStaticMeshesEvent>( this );

            this.batchSize = batchSize;
            matricesSlice = new Matrix4x4[1023];

            this.outputMatrices = new List<NativeList<Matrix4x4>>( );
            this.outputMaterials = new List<RegistryIndex<Material>>( );
            this.outputMeshes = new List<RegistryIndex<Mesh>>( );

            this.dynamicMeshArchetype = world.DefineArchetype( trajectories, rotations, scales, meshFilters, materialFilters );
            this.staticMeshArchetype = world.DefineArchetype( positions, rotations, scales, meshFilters, materialFilters );
        }
        public override void UpdateFrame ( float time )
        {
            outputCount = staticCount;
            foreach ( RegistryIndex<Material> material in this.materialRegistry.ByIndex )
            {
                MaterialFilter materialFilter = new MaterialFilter( ) { material = material };
                foreach ( Taxon taxon in world.MakeQuery( dynamicMeshArchetype, materialFilter ) )
                {
                    DataTaxonSlice<TrajectoryComponent> trajectoryComponents = trajectories.GetDataSlice( taxon );
                    DataTaxonSlice<RotationComponent> rotationComponents = rotations.GetDataSlice( taxon );
                    DataTaxonSlice<ScaleComponent> scaleComponents = scales.GetDataSlice( taxon );
                    FilterTaxonSlice<MeshRenderFilter> meshes = meshFilters.GetFilterSlice( taxon );

                    if ( meshes.Length > 0 )
                    {
                        MeshRenderFilter mesh = meshes[0];

                        if ( outputMatrices.Count <= outputCount )
                        {
                            this.outputMatrices.Add( new NativeList<Matrix4x4>( Allocator.Persistent ) );
                            this.outputMeshes.Add( mesh.mesh );
                            this.outputMaterials.Add( material );
                        }
                        else
                        {
                            outputMatrices[outputCount].Clear( );
                            outputMeshes[outputCount] = mesh.mesh;
                            outputMaterials[outputCount] = material;
                        }

                        int length = trajectoryComponents.Length;
                        this.outputMatrices[outputCount].Resize( length, NativeArrayOptions.UninitializedMemory );

                        Render renderJob = new Render( )
                        {
                            trajectories = trajectoryComponents,
                            rotations = rotationComponents,
                            scales = scaleComponents,
                            currentTime = time,
                            output = outputMatrices[outputCount].AsArray( )
                        };

                        JobHandle handle = renderJob.Schedule( length, batchSize );
                        job = JobHandle.CombineDependencies( job, handle );

                        outputCount++;
                    }
                }
            }
            JobHandle.ScheduleBatchedJobs( );
            job.Complete( );
        }

        public override void CompleteUpdate ( )
        {
            for ( int j = 0; j < outputCount; j++ )
            {
                int amount = outputMatrices[j].Length / 1023;
                int remainder = outputMatrices[j].Length % 1023;
                if ( remainder != 0 ) ++amount;

                for ( int i = 0; i < amount; i++ )
                {
                    int index = i * 1023;
                    int count = ( i == amount - 1 && remainder != 0 ) ? remainder : 1023;

                    NativeArray<Matrix4x4>.Copy( outputMatrices[j].AsArray( ).GetSubArray( index, count ), matricesSlice, count );

                    for ( int sm = 0; sm < this.meshRegistry[outputMeshes[j]].subMeshCount; sm++ )
                    {
                        Graphics.DrawMeshInstanced( this.meshRegistry[outputMeshes[j]], sm, this.materialRegistry[outputMaterials[j]], matricesSlice, count );
                    }
                }
            }
        }


        public override void Initialize ( )
        {
            world.Events.GetEmitter<UpdateStaticMeshesEvent>( this ).CreateEvent( new UpdateStaticMeshesEvent( ) );
        }
        public override void Dispose ( )
        {
            foreach ( NativeList<Matrix4x4> list in this.outputMatrices )
                list.Dispose( );
        }

        [BurstCompile]
        private struct Render : IJobParallelFor
        {
            [ReadOnly]
            public float currentTime;

            [ReadOnly]
            [NoAlias]
            public DataTaxonSlice<TrajectoryComponent> trajectories;

            [ReadOnly]
            [NoAlias]
            public DataTaxonSlice<RotationComponent> rotations;

            [ReadOnly]
            [NoAlias]
            public DataTaxonSlice<ScaleComponent> scales;

            [WriteOnly]
            [NoAlias]
            public NativeSlice<Matrix4x4> output;

            public void Execute ( int i )
            {
                float4 quaternion = rotations[i].quaternion;
                TrajectoryComponent trajectory = trajectories[i];
                float4 start = trajectory.start;
                float4 end = trajectory.end;

                float denominator = end.w - start.w;
                float numerator = currentTime - start.w;

                if ( denominator == 0 ) Debug.Log( "Invalid trajectory component caused division by 0" );

                float ratio = numerator / denominator;

                float3 pos = math.lerp(
                    new float3( start.x, start.y, start.z ),
                    new float3( end.x, end.y, end.z ),
                    ratio );

                Quaternion rot = new Quaternion(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                output[i] = Matrix4x4.TRS( new Vector3( pos.x, pos.y, pos.z ), rot, scales[i].scale );
            }
        }
        public override void ProcessEvents ( ) 
        {
            foreach ( UpdateStaticMeshesEvent e in this.updateStaticEvents )
            {
                staticCount = 0;
                foreach ( RegistryIndex<Material> material in this.materialRegistry.ByIndex )
                {
                    MaterialFilter materialFilter = new MaterialFilter( ) { material = material };
                    foreach ( Taxon taxon in world.MakeQuery( staticMeshArchetype, materialFilter ) )
                    {
                        DataTaxonSlice<StaticPositionComponent> positionComponents = positions.GetDataSlice( taxon );
                        DataTaxonSlice<RotationComponent> rotationComponents = rotations.GetDataSlice( taxon );
                        DataTaxonSlice<ScaleComponent> scaleComponents = scales.GetDataSlice( taxon );
                        FilterTaxonSlice<MeshRenderFilter> meshes = meshFilters.GetFilterSlice( taxon );

                        if ( meshes.Length > 0 )
                        {
                            MeshRenderFilter mesh = meshes[0];

                            if ( outputMatrices.Count <= staticCount )
                            {
                                this.outputMatrices.Add( new NativeList<Matrix4x4>( Allocator.Persistent ) );
                                this.outputMeshes.Add( mesh.mesh );
                                this.outputMaterials.Add( material );
                            }
                            else
                            {
                                outputMatrices[staticCount].Clear( );
                                outputMeshes[staticCount] = mesh.mesh;
                                outputMaterials[staticCount] = material;
                            }

                            for ( int i = 0; i < positionComponents.Length; i++ )
                            {
                                float4 quaternion = rotationComponents[i].quaternion;
                                this.outputMatrices[staticCount].Add( Matrix4x4.TRS( positionComponents[i].position, new Quaternion(quaternion.x, quaternion.y, quaternion.z, quaternion.w), scaleComponents[i].scale ) );
                            }

                            staticCount++;
                        }
                    }
                }
            }
        }
    }
}