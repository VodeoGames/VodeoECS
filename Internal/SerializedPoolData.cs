namespace VodeoECS.Internal
{
    /// <summary>
    /// For internal use with the ECS Serializer. Data structure for storing serialized Component Pool data. 
    /// Depending on the Pool type, some byte arrays will be null. 
    /// </summary>
    public struct SerializedPoolData
    {
        public byte[] filterIndices;
        public byte[] elementCounts;
        public byte[] entities;
        public byte[] components;
    }
}