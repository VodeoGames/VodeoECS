using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using UnityEditor;

namespace VodeoECS.Editor
{
    public class EntityView : EditorWindow
    {
        private Entity selectedEntity;

        private Dictionary<string, bool> expandedDict;
        private Dictionary<string, Vector2> scrollDict;

        [MenuItem("ECS/Entity View")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(EntityView));
        }

        public EntityView()
        {
            expandedDict = new Dictionary<string, bool>( );
            scrollDict = new Dictionary<string, Vector2>( );
        }

        public static object GetTargetObjectOfProperty ( SerializedProperty prop )
        {
            if ( prop == null ) return null;

            var path = prop.propertyPath.Replace( ".Array.data[", "[" );
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split( '.' );
            foreach ( var element in elements )
            {
                if ( element.Contains( "[" ) )
                {
                    var elementName = element.Substring( 0, element.IndexOf( "[" ) );
                    var index = System.Convert.ToInt32( element.Substring( element.IndexOf( "[" ) ).Replace( "[", "" ).Replace( "]", "" ) );
                    obj = GetValue_Imp( obj, elementName, index );
                }
                else
                {
                    obj = GetValue_Imp( obj, element );
                }
            }
            return obj;
        }

        private void OnSelectionChange ( )
        {
            World world = World.singleton;

            if ( world == null || !world.Initialized )
            {
                return;
            }
            else
            {
                if ( Selection.activeTransform != null )
                {
                    selectedEntity = world.objectRenderSystem.getObjectEntity( Selection.activeTransform );
                    this.Repaint( );
                }
            }
        }

        private void OnFocus ( )
        {
            this.OnSelectionChange( );
        }

        private void OnGUI ( )
        {
            World world = World.singleton;

            GUIStyle offset = new GUIStyle( );
            offset.padding = new RectOffset(20,0,0,0);

            if (world == null || !world.Initialized)
            {
                GUILayout.Label("ECS System not running", EditorStyles.boldLabel);
            }
            else
            {
                int entityID = EditorGUILayout.IntField("Entity ID", selectedEntity.ID);
                selectedEntity = new Entity(entityID, selectedEntity.prototype);
                if (!world.HasEntity(selectedEntity)) selectedEntity = new Entity(entityID, !selectedEntity.prototype);

                if (world.HasEntity(selectedEntity))
                {
                    if (selectedEntity.prototype)
                    {
                        EditorGUILayout.LabelField("Prototype: " + world.Prototypes.GetName(selectedEntity));
                    }
                    HashSet<RegistryIndex<Type>> types = world.GetTypes(selectedEntity);

                    foreach (RegistryIndex<Type> type in types)
                    {
                        IComponentPool pool = world.GetComponentPoolDynamic(type);

                        Type poolType = pool.GetType();

                        if (poolType.GetGenericTypeDefinition() == typeof(DataComponentPool<>))
                        {
                            PropertyInfo indexerProperty = poolType.GetProperty("Item", new Type[] { typeof(Entity) });
                            object accessor = indexerProperty.GetValue(pool, new object[] { selectedEntity });
                            IDataComponent component = (IDataComponent)accessor.GetType().GetProperty("Value").GetValue(accessor);

                            ScriptableWrapper wrapper = CreateInstance<ScriptableWrapper>();
                            wrapper.data = component;
                            SerializedObject serialized = new SerializedObject(wrapper);
                            SerializedProperty property = serialized.FindProperty("data");
                            EditorGUILayout.PropertyField(property, new GUIContent(poolType.GenericTypeArguments[0].Name), true);

                            serialized.ApplyModifiedProperties();
                            accessor.GetType().GetMethod("Write", new Type[] { component.GetType() }).Invoke(accessor, new object[] { component });
                        }
                        else if (poolType.GetGenericTypeDefinition() == typeof(FilterComponentPool<>))
                        {
                            PropertyInfo indexerProperty = poolType.GetProperty("Item", new Type[] { typeof(Entity) });
                            IFilterComponent component = (IFilterComponent)indexerProperty.GetValue(pool, new object[] { selectedEntity });

                            ScriptableWrapper wrapper = CreateInstance<ScriptableWrapper>();
                            wrapper.data = component;
                            SerializedObject serialized = new SerializedObject(wrapper);
                            SerializedProperty property = serialized.FindProperty("data");
                            EditorGUILayout.PropertyField(property, new GUIContent(poolType.GenericTypeArguments[0].Name), true);

                            serialized.ApplyModifiedProperties();
                            poolType.GetMethod("SetFilter", new Type[] { typeof(Entity), component.GetType() }).Invoke(pool, new object[] { selectedEntity, component });
                        }
                        else if (poolType.GetGenericTypeDefinition() == typeof(ListComponentPool<>))
                        {
                            PropertyInfo indexerProperty = poolType.GetProperty( "Item", new Type[] { typeof( Entity ) } );
                            PropertyInfo lengthProperty = indexerProperty.PropertyType.GetProperty( "Length" );
                            object accessor = indexerProperty.GetValue( pool, new object[] { selectedEntity } );
                            int length = (int)lengthProperty.GetValue( accessor );
                            if (length > 0)
                            {
                                PropertyInfo indexerProperty2 = indexerProperty.PropertyType.GetProperty( "Item", new Type[] { typeof( int ) } );

                                EditorGUILayout.BeginFoldoutHeaderGroup( true, poolType.GenericTypeArguments[0].Name+" list ("+length+")");

                                Vector2 scroll = new Vector2();
                                if (!scrollDict.TryGetValue( poolType.GenericTypeArguments[0].Name, out scroll ))
                                {
                                    scrollDict.Add( poolType.GenericTypeArguments[0].Name, scroll );
                                }

                                scroll = EditorGUILayout.BeginScrollView( scroll, offset, GUILayout.MaxHeight(100) );
                                for ( int i = 0; i < length; i++ )
                                {
                                    IElementComponent component = ( IElementComponent )indexerProperty2.GetValue(
                                        accessor,
                                        new object[] { i } );

                                    ScriptableWrapper wrapper = CreateInstance<ScriptableWrapper>( );
                                    wrapper.data = component;
                                    SerializedObject serialized = new SerializedObject( wrapper );
                                    SerializedProperty property = serialized.FindProperty( "data" );

                                    EditorGUILayout.PropertyField( property, new GUIContent( poolType.GenericTypeArguments[0].Name ), true );

                                    serialized.ApplyModifiedProperties( );
                                    indexerProperty.PropertyType.GetMethod( "WriteElement", new Type[] { typeof( int ), component.GetType( ) } ).Invoke( accessor, new object[] { i, component } );
                                }
                                EditorGUILayout.EndScrollView( );
                                scrollDict[poolType.GenericTypeArguments[0].Name] = scroll;
                                EditorGUILayout.EndFoldoutHeaderGroup( );
                            }
                        }
                        else
                        {
                            throw new Exception("Unknown pool type");
                        }
                    }
;                }
                else
                {
                    GUILayout.Label("No Entity with this ID", EditorStyles.boldLabel);
                }
            }
        }

        private void OnInspectorUpdate ( )
        {
            this.Repaint( );
        }

        private static object GetValue_Imp ( object source, string name, int index )
        {
            var enumerable = GetValue_Imp( source, name ) as System.Collections.IEnumerable;
            if ( enumerable == null ) return null;
            var enm = enumerable.GetEnumerator( );
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for ( int i = 0; i <= index; i++ )
            {
                if ( !enm.MoveNext( ) ) return null;
            }
            return enm.Current;
        }

        private static object GetValue_Imp ( object source, string name )
        {
            if ( source == null )
                return null;
            var type = source.GetType( );

            while ( type != null )
            {
                var f = type.GetField( name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance );
                if ( f != null )
                    return f.GetValue( source );

                var p = type.GetProperty( name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase );
                if ( p != null )
                    return p.GetValue( source, null );

                type = type.BaseType;
            }
            return null;
        }
    }
}
