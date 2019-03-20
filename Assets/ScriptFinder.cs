using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ScriptFinder : EditorWindow
{
    private static readonly string TITLE = "ScriptFinder";
    private static readonly Vector2 m_MinSize = new Vector2(500, 200);

    private static ScriptFinder Instance = null;
    private string m_SearchString;
    private static List<string> m_LocalPaths = new List<string>();
    private static List<string> m_RemotePaths = new List<string>();
    private static int m_LocalPathsCount;
    private static int m_RemotePathsCount;
    private bool m_Focused = false;
    private int m_CurrentTab = 0;
    private Vector2 m_CurrentScrollPos;
    private List<KeyValuePair<string, string>> m_Results = new List<KeyValuePair<string, string>>();

    [MenuItem("Tools/ScriptFinder #t")]
    public static void OpenWindow()
    {
        if (Instance != null)
        {
            return;
        }

        Instance = EditorWindow.GetWindow<ScriptFinder>(true, TITLE, true);
        LoadData();
        Instance.minSize = m_MinSize;
        Rect position = Instance.position;
        position.center = new Vector2(Screen.currentResolution.width / 2f, Screen.currentResolution.height / 2f);
        Instance.position = position;
        Instance.Show();

    }

    private static void LoadData(bool forceReload = false)
    {

        if (m_LocalPaths.Count == 0 && m_RemotePaths.Count == 0 || forceReload)
        {
            m_LocalPaths.Clear();
            m_RemotePaths.Clear();
            m_RemotePathsCount = 0;
            m_LocalPathsCount = 0;

            string localPathArray = null;
            string remotePathArray = null;

            if (EditorPrefs.HasKey("ScriptFinderLocalPaths"))
            {
                localPathArray = EditorPrefs.GetString("ScriptFinderLocalPaths");
                string[] localPaths = localPathArray.Split('|').Where(x => !string.IsNullOrEmpty(x)).ToArray();
                m_LocalPaths = new List<string>(localPaths);
                m_LocalPathsCount = m_LocalPaths.Count;
            }

            if (EditorPrefs.HasKey("ScriptFinderRemotePaths"))
            {
                remotePathArray = EditorPrefs.GetString("ScriptFinderRemotePaths");
                string[] remotePaths = remotePathArray.Split('|').Where(x => !string.IsNullOrEmpty(x)).ToArray();
                m_RemotePaths = new List<string>(remotePaths);
                m_RemotePathsCount = m_RemotePaths.Count;
            }
        }
    }

    void OnGUI()
    {
        m_CurrentTab = GUILayout.Toolbar(m_CurrentTab, new string[] { "Search", "Preferences"});
        EditorGUILayout.Space();
        m_CurrentScrollPos = EditorGUILayout.BeginScrollView(m_CurrentScrollPos, GUILayout.Width(position.width), GUILayout.Height(position.height));
        switch (m_CurrentTab)
        {
            case 0:
                {
                    ShowSearchTab();
                    break;
                }
            case 1:
                {
                    ShowPreferencesTab();
                    break;
                }
        }
        EditorGUILayout.EndScrollView();
    }

    private void OnLostFocus()
    {
        Instance.Close();
        m_Focused = false;
    }

    private void ShowSearchTab()
    {
        //Search bar
        EditorGUILayout.BeginHorizontal();

        GUI.SetNextControlName("textfield");

        m_SearchString = EditorGUILayout.TextField(m_SearchString, GUILayout.Height(20));

        if (!m_Focused)
        {
            EditorGUI.FocusTextInControl("textfield");
            m_Focused = true;
        }

        if (GUILayout.Button("Search", GUILayout.Width(60), GUILayout.Height(20)))
        {
            Search();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        //Search Results
        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = oldIndent + 10;

        if (m_Results.Count > 0)
        {
            foreach (KeyValuePair<string, string> pair in m_Results)
            {
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(pair.Value, GUILayout.Height(30));
                if (GUILayout.Button("Import", GUILayout.Width(60), GUILayout.Height(20)))
                {
                    ImportFile(pair.Key);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUI.indentLevel = oldIndent;


        //Enter event
        if (Event.current != null && Event.current.isKey && Event.current.keyCode == KeyCode.Return)
        {
            Search();
        }
    }

    private void ShowPreferencesTab()
    {
        EditorGUILayout.LabelField("Local Paths");
        m_LocalPathsCount = EditorGUILayout.IntField(m_LocalPathsCount, GUILayout.Width(30f));
        for (int i = 0; i < Mathf.Abs(m_LocalPathsCount - m_LocalPaths.Count); i++)
        {
            if (m_LocalPathsCount > m_LocalPaths.Count)
            {
                m_LocalPaths.Add("");
            }
            else
            {
                m_LocalPaths.RemoveAt(m_LocalPaths.Count - 1);
            }
        }
        for (int i = 0; i < m_LocalPaths.Count; i++)
        {
            m_LocalPaths[i] = EditorGUILayout.TextField(m_LocalPaths[i], GUILayout.Height(20));
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Remote Paths");
        m_RemotePathsCount = EditorGUILayout.IntField(m_RemotePathsCount, GUILayout.Width(30f));
        for (int i = 0; i < Mathf.Abs(m_RemotePathsCount - m_RemotePaths.Count); i++)
        {
            if (m_RemotePathsCount > m_RemotePaths.Count)
            {
                m_RemotePaths.Add("");
            }
            else
            {
                m_RemotePaths.RemoveAt(m_RemotePaths.Count - 1);
            }
        }
        for (int i = 0; i < m_RemotePaths.Count; i++)
        {
            m_RemotePaths[i] = EditorGUILayout.TextField(m_RemotePaths[i], GUILayout.Height(20));
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Save"))
        {
            string localPathArray = "";
            for (int i = 0; i < m_LocalPaths.Count; i++)
            {
                if (string.IsNullOrEmpty(m_LocalPaths[i])) continue;
                localPathArray += m_LocalPaths[i] + "|";
            }
            if(!string.IsNullOrEmpty(localPathArray))
            {
                localPathArray = localPathArray.Remove(localPathArray.Length - 1);
            }

            string remotePathArray = "";
            for (int i = 0; i < m_RemotePaths.Count; i++)
            {
                if (string.IsNullOrEmpty(m_RemotePaths[i])) continue;
                remotePathArray += m_RemotePaths[i] + "|";
            }
            if (!string.IsNullOrEmpty(remotePathArray))
            {
                remotePathArray = remotePathArray.Remove(remotePathArray.Length - 1);
            }

            EditorPrefs.SetString("ScriptFinderLocalPaths", localPathArray);
            EditorPrefs.SetString("ScriptFinderRemotePaths", remotePathArray);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Reload"))
        {
            LoadData(true);
            Repaint();
        }
    }

    private void Search()
    {
        m_Results.Clear();
        //Local
        for (int i = 0; i < m_LocalPaths.Count; i++)
        {
            if (!Directory.Exists(m_LocalPaths[i])) continue;
            string[] files = Directory.GetFiles(m_LocalPaths[i], $"*{m_SearchString}*.cs",SearchOption.AllDirectories);
            for (int j = 0; j < files.Length; j++)
            {
                m_Results.Add(new KeyValuePair<string, string>(files[j], Path.GetFileName(files[j])));
            }
        }

        //Remote
        for (int i = 0; i < m_RemotePaths.Count; i++)
        {
            if (!Directory.Exists(m_LocalPaths[i])) continue;
            string[] files = Directory.GetFiles(m_RemotePaths[i], $"*{m_SearchString}*.cs", SearchOption.AllDirectories);
            for (int j = 0; j < files.Length; j++)
            {
                m_Results.Add(new KeyValuePair<string, string>(files[j], Path.GetFileName(files[j])));
            }
        }

        Repaint();
    }

    private void ImportFile(string file)
    {
        string folderToImport = Path.Combine(Directory.GetCurrentDirectory(), "Assets","Scripts");
        if (!Directory.Exists(folderToImport)) Directory.CreateDirectory(folderToImport);
        FileUtil.CopyFileOrDirectory(file, folderToImport);
    }

}

//  Copyright (c) 2016-2017 amlovey
public class QuickOpener : Editor
{
    [MenuItem("Tools/Quick Folder Opener/Application.dataPath", false, 100)]
    private static void OpenDataPath()
    {
        Reveal(Application.dataPath);
    }

    [MenuItem("Tools/Quick Folder Opener/Application.persistentDataPath", false, 100)]
    private static void OpenPersistentDataPath()
    {
        Reveal(Application.persistentDataPath);
    }

    [MenuItem("Tools/Quick Folder Opener/Application.streamingAssetsPath", false, 100)]
    private static void OpenStreamingAssets()
    {
        Reveal(Application.streamingAssetsPath);
    }

    [MenuItem("Tools/Quick Folder Opener/Application.temporaryCachePath", false, 100)]
    private static void OpenCachePath()
    {
        Reveal(Application.temporaryCachePath);
    }

    // http://docs.unity3d.com/ScriptReference/MenuItem-ctor.html
    //
    [MenuItem("Tools/Quick Folder Opener/Asset Store Packages Folder", false, 111)]
    private static void OpenAssetStorePackagesFolder()
    {
        //http://answers.unity3d.com/questions/45050/where-unity-store-saves-the-packages.html
        //
#if UNITY_EDITOR_OSX
            string path = GetAssetStorePackagesPathOnMac();
#elif UNITY_EDITOR_WIN
        string path = GetAssetStorePackagesPathOnWindows();
#endif

        Reveal(path);
    }

    [MenuItem("Tools/Quick Folder Opener/Editor Application Path")]
    private static void OpenUnityEditorPath()
    {
        Reveal(new FileInfo(EditorApplication.applicationPath).Directory.FullName);
    }

    [MenuItem("Tools/Quick Folder Opener/Editor Log Folder")]
    private static void OpenEditorLogFolderPath()
    {
#if UNITY_EDITOR_OSX
			string rootFolderPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
			var libraryPath = Path.Combine(rootFolderPath, "Library");
			var logsFolder = Path.Combine(libraryPath, "Logs"); 
			var UnityFolder = Path.Combine(logsFolder, "Unity");
			Reveal(UnityFolder);
#elif UNITY_EDITOR_WIN
        var rootFolderPath = System.Environment.ExpandEnvironmentVariables("%localappdata%");
        var unityFolder = Path.Combine(rootFolderPath, "Unity");
        Reveal(Path.Combine(unityFolder, "Editor"));
#endif
    }

    [MenuItem("Tools/Quick Folder Opener/Asset Backup Folder", false, 122)]
    public static void OpenAEBackupFolder()
    {
        var folder = Path.Combine(Application.persistentDataPath, "AEBackup");
        Directory.CreateDirectory(folder);
        Reveal(folder);
    }

    private const string ASSET_STORE_FOLDER_NAME = "Asset Store-5.x";
    private static string GetAssetStorePackagesPathOnMac()
    {
        var rootFolderPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        var libraryPath = Path.Combine(rootFolderPath, "Library");
        var unityFolder = Path.Combine(libraryPath, "Unity");
        return Path.Combine(unityFolder, ASSET_STORE_FOLDER_NAME);
    }

    private static string GetAssetStorePackagesPathOnWindows()
    {
        var rootFolderPath = System.Environment.ExpandEnvironmentVariables("%appdata%");
        var unityFolder = Path.Combine(rootFolderPath, "Unity");
        return Path.Combine(unityFolder, ASSET_STORE_FOLDER_NAME);
    }

    public static void Reveal(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning(string.Format("Folder '{0}' is not Exists", folderPath));
            return;
        }

        EditorUtility.RevealInFinder(folderPath);
    }
}


