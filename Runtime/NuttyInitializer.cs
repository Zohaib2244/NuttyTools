using System;
using System.Reflection;
using UnityEngine;

public class NuttyInitializer : MonoBehaviour
{
    [SerializeField] private E_InitializeMethod initializeMethod = E_InitializeMethod.Awake;
    private void Awake()
    {
        if (initializeMethod == E_InitializeMethod.Awake)
        {
            InitTaggedStaticClasses();
        }
        return;
    }
    void Start()
    {
        if (initializeMethod == E_InitializeMethod.Start)
        {
            InitTaggedStaticClasses();
        }
    }
    private void InitTaggedStaticClasses()
    {
        // Use reflection to find all classes with [InitializeOnStart] and call their Init() method
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            if (type.GetCustomAttribute<NuttyInitializationAttribute>() != null)
            {
                var initMethod = type.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
                if (initMethod != null && initMethod.GetParameters().Length == 0)
                {
                    try
                    {
                        initMethod.Invoke(null, null);
                        Debug.Log($"Initialized: {type.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to initialize {type.Name}: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Class {type.Name} has [InitializeOnStart] but no valid Init() method.");
                }
            }
        }
    }
}
public enum E_InitializeMethod
{
    Awake,
    Start
}
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class NuttyInitializationAttribute : Attribute{ }