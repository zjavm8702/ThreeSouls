using UnityEngine;

namespace ThreeSouls.Global
{
    public static class StringConstants
    {
        public static readonly string LOADING_SCENE_NAME = "";
        public static readonly string UI_POPUP_PATCH_ROOT = "Prefabs/UI_Patch";
        public const string CHAR_TEXTURE_COLOR_V = "_MainColor";

        public static readonly string TAG_SPAWN_HERO = "HeroSpawn";
        public static readonly string TAG_SPAWN_MONSTER = "MonsterSpawn";
        public static readonly string TAG_CHARACTER_PARENT = "CharacterParent";
        public static readonly string TAG_FOLLOW_CAMERA = "FollowCamera";
        public static readonly string LAYERMASK_ENEMY_HERO = "Hero";
        public static readonly string LAYERMASK_ENEMY_MONSTER = "Monster";
    }
    
    public static class Constants
    {
        public static readonly Vector2 HERO_SPAWN_POS = new Vector2(-9f, -1.5f);
        public static readonly float RUNNING_DISTANCE = 2f;
        public static readonly float CIRCLE_RADIUS_HERO = 1f;
        public static readonly float CIRCLE_RADIUS_MONSTER = 4f;
        public static readonly int MONSTER_SPAWN_CELL_RADIUS = 1;
        public static readonly float ENCOUNTER_SIMULATION_STEP = 0.02f;
        public static readonly int ENCOUNTER_CELL_SEARCH_RADIUS = 5;
        public static readonly float PATH_ARRIVAL_DISTANCE = 0.03f;
        public static readonly float ATTACK_RANGE_TOLERANCE = 0.05f;
        public static readonly float DEAD_DESPAWN_FALLBACK_TIME = 2f;
        public static readonly float HERO_FOLLOW_START_DELAY = 0.2f;
        public static readonly float PROJECTILE_MOVE_SPEED = 5f;
        public static readonly float PROJECTILE_ARRIVAL_DISTANCE = 0.05f;
    }
}
