using System;
using System.Collections.Generic;
using System.Linq;

public static class EnumUtils
{
    public static IEnumerable<T> GetValues<T>()
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }

    public static T FromString<T>(string s) where T : struct
    {
        if (!Enum.TryParse(s, out T result))
            throw new ArgumentException($"The given string {s} doesn't have a matching enum value");
        
        return result;
    }
}