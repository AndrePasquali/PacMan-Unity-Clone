using Aquiris.PacMan.Level.Item.Fruits;
using UnityEngine;

namespace Aquiris.PacMan.Level
{
    [CreateAssetMenu(fileName = "Level", menuName = "Create Level", order = 0)]
    public class Level: ScriptableObject
    {
        public int ID;

        public Fruit Fruit;

        public float GhostBlueTime;

        public float FlashesBeforeBlueTimeEnds;

        public bool AnimationAfterFinish;
    }
}