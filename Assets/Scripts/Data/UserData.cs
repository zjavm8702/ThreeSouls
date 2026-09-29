using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreeSouls.Data
{
    public class UserData
    {
        //private readonly Dictionary<int, HeroData> HeroDataContainer = new();
        
        //public int[] GetHeroIndexList()
        //{
        //    return HeroDataContainer.Select(x => x.Key).ToArray();
        //}

        //public HeroData GetHeroData(int index)
        //{
        //    if (HeroDataContainer.TryGetValue(index, out var data))
        //    {
        //        return data;
        //    }
            
        //    Debug.LogError($"Not found key [Key index : <color=red>{index}</color>]");
        //    return null;
        //}

        ///// <summary>
        ///// Hero 등록
        ///// </summary>
        ///// <param name="index">TableIndex</param>
        //public void SetHero(int index)
        //{
        //    if (HeroDataContainer.ContainsKey(index) == true)
        //    {
        //        return;
        //    }
            
        //    HeroDataContainer.Add(index, new HeroData());            
        //}

        //public void SetHeroLevel(int index, int lv)
        //{
        //    if (HeroDataContainer.ContainsKey(index) == false)
        //    {
        //        return;
        //    }

        //    HeroDataContainer[index].CharLevel = lv;
        //}
    }
}
