using Unity.Collections;

namespace VodeoECS
{
    /// <summary>
    /// Accessor for reading and writing to a single Data Component.
    /// Can be passed to Burst Compiled jobs. 
    /// </summary>
    /// <typeparam name="T">The type of Data Component.</typeparam>
    public struct DataAccessor<T> where T : unmanaged, IDataComponent
    {
        //Disabled because it discourages accessor re-use
        /// <summary>
        /// Implicit cast to value.
        /// </summary>
        /// <param name="a">The data accessor to cast.</param>
        //public static implicit operator T ( DataAccessor<T> a ) => a.Value;

        /// <summary>
        /// The value of the Data Component.
        /// </summary>
        public T Value { get { return slice[0]; } }

        /// <summary>
        /// For internal use by the Data Component Pool class. Constructs a Data Accessor.
        /// </summary>
        /// <param name="slice">The length 1 slice pointing to the Data Component.</param>
        public DataAccessor ( NativeSlice<T> slice )
        {
            this.slice = slice;
        }
        /// <summary>
        /// Write to the Data Component.
        /// </summary>
        /// <param name="value">The value to write to the Data Component.</param>
        public void Write ( T value ) { slice[0] = value; }

        private NativeSlice<T> slice;
    }
}