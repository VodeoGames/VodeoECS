using VodeoECS;

public struct FlyingComponent : IDataComponent
{
    public float pitchAxis;
    public float yawAxis;
    public float rollAxis;
    public float throttle;

    public float maxSpeed;
    public float turnspeed;
    public float acceleration;

    public float velocity;
}