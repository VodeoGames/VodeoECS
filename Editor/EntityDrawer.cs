using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;


namespace VodeoECS.Editor
{
    [CustomPropertyDrawer( typeof( Entity ) )]
    public class EntityDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI ( Rect position, SerializedProperty property, GUIContent label )
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty( position, label, property );

            // Draw label
            position = EditorGUI.PrefixLabel( position, GUIUtility.GetControlID( FocusType.Passive ), label );

            // Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Calculate rects
            var idRect = new Rect( position.x, position.y, 200, position.height );
            var protoRect = new Rect( position.x + 35, position.y, 200, position.height ); 

            // Draw fields - passs GUIContent.none to each so they are drawn without labels
            Entity entity = (Entity)EntityView.GetTargetObjectOfProperty(property);
            EditorGUI.IntField( idRect, entity.ID);
            if ( entity.prototype ) EditorGUI.LabelField( protoRect, "(Prototype)");

            // Set indent back to what it was
            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty( );
        }
    }
}
