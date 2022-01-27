using Unity.Mathematics;
using UnityEngine;
using VodeoECS;
using VodeoECS.Standard;

public class LevelGeneratorSystem : PassiveSystemECS
{
    private Unity.Mathematics.Random random;

    private World world;

    private DataComponentPool<RotationComponent> rotations;
    private DataComponentPool<StaticPositionComponent> staticPositions;
    private DataComponentPool<ScaleComponent> scales;
    private DataComponentPool<RoadComponent> roads;
    private DataComponentPool<TrajectoryComponent> trajectories;
    private DataComponentPool<PathComponent> paths;
    private DataComponentPool<HomeComponent> homes;
    private DataComponentPool<TravelStateComponent> travelStates;

    private ListComponentPool<PathNodeElement> pathLists;
    private ListComponentPool<RoadConnectionElement> roadConnectionLists;

    private Entity roadPrototype;
    private Entity roadNodePrototype;
    private Entity agentPrototype;
    private Entity cylinderPrototype;
    private Entity capsulePrototype;

    private EventEmitter<PathCompleteEvent> pathCompleteEvents;
    private EventEmitter<NewSpawnerEvent> newSpawnerEvents;

    private Archetype roadNodeArchetype;

    public LevelGeneratorSystem (World world) : base(world)
    {
        random = new Unity.Mathematics.Random( 839649 );
        
        this.world = world;

        rotations = world.GetDataComponentPool<RotationComponent>( );
        staticPositions = world.GetDataComponentPool<StaticPositionComponent>( );
        scales = world.GetDataComponentPool<ScaleComponent>( );
        roads = world.GetDataComponentPool<RoadComponent>( );
        trajectories = world.GetDataComponentPool<TrajectoryComponent>( );
        paths = world.GetDataComponentPool<PathComponent>( );
        homes = world.GetDataComponentPool<HomeComponent>( );
        travelStates = world.GetDataComponentPool<TravelStateComponent>( );

        roadConnectionLists = world.GetListComponentPool<RoadConnectionElement>( );
        pathLists = world.GetListComponentPool<PathNodeElement>( );

        this.pathCompleteEvents = world.Events.GetEmitter<PathCompleteEvent>( this );
        this.newSpawnerEvents = world.Events.GetEmitter<NewSpawnerEvent>( this );

        roadNodeArchetype = world.DefineArchetype( roadConnectionLists, staticPositions );
    }

    public override void Initialize ( )
    {

        roadPrototype = world.Prototypes.GetPrototype( "Road" );
        roadNodePrototype = world.Prototypes.GetPrototype( "RoadNode" );
        agentPrototype = world.Prototypes.GetPrototype( "Agent" );
        cylinderPrototype = world.Prototypes.GetPrototype( "Cylinder" );
        capsulePrototype = world.Prototypes.GetPrototype( "AICapsule" );

        Entity startNode = GenerateNetwork( );
        CreateAgents( 1000, startNode );

        newSpawnerEvents.CreateEvent( new NewSpawnerEvent( ) {
            moment = new float4( 0, -100, 200, 0 ), spawner = new SpawnerComponent( )
            {
                outputRate = 0.1f,
                outputVelocity = 50f,
                prototype = cylinderPrototype,
                lifeTime = 10f
            }
        } );

        newSpawnerEvents.CreateEvent( new NewSpawnerEvent( )
        {
            moment = new float4( 150, -100, 0, 0 ),
            spawner = new SpawnerComponent( )
            {
                outputRate = 0.1f,
                outputVelocity = 50f,
                prototype = cylinderPrototype,
                lifeTime = 10f
            }
        } );

        newSpawnerEvents.CreateEvent( new NewSpawnerEvent( )
        {
            moment = new float4( -200, -100, -200, 0 ),
            spawner = new SpawnerComponent( )
            {
                outputRate = 0.1f,
                outputVelocity = 50f,
                prototype = cylinderPrototype,
                lifeTime = 10f
            }
        } );

        for ( int i = 0; i < 100; i++ )
        {
            world.Events.GetEmitter<InitializeFlyerEvent>( null ).CreateEvent( new InitializeFlyerEvent( ) { flyer = world.InstantiatePrototype( capsulePrototype ), time = 0 } );
        }

        //Create player entity
        Entity playerPrototype = world.Prototypes.GetPrototype( "PlayerCapsule" );
        Entity player = world.InstantiatePrototype( playerPrototype );
        world.Events.GetEmitter<InitializeFlyerEvent>( null ).CreateEvent( new InitializeFlyerEvent( ) { flyer = player, time = 0 } );
        world.GetDataComponentPool<TrajectoryComponent>( ).WriteInstantiated( new TrajectoryComponent( ) { start = new float4( -56, -150, -270, 0 ), end = new float4( -56, -150, -270, 0.1f ) } );
        Quaternion rotation = Quaternion.Euler( -30, -2, 1 );
        world.GetDataComponentPool<RotationComponent>( ).WriteInstantiated( new RotationComponent( ) { quaternion = new float4( rotation.x, rotation.y, rotation.z, rotation.w) } );
    }

    public override void ProcessEvents ( ) { }

    public override void Dispose ( ) { }

    private Entity GenerateNetwork ( )
    {
        float initialScale = 20f;
        float deltaScale = 20f;
        Entity root = world.InstantiatePrototype( roadNodePrototype );
        float3 initialFork = initialScale * new float3( ) { x = random.NextFloat( ), y = random.NextFloat( ), z = random.NextFloat( ) };
        ForkRoad( root, new float3( ), initialFork, deltaScale, 12 );
        ForkRoad( root, new float3( ), -initialFork, deltaScale, 12 );
        return root;
    }

    private void ForkRoad(Entity node, float3 position, float3 delta, float scale, int depth)
    {
        Entity newNode = world.InstantiatePrototype( roadNodePrototype );
        float3 newDelta = 0.95f*delta + scale*new float3( ) { x = random.NextFloat( )- random.NextFloat( ), y = random.NextFloat( )- random.NextFloat( ), z = random.NextFloat( )- random.NextFloat( ) };
        float3 newPosition = position + newDelta;
        Quaternion rotation = Quaternion.LookRotation( newDelta );
        staticPositions[newNode].Write( new StaticPositionComponent( ) { position =  newPosition } );

        Entity roadEntity = world.InstantiatePrototype( roadPrototype );
        RoadComponent roadComponent = roads.ReadInstantiated( );

        roadComponent.nodeA = node;
        roadComponent.nodeB = newNode;

        roads.WriteInstantiated( roadComponent );

        staticPositions[roadEntity].Write( new StaticPositionComponent( ) { position = 0.5f*(position + newPosition) } );
        rotations[roadEntity].Write( new RotationComponent( ) { quaternion = new float4( rotation.x, rotation.y, rotation.z, rotation.w ) } );
        scales[roadEntity].Write( new ScaleComponent( ) { scale = new float3( ) { x = 0.25f, y = 0.25f, z = ( new Vector3( newDelta.x, newDelta.y, newDelta.z ) ).magnitude * 0.5f } } );

        roadConnectionLists[node].AppendElement( new RoadConnectionElement( ) { road = roadEntity } );
        roadConnectionLists[newNode].AppendElement( new RoadConnectionElement( ) { road = roadEntity } );

        if ( depth > 0)
        {
            ForkRoad( newNode, newPosition, newDelta, scale, depth - 1 );
            ForkRoad( newNode, newPosition, newDelta, scale, depth - 1 );
        }
    }

    private void CreateAgents ( int n, Entity startNode )
    {
        Entity homeNode = world.GetRandomEntityOfArchetype( roadConnectionLists, this.world.MakeQuery( roadNodeArchetype ) );
        StaticPositionComponent position = staticPositions[homeNode].Value;
        Entity home = world.InstantiatePrototype( world.Prototypes.GetPrototype( "Home" ) );
        staticPositions.WriteInstantiated( position );
        homes.WriteInstantiated( new HomeComponent( ) { homeNode = homeNode } );

        for ( int i = 0; i < n; i++ )
        {
            StaticPositionComponent start_pos = staticPositions[startNode].Value;
            Entity agent = world.InstantiatePrototype( agentPrototype );

            travelStates.WriteInstantiated( new TravelStateComponent( ) { returning = 0, home = home } );

            pathLists.AppendElementToInstantiatedList( new PathNodeElement( ) { node = startNode } );
            PathComponent component = paths.ReadInstantiated();
            component.destination = startNode;
            paths.WriteInstantiated( component );

            float newdeadline = random.NextFloat( 10.0f );
            TrajectoryComponent trajectory = new TrajectoryComponent( )
            {
                start = new float4( start_pos.position, 0 ),
                end = new float4( start_pos.position, newdeadline )
            };
            trajectories.WriteInstantiated( trajectory );

            pathCompleteEvents.CreateEvent( new PathCompleteEvent( ) { entity = agent, time = newdeadline } );
        }
    }
}
