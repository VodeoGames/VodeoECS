using Unity.Mathematics;
using VodeoECS.Standard;
using VodeoECS;

public class PhysicsSystem : ScheduledSystemECS
{
    private EventListener<NewPhysicalEvent> newPhysicalEvent;
    private EventEmitter<DestroyEntityEvent> destroyEvents;

    private DataComponentPool<PhysicsComponent> physicsPool;
    private DataComponentPool<TrajectoryComponent> trajectoryPool;

    private float gravity;
    private float deltaT;

    public PhysicsSystem ( World world, float gravity = -90f, float deltaT = 0.1f ) : base( world, "Physics" )
    {
        newPhysicalEvent = world.Events.GetListener<NewPhysicalEvent>( this );
        destroyEvents = world.Events.GetEmitter<DestroyEntityEvent>( this );
        physicsPool = world.GetDataComponentPool<PhysicsComponent>( );
        trajectoryPool = world.GetDataComponentPool<TrajectoryComponent>( );

        this.gravity = gravity;
        this.deltaT = deltaT;
    }

    public override void Initialize ( ) { }

    public override void ProcessEvents ( )
    {
        foreach ( NewPhysicalEvent e in newPhysicalEvent )
        {
            DataAccessor<PhysicsComponent> physicsAccessor = physicsPool[e.entity];
            PhysicsComponent physics = physicsAccessor.Value;
            DataAccessor<TrajectoryComponent> trajectoryAccessor = trajectoryPool[e.entity];

            physics.velocity = e.velocity;
            physics.deathTime = e.deathTime;
            TrajectoryComponent trajectory = new TrajectoryComponent( )
            {
                start = e.moment,
                end = e.moment + new float4( )
                {
                    x = physics.velocity.x * this.deltaT,
                    y = physics.velocity.y * this.deltaT,
                    z = physics.velocity.z * this.deltaT,
                    w = this.deltaT
                }
            };

            physicsAccessor.Write( physics );
            trajectoryAccessor.Write( trajectory );

            this.ScheduleQueue.Schedule( e.entity, e.moment.w );
        }
    }

    public override void UpdateEntity ( Entity entity, float time )
    {
        DataAccessor<PhysicsComponent> physicsAccessor = physicsPool[entity];
        PhysicsComponent physics = physicsAccessor.Value;
        DataAccessor<TrajectoryComponent> trajectoryAccessor = trajectoryPool[entity];
        TrajectoryComponent trajectory = trajectoryAccessor.Value;

        float lastDelta = trajectory.end.w - trajectory.start.w;
        physics.velocity = physics.velocity + new float3( ) { x = 0, y = this.gravity * lastDelta, z = 0 };
        trajectory = new TrajectoryComponent( )
        {
            start = trajectory.end,
            end = trajectory.end + new float4( )
            {
                x = physics.velocity.x * this.deltaT,
                y = physics.velocity.y * this.deltaT,
                z = physics.velocity.z * this.deltaT,
                w = this.deltaT
            }
        };

        physicsAccessor.Write( physics );
        trajectoryAccessor.Write( trajectory );

        if (physics.deathTime > time)
        {
            this.ScheduleQueue.Schedule( entity, trajectory.start.w + this.deltaT );
        }
        else
        {
            this.destroyEvents.CreateEvent( new DestroyEntityEvent( ) { entity = entity } );
        }
    }

    public override void Dispose ( ) { }
}
