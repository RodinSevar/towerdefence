using UnityEngine;

/// <summary>
/// Base class for scene-level manager singletons. Duplicates destroy themselves.
/// Put any extra Awake work in <see cref="OnSingletonAwake"/> (only runs on the surviving instance).
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = (T)this;
        OnSingletonAwake();
    }

    protected virtual void OnSingletonAwake() { }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
