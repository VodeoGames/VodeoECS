using VodeoECS;

public class PlayerSystem : PassiveSystemECS
{
    private World world;
    private EventListener<InputEvent> listener;
    private Archetype playerFlyer;

    private FilterComponentPool<PlayerFilter> playerPool;
    private DataComponentPool<FlyingComponent> flyerPool;
    public PlayerSystem ( World world ) : base( world ) 
    {
        this.world = world;
        this.listener = world.Events.GetListener<InputEvent>(this);

        this.playerPool = world.GetFilterComponentPool<PlayerFilter>( );
        this.flyerPool = world.GetDataComponentPool<FlyingComponent>( );

        this.playerFlyer = world.DefineArchetype( flyerPool, playerPool );
    }

    public override void Dispose ( ) { }

    public override void Initialize ( ) { }

    public override void ProcessEvents ( ) 
    {
        foreach (InputEvent e in listener)
        {
            PlayerFilter filter = new PlayerFilter( );
            Query query = world.MakeQuery( playerFlyer, filter );

            foreach ( DataAccessor<FlyingComponent> accessor in flyerPool.Matching(query))
            {
                FlyingComponent component = accessor.Value;
                component.pitchAxis = e.pitchAxis;
                component.rollAxis = e.rollAxis;
                component.yawAxis = e.yawAxis;
                component.throttle = e.throttle;
                accessor.Write(component);
            }
        }
    }
}