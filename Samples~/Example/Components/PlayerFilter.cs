using VodeoECS;

public struct PlayerFilter : IFilterComponent<PlayerFilter>
{
    public ushort player;
    public bool Equals ( PlayerFilter other )
    {
        return this.player == other.player;
    }
    public override int GetHashCode ( ) { return (this.player).GetHashCode( ); }
}