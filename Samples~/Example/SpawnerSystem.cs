using Unity.Mathematics;
using VodeoECS;
using VodeoECS.Standard;

public class SpawnerSystem : ScheduledSystemECS
{
    private World world;
    private Random random;
    
    private DataComponentPool<SpawnerComponent> spawnerPool;
    private DataComponentPool<StaticPositionComponent> positionPool;
    private DataComponentPool<RotationComponent> rotationPool;

    private EventListener<NewSpawnerEvent> newSpawnerEvents;
    private EventEmitter<NewPhysicalEvent> physicalEvents;

    private Entity spawnerPrototype;

    public SpawnerSystem(World world) : base(world, "Spawner" ) 
    {
        random = new Random( 334995 );
        newSpawnerEvents = world.Events.GetListener<NewSpawnerEvent>( this );
        physicalEvents = world.Events.GetEmitter<NewPhysicalEvent>( this );
        spawnerPool = world.GetDataComponentPool<SpawnerComponent>( );
        positionPool = world.GetDataComponentPool<StaticPositionComponent>( );
        rotationPool = world.GetDataComponentPool<RotationComponent>( );

        this.world = world;
    }

    public override void Initialize ( ) 
    {
        spawnerPrototype = world.Prototypes.GetPrototype( "Spawner" );
    }

    public override void ProcessEvents ( ) 
    {
        foreach ( NewSpawnerEvent e in newSpawnerEvents)
        {
            if (e.spawner.outputRate > 0)
            {
                Entity entity = world.InstantiatePrototype( spawnerPrototype );
                spawnerPool.WriteInstantiated( e.spawner );
                positionPool.WriteInstantiated( new StaticPositionComponent( ) { position = new float3(e.moment.x, e.moment.y, e.moment.z) } );

                this.ScheduleQueue.Schedule( entity, e.moment.w +random.NextFloat( ) * 2.0f * e.spawner.outputRate );
            }
        }
    }

    public override void UpdateEntity ( Entity entity, float time ) 
    {
        SpawnerComponent spawner = spawnerPool[entity].Value;
        StaticPositionComponent position = positionPool[entity].Value;
        Entity spawned = world.InstantiatePrototype( spawner.prototype );

        rotationPool.WriteInstantiated( new RotationComponent( ) { quaternion = random.NextFloat4( ) } );

        physicalEvents.CreateEvent( new NewPhysicalEvent( ) 
        {
            entity = spawned,

            moment = new float4( )
            {
                x = position.position.x,
                y = position.position.y,
                z = position.position.z,
                w = time
            },

            velocity = spawner.outputVelocity * new float3( )
            {
                x = random.NextFloat( ) - random.NextFloat( ),
                y = random.NextFloat( ) * 3.0f,
                z = random.NextFloat( ) - random.NextFloat( )
            },

            deathTime = time + spawner.lifeTime * random.NextFloat( ) * 2
        } );

        if ( spawner.outputRate > 0 )
        {
            this.ScheduleQueue.Schedule( entity, time + random.NextFloat( ) * 2.0f * spawner.outputRate );
        }
    }

    public override void Dispose ( ) { }
}
