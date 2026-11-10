using UnityEngine;

namespace DefaultNamespace
{
    [CreateAssetMenu(fileName = "Character Configuration", menuName = "Configuration/Character")]
    public class CharacterConfiguration : ScriptableObject
    {
        [field: SerializeField] 
        public float Move_Speed { get; private set; }
        
        [field: SerializeField] 
        public float Walk_Animation_Speed { get; private set; }
        
        [field: SerializeField] 
        public float Run_Speed { get; private set; }
        
        [field: SerializeField] 
        public float Run_Animation_Speed { get; private set; }
        
        [field: SerializeField] 
        public float Angular_Speed { get; private set; }
        
        [field: Space(18), SerializeField]
        public float Push_Force { get; private set; }
    }
}