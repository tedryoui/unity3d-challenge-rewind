using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    [CreateAssetMenu(fileName = "Object Scatterer Configuration", menuName = "Configuration/Object Scatterer")]
    public class ObjectScattererConfiguration : ScriptableObject
    {
        [field: SerializeField] public List<GameObject> Prefabs { get; private set; }
    }
}