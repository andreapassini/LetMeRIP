using UnityEngine;
using System.IO;
using System.Reflection;

public static class LocalJSONHandler
{

    /**
     * returns a readonly instance of T, unmarshalized from a json file at found at path,
     * Any changes made to the returned container will not be saved.
     * Use this if you have to read multiple fields at once.
     */
    public static T GetContainer<T>(string path = null) where T : new()
    {
        path ??= typeof(T).Name;
        T container = new T();
        string json = ReadJson<T>(path);
        JsonUtility.FromJsonOverwrite(json, container);

        return container;
    }

    /**
     * reads a json file at a given path, assuming that is used to represent an instance of T
     * if such file is not found, an empty marshallized instance of T is returned
     */
    private static string ReadJson<T>(string path) where T : new()
    {
        if (path == null) return "";
        string fullPath = Application.persistentDataPath + "/" + path + ".json";
        try
        {
            return File.ReadAllText(fullPath);
        }
        catch
        {
            // if setting file is not found create a new instance of it and save it
            T emptyContainer = new T();
            return JsonUtility.ToJson(emptyContainer);
        }
    }

    /**
     * let T be the type of a marshalized T instance, saved on a local file
     * let key be the name of a field (not a property) of T
     * returns the value of the field key of the locally saved instance
     */
    public static object ReadValue<T>(string key, string path = null) where T : new()
    {
        if (key == null) return null;

        path ??= typeof(T).Name;
        T container = GetContainer<T>(path);
        return container.GetType().GetField(key).GetValue(container);
    }

    /**
     * let T be serializable, marshallized (converted to JSON) and saved on a local file
     * let key be the name of a field (not a property) of T
     * and let value be of the same type of the field key
     * 
     * overwrites the value of field key of the locally saved instance and saves it again
     */
    public static void SetValue<T>(string key, object value, string path = null) where T : new()
    {
        if (key == null || value == null) return;
        path ??= typeof(T).Name;

        T container = GetContainer<T>(path);
        typeof(T).GetField(key).SetValue(container, value);
        SaveToJSON(container, path);
    }

    /**
     * overwrites the current saved container with newContainer if not null
     */
    public static void OverwriteContainer<T>(T newContainer, string path = null)
    {
        if (newContainer == null) return;
        SaveToJSON<T>(newContainer, path ?? typeof(T).Name);
    }

    /**
     * Coverts container (assuming that is serializable) to JSON (Marshalling)
     * and saves it to a local path
     */
    private static void SaveToJSON<T>(T container, string path)
    {
        if (path == null || container == null) return;
        string fullPath = $"{Application.persistentDataPath}/{path}.json";
        File.WriteAllText(fullPath, JsonUtility.ToJson(container));
    }

    private static void PrintFields<T>(T container)
    {
        if (container == null) { Debug.Log("Container's null"); return; }
        string rep = "";
        foreach (FieldInfo fi in container.GetType().GetFields())
            rep += $"{fi.Name}: {fi.GetValue(container)}, ";
        Debug.Log(rep);
    }
}