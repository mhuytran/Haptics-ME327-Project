using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneRuntimeUnpauser : MonoBehaviour
{
    [Header("debug")]
    public bool printUnpauseLog = true;

    void Awake()
    {
        ForceUnpause("Awake");
    }

    void OnEnable()
    {
        ForceUnpause("OnEnable");
    }

    void Start()
    {
        ForceUnpause("Start");
        StartCoroutine(ForceUnpauseForFirstSecond());
    }

    IEnumerator ForceUnpauseForFirstSecond()
    {
        float elapsed = 0f;

        while (elapsed < 1.0f)
        {
            ForceUnpause("startup guard");

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void ForceUnpause(string source)
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        if (EditorApplication.isPaused)
        {
            EditorApplication.isPaused = false;
        }
#endif

        if (printUnpauseLog)
        {
            Debug.Log("Runtime unpaused from " + source);
        }
    }
}