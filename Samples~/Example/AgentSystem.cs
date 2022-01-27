using Unity.Mathematics;
using UnityEngine;
using VodeoECS;
using VodeoECS.Standard;

public class AgentSystem : ScheduledSystemECS
{
    private World world;

    private DataComponentPool<TravelStateComponent> states;
    private DataComponentPool<ScaleComponent> scales;
    private DataComponentPool<StaticPositionComponent> positions;
    private DataComponentPool<PathComponent> paths;
    private DataComponentPool<HomeComponent> homes;
    private ListComponentPool<RoadConnectionElement> connectionLists;
    private FilterComponentPool<MaterialFilter> materials;

    private MaterialFilter agentMaterial;
    private MaterialFilter returningMaterial;

    private EventListener<PathCompleteEvent> pathEvents;
    private EventListener<ChangeHomeEvent> homeEvents;

    private EventEmitter<WorldLoadedEvent> updateEmitter;

    private Archetype roadNodeArchetype;
    private Archetype homeArchetype;

    private ScheduleQueue pathfindingQueue;

    public AgentSystem ( World world ) : base( world, "Agents" )
    {
        this.world = world;

        this.states = world.GetDataComponentPool<TravelStateComponent>( );
        this.scales = world.GetDataComponentPool<ScaleComponent>( );
        this.positions = world.GetDataComponentPool<StaticPositionComponent>( );
        this.paths = world.GetDataComponentPool<PathComponent>( );
        this.connectionLists = world.GetListComponentPool<RoadConnectionElement>( );
        this.materials = world.GetFilterComponentPool<MaterialFilter>( );
        this.homes = world.GetDataComponentPool<HomeComponent>( );

        pathEvents = world.Events.GetListener<PathCompleteEvent>( this );
        homeEvents = world.Events.GetListener<ChangeHomeEvent>( this );
        updateEmitter = world.Events.GetEmitter<WorldLoadedEvent>( this );

        roadNodeArchetype = world.DefineArchetype( connectionLists, positions );
        homeArchetype = world.DefineArchetype( homes );
    }
    public override void UpdateEntity ( Entity agent, float time )
    {
        Entity roadNode;
        MaterialFilter material;
        DataAccessor<TravelStateComponent> stateAccessor = states[agent];
        DataAccessor<ScaleComponent> scaleAccessor = scales[agent];
        TravelStateComponent state = stateAccessor.Value;
        if ( state.returning == 0)
        {
            state.returning = 1;
            stateAccessor.Write( state );
            scaleAccessor.Write( new ScaleComponent( ) { scale = new float3( 2.5f, 2.5f, 2.5f ) } );
            material = agentMaterial;
            roadNode = world.GetRandomEntityOfArchetype( positions, this.world.MakeQuery( roadNodeArchetype ) );
        }
        else
        {
            state.returning = 0;
            stateAccessor.Write( state );
            scaleAccessor.Write( new ScaleComponent( ) { scale = new float3( 3.5f, 3.5f, 3.5f ) } );
            material = returningMaterial;
            roadNode = homes[state.home].Value.homeNode;
        }

        PathComponent component = paths[agent].Value;
        component.destination = roadNode;

        paths[agent].Write( component );

        materials.SetFilter( agent, material );

        pathfindingQueue.Schedule( agent, time );
    }
    public override void Dispose ( ) { }
    public override void Initialize ( )
    {
        pathfindingQueue = world.ScheduleQueues.GetQueue( "PathFinding", this );

        returningMaterial = materials[world.Prototypes.GetPrototype( "ReturningAgent" )];
        agentMaterial = materials[world.Prototypes.GetPrototype( "Agent" )];
    }
    public override void ProcessEvents ( )
    {
        foreach ( PathCompleteEvent e in this.pathEvents )
        {
            this.ScheduleQueue.Schedule( e.entity, e.time );
        }
        foreach ( ChangeHomeEvent e in this.homeEvents )
        {
            Entity homeNode = world.GetRandomEntityOfArchetype( connectionLists, this.world.MakeQuery( roadNodeArchetype ) );
            StaticPositionComponent position = positions[homeNode].Value;
            Entity homeNest = world.GetRandomEntityOfArchetype( homes, this.world.MakeQuery( homeArchetype ) );
            positions[homeNest].Write( position );
            homes[homeNest].Write( new HomeComponent( ) { homeNode = homeNode } );

            updateEmitter.CreateEvent( new WorldLoadedEvent( ) { } );
        }
    }
}