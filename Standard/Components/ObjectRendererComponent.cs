namespace VodeoECS.Standard
{
    using RenderObjectType = RegistryIndex<PrefabPool>;

    /// <summary>
    /// The type of rendering GameObject associated with the Entity.
    /// </summary>
    public struct ObjectRendererComponent : IDataComponent
    {
        public RenderObjectType objectType;
    }
}
