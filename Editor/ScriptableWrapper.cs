using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS.Editor
{
    public class ScriptableWrapper : ScriptableObject
    {
        [SerializeReference]
        public IComponent data;
    }
}
