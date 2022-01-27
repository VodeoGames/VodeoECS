using Unity.Mathematics;
using UnityEngine;
using VodeoECS;
using VodeoECS.Standard;

public class FlyingSystem : ScheduledSystemECS
{
    private EventListener<InitializeFlyerEvent> listener;

    private DataComponentPool<FlyingComponent> flyingPool;
    private DataComponentPool<RotationComponent> rotations;
    private DataComponentPool<TrajectoryComponent> trajectories;

    private float deltaT;

    public FlyingSystem (World world, float deltaT = 0.0166f ) : base( world, "Flying" ) 
    {
        this.deltaT = deltaT;
        this.flyingPool = world.GetDataComponentPool<FlyingComponent>( );
        this.rotations = world.GetDataComponentPool<RotationComponent>( );
        this.trajectories = world.GetDataComponentPool<TrajectoryComponent>( );

        world.DefineArchetype( flyingPool, rotations, trajectories );

        this.listener = world.Events.GetListener<InitializeFlyerEvent>( this );
    }

    public override void Dispose ( ) { }

    public override void Initialize ( ) { }

    public override void ProcessEvents ( ) 
    {
        foreach (InitializeFlyerEvent e in listener)
        {
            this.ScheduleQueue.Schedule( e.flyer, e.time );
        }
    }

    public override void UpdateEntity ( Entity entity, float time ) 
    {
        ComponentIndex index = flyingPool.GetIndex( entity );

        DataAccessor<FlyingComponent> flyingAccessor = flyingPool[index];
        DataAccessor<RotationComponent> rotationAccessor = rotations[index];
        DataAccessor<TrajectoryComponent> trajectoryAccessor = trajectories[index];
        FlyingComponent flyingComponent = flyingAccessor.Value;
        RotationComponent rotation = rotationAccessor.Value;
        TrajectoryComponent trajectory = trajectoryAccessor.Value;

        float lastDelta = trajectory.end.w - trajectory.start.w;

        Quaternion quaternion = new Quaternion( rotation.quaternion.x, rotation.quaternion.y, rotation.quaternion.z, rotation.quaternion.w);
        Quaternion rotationchange = Quaternion.Euler( lastDelta * flyingComponent.pitchAxis * flyingComponent.turnspeed, lastDelta * flyingComponent.yawAxis * flyingComponent.turnspeed, lastDelta * flyingComponent.rollAxis * flyingComponent.turnspeed );
        quaternion *= rotationchange;

        if ( flyingComponent.throttle == 0)
        {
            if (flyingComponent.velocity >= 0)
            {
                flyingComponent.velocity = math.clamp( flyingComponent.velocity + flyingComponent.acceleration * ( -0.5f ) * lastDelta, 0, flyingComponent.maxSpeed );
            }
            else
            {
                flyingComponent.velocity = math.clamp( flyingComponent.velocity + flyingComponent.acceleration * ( 0.5f ) * lastDelta, -flyingComponent.maxSpeed, 0 );
            }
        }
        else
        {
            flyingComponent.velocity = math.clamp( flyingComponent.velocity + flyingComponent.acceleration * ( flyingComponent.throttle ) * lastDelta, -flyingComponent.maxSpeed * 0.5f, flyingComponent.maxSpeed );
        }

        Vector3 vector = quaternion * Vector3.forward * flyingComponent.velocity;

        trajectory = new TrajectoryComponent( )
        {
            start = trajectory.end,
            end = trajectory.end + new float4( )
            {
                x = vector.x * this.deltaT,
                y = vector.y * this.deltaT,
                z = vector.z * this.deltaT,
                w = this.deltaT
            }
        };

        rotation.quaternion = new float4( quaternion.x, quaternion.y, quaternion.z, quaternion.w );

        flyingAccessor.Write( flyingComponent );
        rotationAccessor.Write( rotation );
        trajectoryAccessor.Write( trajectory );

        this.ScheduleQueue.Schedule( entity, trajectory.start.w + this.deltaT );
    }
}