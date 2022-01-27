using VodeoECS;

public struct PathComponent : IDataComponent
{
    public Entity destination; //final destination of path
    public ushort step; //current step in path
    public float range; //range within which to pathfind
    public float invertedSpeed;
}