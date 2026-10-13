using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ThreeSouls.Utility
{
    public class AddressableDistinctItemComparer : IEqualityComparer<IResourceLocation>
    {
        public bool Equals(IResourceLocation x, IResourceLocation y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            return string.Equals(x.PrimaryKey, y.PrimaryKey, StringComparison.Ordinal) && x.ResourceType == y.ResourceType;
        }

        public int GetHashCode(IResourceLocation obj)
        {
            if (obj == null)
            {
                return 0;
            }

            return HashCode.Combine(obj.PrimaryKey, obj.ResourceType);
        }
    }

}
