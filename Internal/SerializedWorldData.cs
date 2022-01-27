namespace VodeoECS.Internal
{
    /// <summary>
    /// For internal use with the ECS Serializer. Data structure for storing World Data unrelated to individual Component Pools.
    /// </summary>
    public struct SerializedWorldData
    {
        public byte[] entities;
        public int recyclenext;
        public int nextfree;
    }
}