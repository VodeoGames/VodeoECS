using System;
using System.Collections.Generic;

namespace VodeoECS.Internal
{
    /// <summary>
    /// For internal use by the World class. Represents a combination of filter values.
    /// </summary>
    public readonly struct FilterCombination : IEquatable<FilterCombination>
    {
        public static FilterCombination Null { get { return new FilterCombination( ); } }
        public static FilterCombination Default { get { return new FilterCombination( empty ); } }
        public static FilterCombination NewEmpty { get { return new FilterCombination( new RegistryIndex<IFilterComponent>[0] ); } }

        private static IEqualityComparer<HashSet<RegistryIndex<IFilterComponent>>> hashComparer = HashSet<RegistryIndex<IFilterComponent>>.CreateSetComparer( );

        public readonly HashSet<RegistryIndex<IFilterComponent>> filterComponentInstances;

        public FilterCombination ( params RegistryIndex<IFilterComponent>[] components )
        {
            this.filterComponentInstances = new HashSet<RegistryIndex<IFilterComponent>>( components );
        }
        public bool Equals ( FilterCombination other )
        {
            if ( this.filterComponentInstances == null && other.filterComponentInstances == null )
                return true;
            if ( this.filterComponentInstances == null || other.filterComponentInstances == null )
                return false;
            return this.filterComponentInstances.SetEquals( other.filterComponentInstances );
        }
        public static bool operator == ( FilterCombination x, FilterCombination y ) { return x.Equals( y ); }
        public static bool operator != ( FilterCombination x, FilterCombination y ) { return !( x.Equals( y ) ); }
        public override bool Equals ( object o )
        {
            if ( o == null ) return false;
            else return this == ( FilterCombination )o;
        }
        public override int GetHashCode ( )
        {
            return hashComparer.GetHashCode( filterComponentInstances );
        }
        private static RegistryIndex<IFilterComponent>[] empty = new RegistryIndex<IFilterComponent>[0];
    }
}