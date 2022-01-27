namespace VodeoECS.Standard
{
    /// <summary>
    /// The rendering layer of the Entity.
    /// </summary>
    public struct RenderLayerFilter : IFilterComponent<RenderLayerFilter>
    {
        public byte layer;
        public bool Equals ( RenderLayerFilter other )
        {
            return (
                this.layer == other.layer
                );
        }
        public override int GetHashCode ( ) { return ( layer ).GetHashCode( ); }
    }
}
