using System.Collections.Generic;

namespace GenerateAssets
{
    public static class BagExtensions
    {
        public static T Get<T>(this IDictionary<string, object> bag, string key)
        {
            return bag.ContainsKey(key) ? (T)bag[key] : default;
        }

        public static IDictionary<string, object> Set<T>(this IDictionary<string, object> bag, string key, T value)
        {
            bag[key] = value;
            return bag;
        }
    }
}
