using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// A registry class associating a set of unique items with a name and RegistryIndex for each one.
    /// </summary>
    /// <typeparam name="T">The type registered in this NamedRegistry.</typeparam>
    public class NamedRegistry<T> : Registry<T>
    {
        private readonly Dictionary<string, RegistryIndex<T>> nameLookup;
        private readonly List<string> names;
        private readonly IRegistryLoader<T> fallback;

        /// <summary>
        /// Constructs a NamedRegistry.
        /// </summary>
        /// <param name="fallback">A loader implementing the IRegistryLoader class. If no item under a requested name is registered, it will be loaded through this loader as fallback.</param>
        public NamedRegistry ( IRegistryLoader<T> fallback = null ) : base( )
        {
            this.nameLookup = new Dictionary<string, RegistryIndex<T>>( );
            this.names = new List<string>( );
            this.fallback = fallback;
        }

        /// <summary>
        /// Register the given item into the Registry.
        /// </summary>
        /// <param name="item">The item to register.</param>
        /// <returns>The index of the registered item.</returns>
        public new RegistryIndex<T> Register ( T item ) { return this.Register( item, "Null" ); }

        /// <summary>
        /// Register the given item into the Registry, under the given name.
        /// </summary>
        /// <param name="item">The item to register.</param>
        /// <param name="name">The name to register the item under.</param>
        /// <returns>The index of the registered item.</returns>
        public RegistryIndex<T> Register ( T item, string name )
        {
            RegistryIndex<T> index = new RegistryIndex<T>( ( ushort )this.Count );
            if ( nameLookup.ContainsKey( name ) ) throw new ArgumentException( typeof( T ).ToString( ) + " already registered under name " + name.ToString( ) );
            if ( base.indexLookup.ContainsKey( item ) ) throw new ArgumentException( item.GetType( ).ToString( ) + " " + name.ToString( ) + " is already registered under a different name" );

            this.registered.Add( item );
            this.names.Add( name );
            base.indexLookup.Add( item, index );
            this.nameLookup.Add( name, index );

            return index;
        }

        /// <summary>
        /// Get the name of a registered item.
        /// </summary>
        /// <param name="index">The RegistryIndex of the registered item.</param>
        /// <returns>The name of the registered item.</returns>
        public string GetName ( RegistryIndex<T> index )
        {
            return this.names[index.ID];
        }

        /// <summary>
        /// Get the RegistryIndex of an item by its name. 
        /// If no item is registered under this name and this Registry has a fallback loader, load the item by name.
        /// </summary>
        /// <param name="name">The name of the requested item.</param>
        /// <returns>The RegistryIndex of the requested item.</returns>
        public RegistryIndex<T> GetIndexByName ( string name )
        {
            RegistryIndex<T> index;
            if ( this.nameLookup.TryGetValue( name, out index ) )
            {
                return index;
            }
            else if ( fallback != null )
            {
                return this.Register( fallback.Load( name ), name );
            }
            else
            {
                throw new FormatException( "No " + typeof( T ).Name + " registered under the name " + name );
            }
        }
    }
}