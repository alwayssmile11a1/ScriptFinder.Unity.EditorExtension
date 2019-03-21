using Octokit;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace CoheeCreative
{

    public class ScriptFinder : EditorWindow
    {
        private static readonly string TITLE = "ScriptFinder";
        private static readonly Vector2 m_MinSize = new Vector2(500, 250);
        private static readonly float m_Timeout = 5f;

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
        private static string m_UserName;
        private static string m_Password;
        private List<string> m_Results = new List<string>();

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

                if (EditorPrefs.HasKey("ScriptFinderUsername"))
                {
                    m_UserName = EditorPrefs.GetString("ScriptFinderUsername");
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

        public void OnInspectorUpdate()
        {
            // This will only get called 10 times per second.
            Repaint();
        }

        private void OnLostFocus()
        {
            Quit();
        }

        private void Quit()
        {
            if (Instance != null) Instance.Close();
            Instance = null;
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

            m_CurrentScrollPos1 = EditorGUILayout.BeginScrollView(m_CurrentScrollPos1, GUILayout.Width(position.width), GUILayout.Height(position.height - 50));

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (m_Results.Count > 0)
            {
                foreach (string item in m_Results)
                {
                    EditorGUILayout.BeginHorizontal();
                    string name = Path.GetFileName(item);
                    EditorGUILayout.SelectableLabel(name, GUILayout.Height(30), GUILayout.Width(250));
                    EditorGUILayout.SelectableLabel(item, GUILayout.Height(30));
                    if (GUILayout.Button("Import", GUILayout.Width(60), GUILayout.Height(20)))
                    {
                        ImportFile(name, item);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
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

            EditorGUILayout.LabelField("Github Remote Paths");
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

            EditorGUILayout.LabelField("Github account: (Left blank if you don't want to use Github account)");
            m_UserName = EditorGUILayout.TextField(m_UserName, GUILayout.Height(20));
            m_Password = EditorGUILayout.PasswordField(m_Password, GUILayout.Height(20));
            EditorGUILayout.LabelField("When authenticated, you have access to private repositories and 5000 requests per hour instead of 60. So this is the recommended approach for interacting with the API");
            EditorGUILayout.SelectableLabel("Detail: https://developer.github.com/v3/#rate-limiting");
            EditorGUILayout.LabelField("Note: For security purpose, password would not be saved locally");

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
                EditorPrefs.SetString("ScriptFinderUsername", m_UserName);
            }

            if (GUILayout.Button("Reload", GUILayout.Height(30)))
            {
                LoadData(true);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

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
                    m_Results.Add(files[j]);
                }
            }

            //Remote
            for (int i = 0; i < m_RemotePaths.Count; i++)
            {
                GetRemoteFiles(m_RemotePaths[i], m_SearchString);
            }

            Repaint();
        }

        private void GetRemoteFiles(string url, string searchTerm)
        {
            Task.Factory.StartNew(async () =>
            {
                int repoNameStartIndex = url.LastIndexOf('/') + 1;
                var repoName = url.Substring(repoNameStartIndex);

                var repoOwner = url.Substring(0, repoNameStartIndex - 1);
                repoOwner = repoOwner.Substring(repoOwner.LastIndexOf('/') + 1);
                var octokitResults = await ListContentsOctokit(searchTerm, repoOwner, repoName, url);
                m_Results.AddRange(octokitResults);

                Repaint();

            }).Wait();
        }

        async Task<IEnumerable<string>> ListContentsOctokit(string searchTerm, string repoOwner, string repoName, string url)
        {
            var client = new GitHubClient(new ProductHeaderValue("Cohee-Creative"));

            EditorCoroutine currentCoroutine = EditorCoroutine.StartCoroutine(CheckTimeOut());

            if (!string.IsNullOrEmpty(m_UserName))
            {
                var basicAuth = new Credentials(m_UserName, m_Password);
                client.Credentials = basicAuth;
            }

            var request = new SearchCodeRequest(searchTerm, repoOwner, repoName)
            {
                // we can restrict search to the file, path or search both
                In = new[] { CodeInQualifier.Path },

                // how about we find a file based on a certain language
                Language = Language.CSharp,

                // we may want to restrict the file based on file extension
                Extension = "cs",


            };

            var task = await client.Search.SearchCode(request);
            currentCoroutine.Stop();
            return task.Items.Select(content => url + content.Path);

        }

        private IEnumerator CheckTimeOut()
        {
            //Since waitforsecond doesn't work, we have to do this
            int iteration = (int)(m_Timeout / (1 / 60f));
            for (int i = 0; i < iteration; i++)
            {
                yield return null;
            }

            //Timeout 
            Debug.LogWarning("ScriptFinder: Github request timeout!! Consider checking username, password (if using) and remote paths.");

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
    }

}

