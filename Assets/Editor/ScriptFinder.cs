using Octokit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using marijnz;

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

            EditorCoroutines.EditorCoroutine currentCoroutine = this.StartCoroutine(CheckTimeOut());

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
            this.StopCoroutine("CheckTimeOut");
            return task.Items.Select(content => content.HtmlUrl);

        }

        private IEnumerator CheckTimeOut()
        {
            ////Since WaitForSeconds doesn't actually work, we do it this way
            //int interation = (int)(m_Timeout / (1 / 60f));
            //for (int i = 0; i < interation; i++)
            //{
            //    yield return null;
            //}

            yield return new WaitForSeconds(m_Timeout);

            //Timeout 
            Debug.LogWarning("ScriptFinder: Github request timeout!! Consider checking username, password (if using) and remote paths.");

        }

        private void ImportFile(string fileName, string fullPath)
        {
            string folderToImport = Path.Combine(Directory.GetCurrentDirectory(), "Assets", m_DefaultImportFolder);
            if (!Directory.Exists(folderToImport)) AssetDatabase.CreateFolder("Assets", m_DefaultImportFolder);
            string newFilePath = Path.Combine(folderToImport, fileName);

            //remote files
            if (fullPath.Contains("github.com"))
            {
                fullPath = fullPath.Replace("github", "raw.githubusercontent");
                fullPath = fullPath.Replace($"/blob", "");
                this.StartCoroutine(DownloadFile(fileName, fullPath, newFilePath));
            }
            else //local files
            {
                File.Copy(fullPath, newFilePath);
                AssetDatabase.ImportAsset(Path.Combine("Assets", m_DefaultImportFolder, fileName));
            }
        }

        IEnumerator DownloadFile(string fileName, string url, string newFilePath)
        {
            var uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            string path = Path.Combine(newFilePath);
            Debug.Log($"Downloading {fileName}, please wait!");
            uwr.downloadHandler = new DownloadHandlerFile(path);
            yield return uwr.SendWebRequest();
            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Debug.LogError(uwr.error);
            }
            else
            {
                File.Move(path, newFilePath);
                AssetDatabase.ImportAsset(Path.Combine("Assets", m_DefaultImportFolder, fileName));
                Debug.Log($"{fileName} Imported.");
            }
        }

    }
}

/// <summary>
/// Gives the possibility to use coroutines in editorscripts.
/// from https://github.com/marijnz/unity-editor-coroutines/tree/master/Assets/EditorCoroutines/Editor
/// </summary>
namespace marijnz
{
    public class EditorCoroutines
    {
        public class EditorCoroutine
        {
            public ICoroutineYield currentYield = new YieldDefault();
            public IEnumerator routine;
            public string routineUniqueHash;
            public string ownerUniqueHash;
            public string MethodName = "";

            public int ownerHash;
            public string ownerType;

            public bool finished = false;

            public EditorCoroutine(IEnumerator routine, int ownerHash, string ownerType)
            {
                this.routine = routine;
                this.ownerHash = ownerHash;
                this.ownerType = ownerType;
                ownerUniqueHash = ownerHash + "_" + ownerType;

                if (routine != null)
                {
                    string[] split = routine.ToString().Split('<', '>');
                    if (split.Length == 3)
                    {
                        this.MethodName = split[1];
                    }
                }

                routineUniqueHash = ownerHash + "_" + ownerType + "_" + MethodName;
            }

            public EditorCoroutine(string methodName, int ownerHash, string ownerType)
            {
                MethodName = methodName;
                this.ownerHash = ownerHash;
                this.ownerType = ownerType;
                ownerUniqueHash = ownerHash + "_" + ownerType;
                routineUniqueHash = ownerHash + "_" + ownerType + "_" + MethodName;
            }
        }

        public interface ICoroutineYield
        {
            bool IsDone(float deltaTime);
        }

        struct YieldDefault : ICoroutineYield
        {
            public bool IsDone(float deltaTime)
            {
                return true;
            }
        }

        struct YieldWaitForSeconds : ICoroutineYield
        {
            public float timeLeft;

            public bool IsDone(float deltaTime)
            {
                timeLeft -= deltaTime;
                return timeLeft < 0;
            }
        }

        struct YieldCustomYieldInstruction : ICoroutineYield
        {
            public CustomYieldInstruction customYield;

            public bool IsDone(float deltaTime)
            {
                return !customYield.keepWaiting;
            }
        }

        struct YieldWWW : ICoroutineYield
        {
            public UnityWebRequest Www;

            public bool IsDone(float deltaTime)
            {
                return Www.isDone;
            }
        }

        struct YieldAsync : ICoroutineYield
        {
            public AsyncOperation asyncOperation;

            public bool IsDone(float deltaTime)
            {
                return asyncOperation.isDone;
            }
        }

        struct YieldNestedCoroutine : ICoroutineYield
        {
            public EditorCoroutine coroutine;

            public bool IsDone(float deltaTime)
            {
                return coroutine.finished;
            }
        }

        static EditorCoroutines instance = null;

        Dictionary<string, List<EditorCoroutine>> coroutineDict = new Dictionary<string, List<EditorCoroutine>>();
        List<List<EditorCoroutine>> tempCoroutineList = new List<List<EditorCoroutine>>();

        Dictionary<string, Dictionary<string, EditorCoroutine>> coroutineOwnerDict =
            new Dictionary<string, Dictionary<string, EditorCoroutine>>();

        DateTime previousTimeSinceStartup;

        /// <summary>Starts a coroutine.</summary>
        /// <param name="routine">The coroutine to start.</param>
        /// <param name="thisReference">Reference to the instance of the class containing the method.</param>
        public static EditorCoroutine StartCoroutine(IEnumerator routine, object thisReference)
        {
            CreateInstanceIfNeeded();
            return instance.GoStartCoroutine(routine, thisReference);
        }

        /// <summary>Starts a coroutine.</summary>
        /// <param name="methodName">The name of the coroutine method to start.</param>
        /// <param name="thisReference">Reference to the instance of the class containing the method.</param>
        public static EditorCoroutine StartCoroutine(string methodName, object thisReference)
        {
            return StartCoroutine(methodName, null, thisReference);
        }

        /// <summary>Starts a coroutine.</summary>
        /// <param name="methodName">The name of the coroutine method to start.</param>
        /// <param name="value">The parameter to pass to the coroutine.</param>
        /// <param name="thisReference">Reference to the instance of the class containing the method.</param>
        public static EditorCoroutine StartCoroutine(string methodName, object value, object thisReference)
        {
            MethodInfo methodInfo = thisReference.GetType()
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (methodInfo == null)
            {
                Debug.LogError("Coroutine '" + methodName + "' couldn't be started, the method doesn't exist!");
            }
            object returnValue;

            if (value == null)
            {
                returnValue = methodInfo.Invoke(thisReference, null);
            }
            else
            {
                returnValue = methodInfo.Invoke(thisReference, new object[] { value });
            }

            if (returnValue is IEnumerator)
            {
                CreateInstanceIfNeeded();
                return instance.GoStartCoroutine((IEnumerator)returnValue, thisReference);
            }
            else
            {
                Debug.LogError("Coroutine '" + methodName + "' couldn't be started, the method doesn't return an IEnumerator!");
            }

            return null;
        }

        /// <summary>Stops all coroutines being the routine running on the passed instance.</summary>
        /// <param name="routine"> The coroutine to stop.</param>
        /// <param name="thisReference">Reference to the instance of the class containing the method.</param>
        public static void StopCoroutine(IEnumerator routine, object thisReference)
        {
            CreateInstanceIfNeeded();
            instance.GoStopCoroutine(routine, thisReference);
        }

        /// <summary>
        /// Stops all coroutines named methodName running on the passed instance.</summary>
        /// <param name="methodName"> The name of the coroutine method to stop.</param>
        /// <param name="thisReference">Reference to the instance of the class containing the method.</param>
        public static void StopCoroutine(string methodName, object thisReference)
        {
            CreateInstanceIfNeeded();
            instance.GoStopCoroutine(methodName, thisReference);
        }

        /// <summary>
        /// Stops all coroutines running on the passed instance.</summary>
        /// <param name="thisReference">Reference to the instance of the class containing the method.</param>
        public static void StopAllCoroutines(object thisReference)
        {
            CreateInstanceIfNeeded();
            instance.GoStopAllCoroutines(thisReference);
        }

        static void CreateInstanceIfNeeded()
        {
            if (instance == null)
            {
                instance = new EditorCoroutines();
                instance.Initialize();
            }
        }

        void Initialize()
        {
            previousTimeSinceStartup = DateTime.Now;
            EditorApplication.update += OnUpdate;
        }

        void GoStopCoroutine(IEnumerator routine, object thisReference)
        {
            GoStopActualRoutine(CreateCoroutine(routine, thisReference));
        }

        void GoStopCoroutine(string methodName, object thisReference)
        {
            GoStopActualRoutine(CreateCoroutineFromString(methodName, thisReference));
        }

        void GoStopActualRoutine(EditorCoroutine routine)
        {
            if (coroutineDict.ContainsKey(routine.routineUniqueHash))
            {
                coroutineOwnerDict[routine.ownerUniqueHash].Remove(routine.routineUniqueHash);
                coroutineDict.Remove(routine.routineUniqueHash);
            }
        }

        void GoStopAllCoroutines(object thisReference)
        {
            EditorCoroutine coroutine = CreateCoroutine(null, thisReference);
            if (coroutineOwnerDict.ContainsKey(coroutine.ownerUniqueHash))
            {
                foreach (var couple in coroutineOwnerDict[coroutine.ownerUniqueHash])
                {
                    coroutineDict.Remove(couple.Value.routineUniqueHash);
                }
                coroutineOwnerDict.Remove(coroutine.ownerUniqueHash);
            }
        }

        EditorCoroutine GoStartCoroutine(IEnumerator routine, object thisReference)
        {
            if (routine == null)
            {
                Debug.LogException(new Exception("IEnumerator is null!"), null);
            }
            EditorCoroutine coroutine = CreateCoroutine(routine, thisReference);
            GoStartCoroutine(coroutine);
            return coroutine;
        }

        void GoStartCoroutine(EditorCoroutine coroutine)
        {
            if (!coroutineDict.ContainsKey(coroutine.routineUniqueHash))
            {
                List<EditorCoroutine> newCoroutineList = new List<EditorCoroutine>();
                coroutineDict.Add(coroutine.routineUniqueHash, newCoroutineList);
            }
            coroutineDict[coroutine.routineUniqueHash].Add(coroutine);

            if (!coroutineOwnerDict.ContainsKey(coroutine.ownerUniqueHash))
            {
                Dictionary<string, EditorCoroutine> newCoroutineDict = new Dictionary<string, EditorCoroutine>();
                coroutineOwnerDict.Add(coroutine.ownerUniqueHash, newCoroutineDict);
            }

            // If the method from the same owner has been stored before, it doesn't have to be stored anymore,
            // One reference is enough in order for "StopAllCoroutines" to work
            if (!coroutineOwnerDict[coroutine.ownerUniqueHash].ContainsKey(coroutine.routineUniqueHash))
            {
                coroutineOwnerDict[coroutine.ownerUniqueHash].Add(coroutine.routineUniqueHash, coroutine);
            }

            MoveNext(coroutine);
        }

        EditorCoroutine CreateCoroutine(IEnumerator routine, object thisReference)
        {
            return new EditorCoroutine(routine, thisReference.GetHashCode(), thisReference.GetType().ToString());
        }

        EditorCoroutine CreateCoroutineFromString(string methodName, object thisReference)
        {
            return new EditorCoroutine(methodName, thisReference.GetHashCode(), thisReference.GetType().ToString());
        }

        void OnUpdate()
        {
            float deltaTime = (float)(DateTime.Now.Subtract(previousTimeSinceStartup).TotalMilliseconds / 1000.0f);

            previousTimeSinceStartup = DateTime.Now;
            if (coroutineDict.Count == 0)
            {
                return;
            }

            tempCoroutineList.Clear();
            foreach (var pair in coroutineDict)
                tempCoroutineList.Add(pair.Value);

            for (var i = tempCoroutineList.Count - 1; i >= 0; i--)
            {
                List<EditorCoroutine> coroutines = tempCoroutineList[i];

                for (int j = coroutines.Count - 1; j >= 0; j--)
                {
                    EditorCoroutine coroutine = coroutines[j];

                    if (!coroutine.currentYield.IsDone(deltaTime))
                    {
                        continue;
                    }

                    if (!MoveNext(coroutine))
                    {
                        coroutines.RemoveAt(j);
                        coroutine.currentYield = null;
                        coroutine.finished = true;
                    }

                    if (coroutines.Count == 0)
                    {
                        coroutineDict.Remove(coroutine.ownerUniqueHash);
                    }
                }
            }
        }

        static bool MoveNext(EditorCoroutine coroutine)
        {
            if (coroutine.routine.MoveNext())
            {
                return Process(coroutine);
            }

            return false;
        }

        // returns false if no next, returns true if OK
        static bool Process(EditorCoroutine coroutine)
        {
            object current = coroutine.routine.Current;
            if (current == null)
            {
                coroutine.currentYield = new YieldDefault();
            }
            else if (current is WaitForSeconds)
            {
                float seconds = float.Parse(GetInstanceField(typeof(WaitForSeconds), current, "m_Seconds").ToString());
                coroutine.currentYield = new YieldWaitForSeconds() { timeLeft = seconds };
            }
            else if (current is CustomYieldInstruction)
            {
                coroutine.currentYield = new YieldCustomYieldInstruction()
                {
                    customYield = current as CustomYieldInstruction
                };
            }
            else if (current is UnityWebRequest)
            {
                coroutine.currentYield = new YieldWWW { Www = (UnityWebRequest)current };
            }
            else if (current is WaitForFixedUpdate || current is WaitForEndOfFrame)
            {
                coroutine.currentYield = new YieldDefault();
            }
            else if (current is AsyncOperation)
            {
                coroutine.currentYield = new YieldAsync { asyncOperation = (AsyncOperation)current };
            }
            else if (current is EditorCoroutine)
            {
                coroutine.currentYield = new YieldNestedCoroutine { coroutine = (EditorCoroutine)current };
            }
            else
            {
                Debug.LogException(
                    new Exception("<" + coroutine.MethodName + "> yielded an unknown or unsupported type! (" + current.GetType() + ")"),
                    null);
                coroutine.currentYield = new YieldDefault();
            }
            return true;
        }

        static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
    }

    public static class EditorCoroutineExtensions
    {
        public static EditorCoroutines.EditorCoroutine StartCoroutine(this EditorWindow thisRef, IEnumerator coroutine)
        {
            return EditorCoroutines.StartCoroutine(coroutine, thisRef);
        }

        public static EditorCoroutines.EditorCoroutine StartCoroutine(this EditorWindow thisRef, string methodName)
        {
            return EditorCoroutines.StartCoroutine(methodName, thisRef);
        }

        public static EditorCoroutines.EditorCoroutine StartCoroutine(this EditorWindow thisRef, string methodName, object value)
        {
            return EditorCoroutines.StartCoroutine(methodName, value, thisRef);
        }

        public static void StopCoroutine(this EditorWindow thisRef, IEnumerator coroutine)
        {
            EditorCoroutines.StopCoroutine(coroutine, thisRef);
        }

        public static void StopCoroutine(this EditorWindow thisRef, string methodName)
        {
            EditorCoroutines.StopCoroutine(methodName, thisRef);
        }

        public static void StopAllCoroutines(this EditorWindow thisRef)
        {
            EditorCoroutines.StopAllCoroutines(thisRef);
        }
    }
}