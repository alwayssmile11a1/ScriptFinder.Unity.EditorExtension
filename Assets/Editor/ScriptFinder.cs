using Octokit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace CoheeCreative
{

    public class ScriptFinder : EditorWindow
    {
        private static readonly string TITLE = "ScriptFinder";
        private static readonly Vector2 m_MinSize = new Vector2(500, 250);

        private static ScriptFinder Instance = null;
        private string m_SearchString;
        private static string m_DefaultImportFolder;
        private static List<string> m_LocalPaths = new List<string>();
        private static List<string> m_RemotePaths = new List<string>();
        private static int m_LocalPathsCount;
        private static int m_RemotePathsCount;
        private bool m_Focused = false;
        private int m_CurrentTab = 0;
        private Vector2 m_CurrentScrollPos1;
        private Vector2 m_CurrentScrollPos2;
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

                if (EditorPrefs.HasKey("ScriptFinderDefaultImportFolder"))
                {
                    m_DefaultImportFolder = EditorPrefs.GetString("ScriptFinderDefaultImportFolder");
                }
                else
                {
                    m_DefaultImportFolder = "Scripts";
                }

            }
        }

        void OnGUI()
        {
            m_CurrentTab = GUILayout.Toolbar(m_CurrentTab, new string[] { "Search", "Preferences" });
            EditorGUILayout.Space();
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

            //Enter event
            if (Event.current != null && Event.current.isKey && Event.current.keyCode == KeyCode.Escape)
            {
                Quit();
            }

        }

        private void OnLostFocus()
        {
            Quit();
        }

        private void Quit()
        {
            Instance.Close();
            Instance = null;
            m_Focused = false;
        }

        private void ShowSearchTab()
        {
            m_CurrentScrollPos1 = EditorGUILayout.BeginScrollView(m_CurrentScrollPos1, GUILayout.Width(position.width), GUILayout.Height(position.height));

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
                    GUILayout.Label(pair.Key, GUILayout.Height(30));
                    if (GUILayout.Button("Import", GUILayout.Width(60), GUILayout.Height(20)))
                    {
                        ImportFile(pair.Value, pair.Key);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel = oldIndent;


            EditorGUILayout.EndScrollView();

            //Enter event
            if (Event.current != null && Event.current.isKey && Event.current.keyCode == KeyCode.Return)
            {
                Search();
            }
        }

        private void ShowPreferencesTab()
        {

            m_CurrentScrollPos2 = EditorGUILayout.BeginScrollView(m_CurrentScrollPos2, GUILayout.Width(position.width), GUILayout.Height(position.height));

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

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Default Import Folder: ");
            m_DefaultImportFolder = EditorGUILayout.TextField(m_DefaultImportFolder, GUILayout.Height(20));

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(30)))
            {
                string localPathArray = "";
                for (int i = 0; i < m_LocalPaths.Count; i++)
                {
                    if (string.IsNullOrEmpty(m_LocalPaths[i])) continue;
                    localPathArray += m_LocalPaths[i] + "|";
                }
                if (!string.IsNullOrEmpty(localPathArray))
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
                EditorPrefs.SetString("ScriptFinderDefaultImportFolder", m_DefaultImportFolder);
            }

            if (GUILayout.Button("Reload", GUILayout.Height(30)))
            {
                LoadData(true);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();

        }

        private void Search()
        {
            m_Results.Clear();
            //Local
            for (int i = 0; i < m_LocalPaths.Count; i++)
            {
                if (!Directory.Exists(m_LocalPaths[i])) continue;
                string[] files = Directory.GetFiles(m_LocalPaths[i], $"*{m_SearchString}*.cs", SearchOption.AllDirectories);
                for (int j = 0; j < files.Length; j++)
                {
                    m_Results.Add(new KeyValuePair<string, string>(files[j], Path.GetFileName(files[j])));
                }
            }

            //Remote
            for (int i = 0; i < m_RemotePaths.Count; i++)
            {
                GetRemoteFiles(m_RemotePaths[i]);
                //if (!Directory.Exists(m_RemotePaths[i])) continue;
                //string[] files = Directory.GetFiles(m_RemotePaths[i], $"*{m_SearchString}*.cs", SearchOption.AllDirectories);
                //for (int j = 0; j < files.Length; j++)
                //{
                //    m_Results.Add(new KeyValuePair<string, string>(files[j], Path.GetFileName(files[j])));
                //}
            }

            Repaint();
        }

        //private IEnumerator GetRemoteFiles(string url)
        //{
        //    using (UnityWebRequest www = UnityWebRequest.Get(url))
        //    {
        //        yield return www.SendWebRequest();
        //        if (www.isNetworkError || www.isHttpError)
        //        {
        //            Debug.LogWarning(www.error);
        //        }
        //        else
        //        {
        //            string result = www.downloadHandler.text;
        //            Debug.Log(result);

        //        }
        //    }
        //}

        private void GetRemoteFiles(string url)
        {
            Task.Factory.StartNew(async () =>
            {
                var repoOwner = "crs2007";
                var repoName = "ActiveReport";
                var path = "ActiveReport";

                var octokitResults = await ListContentsOctokit(repoOwner, repoName, path);
                PrintResults("From Octokit", octokitResults);

            }).Wait();
        }

        static async Task<IEnumerable<string>> ListContentsOctokit(string repoOwner, string repoName, string path)
        {
            var client = new GitHubClient(new ProductHeaderValue("Cohee-Creative"));
            //var basicAuth = new Credentials("username", "password");
            //client.Credentials = basicAuth;
            var contents = await client.Repository.Content.GetAllContents(repoOwner, repoName);
            return contents.Select(content => content.Name);
        }

        static void PrintResults(string source, IEnumerable<string> files)
        {
            Debug.Log(source);
            foreach (var file in files)
            {
                Debug.Log($" -{file}");
            }
        }

        private void ImportFile(string fileName, string fullPath)
        {
            string folderToImport = Path.Combine(Directory.GetCurrentDirectory(), "Assets", m_DefaultImportFolder);
            if (!Directory.Exists(folderToImport)) AssetDatabase.CreateFolder("Assets", m_DefaultImportFolder);
            File.Copy(fullPath, Path.Combine(folderToImport, fileName));
            AssetDatabase.ImportAsset(Path.Combine("Assets", m_DefaultImportFolder, fileName));
        }
    }

    /// <summary>
    /// Gives the possibility to use coroutines in editorscripts.
    /// from https://github.com/FelixEngl/EditorCoroutines/blob/master/Editor/EditorCoroutine.cs
    /// </summary>
    public class EditorCoroutine
    {

        //The given coroutine
        //
        readonly IEnumerator routine;

        //The subroutine of the given coroutine
        private IEnumerator internalRoutine;

        //Constructor
        EditorCoroutine(IEnumerator routine)
        {
            this.routine = routine;
        }


        #region static functions
        /// <summary>
        /// Starts a new EditorCoroutine.
        /// </summary>
        /// <param name="routine">Coroutine</param>
        /// <returns>new EditorCoroutine</returns>
        public static EditorCoroutine StartCoroutine(IEnumerator routine)
        {
            EditorCoroutine coroutine = new EditorCoroutine(routine);
            coroutine.Start();
            return coroutine;
        }

        /// <summary>
        /// Clears the EditorApplication.update delegate by setting it null
        /// </summary>
        public static void ClearEditorUpdate()
        {
            EditorApplication.update = null;
        }

        #endregion



        //Delegate to EditorUpdate
        void Start()
        {
            EditorApplication.update += Update;
        }

        //Undelegate
        public void Stop()
        {
            if (EditorApplication.update != null)
                EditorApplication.update -= Update;

        }

        //Updatefunction
        void Update()
        {

            //if the internal routine is null
            if (internalRoutine == null)
            {
                //if given routine doesn't continue
                if (!routine.MoveNext())
                {
                    Stop();
                }
            }

            if (internalRoutine != null)
            {
                if (!internalRoutine.MoveNext())
                {
                    internalRoutine = null;
                }
                if (internalRoutine.Current != null && (bool)internalRoutine.Current)
                {
                    internalRoutine = null;
                }
            }
        }

        ////IEnumerator for a EditorYieldInstruction, false if EditorYieldInstruction is false, else true and leaving
        //private IEnumerator isTrue(EditorYieldInstruction editorYieldInstruction)
        //{
        //    while (!editorYieldInstruction.IsDone)
        //    {
        //        yield return false;
        //    }
        //    yield return true;
        //}
    }

    ///// <summary>
    ///// Abstract Class for a EditorYieldInstruction.
    ///// Be careful with the abstract function: <see cref="InternalLogic"/>
    ///// </summary>
    //public abstract class EditorYieldInstruction
    //{
    //    //EditorYieldInstruction done?
    //    private bool isDone = false;

    //    //internal logik routine of the EditorYieldInstruction
    //    readonly IEnumerator routine;

    //    /// <summary>
    //    /// Updates the EditorYieldInstruction and returns it's state. True if done.
    //    /// </summary>
    //    internal bool IsDone
    //    {
    //        get { Update(); return isDone; }
    //    }


    //    //basic constructor
    //    protected internal EditorYieldInstruction()
    //    {
    //        routine = InternalLogic();
    //    }

    //    //internal updatefunction, called with readonly
    //    protected internal void Update()
    //    {
    //        if (routine != null)
    //        {
    //            if (routine.MoveNext())
    //            {
    //                if (routine.Current != null)
    //                    isDone = (bool)routine.Current;
    //            }

    //        }
    //    }

    //    /// <summary>
    //    /// Internal logic routine of the EditorYieldInstruction.
    //    /// yield return false when not finished
    //    /// yield return true when finished.
    //    /// </summary>
    //    /// <returns>IEnumerator with true for done and false for not done</returns>
    //    protected internal abstract IEnumerator InternalLogic();
    //}
}

