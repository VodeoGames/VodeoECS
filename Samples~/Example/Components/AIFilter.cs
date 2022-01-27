using VodeoECS;

public struct AIFilter : IFilterComponent<AIFilter>
{
    public ushort AI;
    public bool Equals ( AIFilter other )
    {
        return this.AI == other.AI;
    }
    public override int GetHashCode ( ) { return (this.AI ).GetHashCode( ); }
}