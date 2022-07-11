using System.Collections;

namespace Photon.Hive.Plugin
{
    public static class PropertyValueComparer
    {
        public static bool Compare(object lhs, object rhs)
        {
            if (lhs == null || rhs == null)
            {
                return lhs == rhs;
            }

            var lhsAsStr = lhs as string;
            var rhsAsStr = rhs as string;
            if (lhsAsStr != null && rhsAsStr != null)
            {
                return lhsAsStr == rhsAsStr;
            }

            var lhsAsIDic = lhs as IDictionary;
            var rhsAsIDic = rhs as IDictionary;
            if (lhsAsIDic != null && rhsAsIDic != null)
            {
                return CompareIDictionary(lhsAsIDic, rhsAsIDic);
            }

            var lhsAsEnum = lhs as ICollection;
            var rhsAsEnum = rhs as ICollection;

            if (lhsAsEnum == null || rhsAsEnum == null)
            {
                return lhs.Equals(rhs);
            }

            if (lhsAsEnum.Count != rhsAsEnum.Count)
            {
                return false;
            }

            return CompareIEnumerable(lhsAsEnum, rhsAsEnum);
        }

        private static bool CompareIDictionary(IDictionary lhsAsIDic, IDictionary rhsAsIDic)
        {
            if (lhsAsIDic.Count != rhsAsIDic.Count)
            {
                return false;
            }

            var enum1 = lhsAsIDic.GetEnumerator();
            var enum2 = rhsAsIDic.GetEnumerator();

            while (true)
            {
                var res = enum1.MoveNext();
                var res2 = enum2.MoveNext();
                if (res != res2)
                {
                    return false;
                }
                if (res == false)
                {
                    return true;
                }
                if (!Compare(enum1.Key, enum2.Key))
                {
                    return false;
                }
                if (!Compare(enum1.Value, enum2.Value))
                {
                    return false;
                }
            }
        }

        private static bool CompareIEnumerable(IEnumerable lhsAsEnum, IEnumerable rhsAsEnum)
        {
            var enum1 = lhsAsEnum.GetEnumerator();
            var enum2 = rhsAsEnum.GetEnumerator();

            while (true)
            {
                var res = enum1.MoveNext();
                var res2 = enum2.MoveNext();
                if (res != res2)
                {
                    return false;
                }
                if (res == false)
                {
                    return true;
                }
                if (!Compare(enum1.Current, enum2.Current))
                {
                    return false;
                }
            }
        }
    }
}