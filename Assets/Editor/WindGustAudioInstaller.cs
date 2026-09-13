using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Assigns the supplied WindGust clip to each existing ScriptedGustTrigger.
/// The trigger script already calls gustAudio.Play(); its serialized reference
/// was simply left empty in every playable level.
/// </summary>
public static class WindGustAudioInstaller
{
    private const string GustClipPath = "Assets/Audio/WindGust.mp3";

    private static readonly string[] LevelPaths =
    {
        "Assets/Scenes/Soulie_Level1.unity",
        "Assets/Scenes/Elliot_Level2.unity",
        "Assets/Scenes/Soulie_Level3.unity",
        "Assets/Scenes/Elliot_Level4.unity"
    };

    [MenuItem("Tools/Little Lamplighter/Install Wind Gust Audio")]
    public static void Install()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Wind gust audio", "Exit Play Mode before installing the gust sound.", "OK");
            return;
        }

        AudioClip gustClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GustClipPath);
        if (gustClip == null)
        {
            EditorUtility.DisplayDialog("Wind gust audio", "Could not load " + GustClipPath + ". Copy the complete patch Assets folder into the project and wait for import to finish.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        string returnScenePath = SceneManager.GetActiveScene().path;
        int installedTriggers = 0;

        try
        {
            foreach (string levelPath in LevelPaths)
            {
                Scene scene = EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
                int sceneTriggerCount = InstallForScene(scene, gustClip);
                installedTriggers += sceneTriggerCount;
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Wind gust audio installed on " + sceneTriggerCount + " trigger(s): " + levelPath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Wind gust audio", "Installation stopped. Check the Console for details.", "OK");
            return;
        }
        finally
        {
            if (!string.IsNullOrEmpty(returnScenePath))
            {
                EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
            }
        }

        EditorUtility.DisplayDialog("Wind gust audio", "Assigned WindGust.mp3 to " + installedTriggers + " wind trigger(s) across Levels 1–4.", "OK");
    }

    [MenuItem("Tools/Little Lamplighter/Validate Wind Gust Audio")]
    public static void Validate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        string returnScenePath = SceneManager.GetActiveScene().path;
        int triggerCount = 0;
        int failures = 0;

        try
        {
            foreach (string levelPath in LevelPaths)
            {
                EditorSceneManager.OpenScene(levelPath, OpenSceneMode.Single);
                ScriptedGustTrigger[] triggers = UnityEngine.Object.FindObjectsByType<ScriptedGustTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                triggerCount += triggers.Length;

                foreach (ScriptedGustTrigger trigger in triggers)
                {
                    AudioSource source = trigger.GetComponent<AudioSource>();
                    if (source == null || source.clip == null || source.clip.name != "WindGust")
                    {
                        failures++;
                        Debug.LogError("Wind gust audio validation failed for " + levelPath + ": " + trigger.name);
                    }
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(returnScenePath))
            {
                EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
            }
        }

        EditorUtility.DisplayDialog(
            "Wind gust audio",
            failures == 0
                ? "Validation PASS: " + triggerCount + " wind trigger(s) use WindGust.mp3."
                : "Validation found " + failures + " incomplete trigger(s). See the Console.",
            "OK");
    }

    private static int InstallForScene(Scene scene, AudioClip gustClip)
    {
        ScriptedGustTrigger[] triggers = UnityEngine.Object.FindObjectsByType<ScriptedGustTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (triggers.Length == 0)
        {
            throw new InvalidOperationException("No ScriptedGustTrigger components were found in " + scene.path + ".");
        }

        foreach (ScriptedGustTrigger trigger in triggers)
        {
            AudioSource source = trigger.GetComponent<AudioSource>();
            if (source == null)
            {
                source = trigger.gameObject.AddComponent<AudioSource>();
            }

            source.clip = gustClip;
            source.playOnAwake = false;
            source.loop = false;
            // WindGust.mp3 is gain-normalized in this patch. Keep its source
            // at unity volume so the sweep remains audible over level music.
            source.volume = 1f;
            source.pitch = 1f;
            source.spatialBlend = 0f;

            SerializedObject serializedTrigger = new SerializedObject(trigger);
            SerializedProperty gustAudio = serializedTrigger.FindProperty("gustAudio");
            if (gustAudio == null)
            {
                throw new InvalidOperationException("The gustAudio field was not found on " + trigger.name + ".");
            }
            gustAudio.objectReferenceValue = source;
            serializedTrigger.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(trigger.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return triggers.Length;
    }
}
