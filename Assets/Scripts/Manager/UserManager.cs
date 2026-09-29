using ThreeSouls.Interface;
using ThreeSouls.Data;
using UnityEngine;

namespace ThreeSouls.Manager
{
    public class UserManager : MonoBehaviour, IManager
    {
        public static UserManager Get()
        {
            return GameManager.Instance.Get<UserManager>();
        }
        
        public readonly UserData UData = new();
        
        public void Initialize()
        {
            SetBaseData();
        }

        public void Release()
        {
            
        }

        public void OnSceneChanged()
        {
            
        }
        
        private void SetBaseData()
        {
            //UData.SetHero(1);
        }
    }
}
