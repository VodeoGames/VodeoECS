using UnityEngine;
using VodeoECS;
using VodeoECS.Standard;

public class AISystem : ScheduledSystemECS
{
    private float deltaT;
    private Unity.Mathematics.Random random;

    private EventListener<InitializeFlyerEvent> listener;

    private FilterComponentPool<AIFilter> aiPool;
    private DataComponentPool<FlyingComponent> flyerPool;
    private DataComponentPool<TrajectoryComponent> trajectories;
    public AISystem ( World world, float deltaT = 1.0f ) : base( world, "AI" ) 
    {
        this.random = new Unity.Mathematics.Random( 931028 );
        this.deltaT = deltaT;

        this.flyerPool = world.GetDataComponentPool<FlyingComponent>( );
        this.trajectories = world.GetDataComponentPool<TrajectoryComponent>( );
        this.aiPool = world.GetFilterComponentPool<AIFilter>( );

        this.listener = world.Events.GetListener<InitializeFlyerEvent>( this );
    }

    public override void Dispose ( ) { }

    public override void Initialize ( ) { }

    public override void ProcessEvents ( ) 
    {
        foreach ( InitializeFlyerEvent e in listener )
        {
            if ( aiPool.HasComponent(e.flyer))
            {
                this.ScheduleQueue.Schedule( e.flyer, e.time );
            }
        }
    }

    public override void UpdateEntity ( Entity entity, float time )
    {
        TrajectoryComponent trajectory = trajectories[entity].Value;
        Vector3 vector = new Vector3( trajectory.end.x - trajectory.start.x, trajectory.end.y - trajectory.start.y, trajectory.end.z - trajectory.start.z);
        Vector3 position = new Vector3( trajectory.end.x, trajectory.end.y, trajectory.end.z );

        DataAccessor<FlyingComponent> flyingAccessor = flyerPool[entity];
        FlyingComponent component = flyingAccessor.Value;
        if ( Vector3.Dot(vector, position) >= 0 || random.NextFloat() < 0.1f)
        {
            component.pitchAxis = random.NextFloat( ) - random.NextFloat( );
            component.rollAxis = random.NextFloat( ) - random.NextFloat( );
            component.yawAxis = random.NextFloat( ) - random.NextFloat( );
            if (random.NextFloat() > 0.33f ) component.throttle = random.NextFloat( );
            else component.throttle = 0;
        }
        else
        {
            component.pitchAxis = ( random.NextFloat( ) - random.NextFloat( ) )*0.1f;
            component.rollAxis = random.NextFloat( ) - random.NextFloat( );
            component.yawAxis = ( random.NextFloat( ) - random.NextFloat( ) ) * 0.1f;
            component.throttle = random.NextFloat( );
        }
        flyingAccessor.Write( component );
        this.ScheduleQueue.Schedule( entity, random.NextFloat( ) * deltaT + random.NextFloat( ) * deltaT + time );
    }
}