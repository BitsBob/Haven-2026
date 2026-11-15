using UnityEngine;

public class EnemyVisualSet : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public EnemyType type;
        [Tooltip("The model (an FBX with a skinned mesh).")]
        public GameObject model;
        [Tooltip("The walk, run or fly loop. Leave empty to use the model's rest pose.")]
        public AnimationClip clip;
        [Tooltip("Turn the model if it doesn't face the way it moves.")]
        public float yawOffset;
        public float animationSpeed = 1f;
    }

    public Entry[] entries;
}
