using System.Collections.Generic;
using IIASA.WorldCereal.Rdm.Enums;

namespace IIASA.WorldCereal.Rdm.Core
{
    public static class SplitHelper
    {
        private static readonly Dictionary<string, SplitType> SplitMap;

        static SplitHelper()
        {
            SplitMap = GetSplitMap();
        }

        public static SplitType Get(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return SplitType.Cal;
            }
            var key = value.ToLowerInvariant();
            if (SplitMap.ContainsKey(key))
            {
                return SplitMap[key];
            }
            return SplitType.Cal;
        }

        public static bool IsValid(string value)
        {
            var key = value.ToLowerInvariant();
            if (SplitMap.ContainsKey(key))
            {
                return true;
            }

            return false;
        }

        private static Dictionary<string, SplitType> GetSplitMap()
        {
            var splitTypes = new Dictionary<string, SplitType>();
            splitTypes.Add("cal", SplitType.Cal);
            splitTypes.Add("val", SplitType.Val);
            splitTypes.Add("test", SplitType.Test);
            return splitTypes;
        }
    }
}