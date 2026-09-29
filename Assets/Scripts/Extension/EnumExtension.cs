using System;

namespace ThreeSouls.Extension
{
    public static class EnumExtension 
    {
        public static int ToInt(this Enum @this)
        {
            return Convert.ToInt32(@this);
        }
    }
}
