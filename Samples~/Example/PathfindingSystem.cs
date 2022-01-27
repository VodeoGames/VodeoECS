using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VodeoECS;
using VodeoECS.Standard;

public class PathFindingSystem : ScheduledSystemECS
{
    private int pathBufferSize;

    private DataComponentPool<TrajectoryComponent> trajectories;
    private DataComponentPool<RotationComponent> rotations;
    private DataComponentPool<RoadComponent> roads;
    private DataComponentPool<StaticPositionComponent> positions;
    private DataComponentPool<PathComponent> paths;
    private ListComponentPool<RoadConnectionElement> connectionLists;
    private ListComponentPool<PathNodeElement> pathLists;

    private EventEmitter<PathCompleteEvent> pathCompleteEvents;

    public PathFindingSystem ( World world, int pathBufferSize = 64 ) : base( world, "PathFinding" )
    {
        this.trajectories = world.GetDataComponentPool<TrajectoryComponent>( );
        this.rotations = world.GetDataComponentPool<RotationComponent>( );
        this.roads = world.GetDataComponentPool<RoadComponent>( );
        this.positions = world.GetDataComponentPool<StaticPositionComponent>( );
        this.paths = world.GetDataComponentPool<PathComponent>( );
        this.pathLists = world.GetListComponentPool<PathNodeElement>( );
        this.connectionLists = world.GetListComponentPool<RoadConnectionElement>( );

        this.pathCompleteEvents = world.Events.GetEmitter<PathCompleteEvent>( this );

        this.pathBufferSize = pathBufferSize;
    }
    public override void UpdateEntity ( Entity pathEntity, float time )
    {
        DataAccessor<TrajectoryComponent> trajectory = trajectories[pathEntity];
        DataAccessor<PathComponent> path = paths[pathEntity];
        ListAccessor<PathNodeElement> list = pathLists[pathEntity];

        float deadline = trajectory.Value.end.w;

        PathComponent component = path.Value;
        PathNodeElement element = list[component.step];

        Entity nextStepEntity = element.node;

        bool waitingForUpdate = false;
        if ( component.step < list.Length - 1 ) // next step in path
        {
            component.step += 1;
            path.Write( component );
        }
        else if ( nextStepEntity == component.destination ) // at destination
        {
            pathCompleteEvents.CreateEvent( new PathCompleteEvent( ) { entity = pathEntity, time = time } );
            waitingForUpdate = true;
        }
        else // no path, create a new path to destination
        {
            list.ClearList( );
            component.step = 0;

            FindPathJob job = new FindPathJob( )
            {
                bufferSize = this.pathBufferSize,

                startNodeEntity = nextStepEntity,
                endNodeEntity = component.destination,

                connections = connectionLists.NewListPoolAccessor( Allocator.TempJob ),
                roads = roads.NewDataPoolAccessor( Allocator.TempJob ),
                positions = positions.NewDataPoolAccessor( Allocator.TempJob ),
                output_reversePathNodes = new NativeList<Entity>( pathBufferSize, Allocator.TempJob ),

                range = component.range,

                openSet = new NativePriorityQueue<OpenSetEntry>( pathBufferSize, Allocator.TempJob ),
                closedSet = new NativeHashMap<Entity, Entity>( pathBufferSize, Allocator.TempJob ),

                output_path = component,
            };

            job.Schedule( ).Complete( );

            component = job.output_path;
            path.Write( component );

            for ( int i = job.output_reversePathNodes.Length - 1; i >= 0; i-- )
            {
                list.AppendElement( new PathNodeElement( ) { node = job.output_reversePathNodes[i] } );
            }

            job.output_reversePathNodes.Dispose( );
            job.openSet.Dispose( );
            job.closedSet.Dispose( );
            job.connections.Dispose( );
            job.roads.Dispose( );
            job.positions.Dispose( );
        }

        //set next trajectory
        if ( !waitingForUpdate )
        {
            nextStepEntity = list[component.step].node;
            StaticPositionComponent position = positions[nextStepEntity].Value;

            float4 newstart = trajectory.Value.end;

            float distance = math.length( position.position - new float3( newstart.x, newstart.y, newstart.z ) );
            float ETA = deadline + distance * component.invertedSpeed;
            float4 newend = new float4( position.position, ETA );
            trajectory.Write( new TrajectoryComponent
            {
                start = newstart,
                end = newend
            } );

            float4 difference = ( newend - newstart );
            Vector3 direction = new Vector3( difference.x, difference.y, difference.z );
            Quaternion quaternion = Quaternion.LookRotation( direction );
            rotations[pathEntity].Write( new RotationComponent( ) { quaternion = new float4(quaternion.x, quaternion.y, quaternion.z, quaternion.w) } );

            this.ScheduleQueue.Schedule( pathEntity, ETA );
        }
    }

    [BurstCompile]
    private struct FindPathJob : IJob
    {
        [ReadOnly] public int bufferSize;

        [ReadOnly] public Entity startNodeEntity;
        [ReadOnly] public Entity endNodeEntity;

        [ReadOnly] [NoAlias] public ListPoolAccessor<RoadConnectionElement> connections;
        [ReadOnly] [NoAlias] public DataPoolAccessor<RoadComponent> roads;
        [ReadOnly] [NoAlias] public DataPoolAccessor<StaticPositionComponent> positions;

        [ReadOnly] public float range;

        [NoAlias] public NativePriorityQueue<OpenSetEntry> openSet;
        [NoAlias] public NativeHashMap<Entity, Entity> closedSet;

        [WriteOnly] public PathComponent output_path;
        [WriteOnly] [NoAlias] public NativeList<Entity> output_reversePathNodes;

        public void Execute ( )
        {
            float cost = 0;
            Entity previous = Entity.Null;
            Entity currentNodeEntity = startNodeEntity;
            Entity bestCandidate = startNodeEntity;
            float bestDistance = float.MaxValue;

            closedSet.TryAdd( currentNodeEntity, previous );

            while ( !( currentNodeEntity == endNodeEntity ) )
            {
                for ( int i = 0; i < connections[currentNodeEntity].Length; i++ ) //for all roads connected to node
                {
                    Entity roadEntity = connections[currentNodeEntity][i].road;
                    RoadComponent connectedRoad = roads[roadEntity].Value;

                    Entity nextNode; //find the other node
                    if ( connectedRoad.nodeA == currentNodeEntity )
                    {
                        nextNode = connectedRoad.nodeB;
                    }
                    else
                    {
                        nextNode = connectedRoad.nodeA;
                    }

                    if ( !closedSet.ContainsKey( nextNode ) )
                    {
                        float3 pos1 = positions[nextNode].Value.position;
                        float3 pos2 = positions[endNodeEntity].Value.position;
                        float3 pos3 = positions[currentNodeEntity].Value.position;
                        float3 diff1 = pos2 - pos1;
                        float3 diff2 = pos3 - pos1;
                        float distanceLeft = math.length( diff1 );
                        float distanceNext = math.length( diff2 );
                        float entryCost = cost + distanceNext;

                        if ( entryCost < this.range )
                        {
                            float heuristic = entryCost + distanceLeft;

                            if ( distanceLeft < bestDistance )
                            {
                                bestCandidate = nextNode;
                                bestDistance = distanceLeft;
                            }

                            OpenSetEntry entry = new OpenSetEntry( ) { entity = nextNode, previous = currentNodeEntity, cost = entryCost };
                            openSet.Push( entry, heuristic );

                            closedSet.Add( nextNode, currentNodeEntity );
                        }
                    }
                }

                if ( ( openSet.Length == 0 ) && currentNodeEntity != endNodeEntity ) //break out if open set is empty
                {
                    currentNodeEntity = bestCandidate;
                    output_path.destination = currentNodeEntity;
                    break;
                }

                OpenSetEntry poppedEntry = openSet.Pop( );
                currentNodeEntity = poppedEntry.entity;
                cost = poppedEntry.cost;
            }
            //unroll path             
            while ( currentNodeEntity != startNodeEntity )
            {
                output_reversePathNodes.Add( currentNodeEntity );
                currentNodeEntity = closedSet[currentNodeEntity];
            }
        }
    }

    public override void Dispose ( ) { }
    public override void Initialize ( ) { }
    public override void ProcessEvents ( ) { }

    private struct OpenSetEntry
    {
        public Entity entity;
        public Entity previous;
        public float cost;
    }
}