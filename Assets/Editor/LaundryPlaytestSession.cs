using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in, Editor-only. Never opens or terminates a Unity process.
[InitializeOnLoad]
public static class LaundryPlaytestSession
{
    [Serializable] public class Record
    {
        public string id, project, processStart, startedUtc, endedUtc, state, scenario, reason;
        public int pid, seconds, screenshots, errors, warnings, targetFrameRate, vSync, index;
        public bool passed, runInBackground;
        public float timeScale;
        public int gameViewSizeIndex;
        public uint gameViewWidth, gameViewHeight;
        public bool gameViewChanged;
        public bool gameViewMaximized;
    }
    [Serializable] public class Metrics
    {
        public string utc, reason;
        public double cpu, ram, gpu, freeBytes;
    }
    [Serializable] private class History
    {
        public List<Record> sessions = new List<Record>();
        public int cycleStart;
        public string cycleReview;
    }

    private const string Owner = "LaundryQA-v1";
    private const string SessionKey = "LaundryQA.ActiveSession";
    private const long MaxBytes = 300L * 1024 * 1024;
    private const long MaxLogBytes = 5L * 1024 * 1024;
    private static readonly string Project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    public static string Root { get { return Path.Combine(Project, "QAOutput", "Playtests"); } }
    private static string OutputRoot { get { return Path.Combine(Project, "QAOutput"); } }
    public static string Folder { get { return current == null ? null : Path.Combine(Root, current.id); } }
    public static Record Current { get { return current; } }
    private static string LockPath { get { return Path.Combine(Root, "active.lock"); } }
    private static Record current;
    private static double nextPoll;
    private static int lastFrame;
    private static DateTime lastFrameUtc;
    private static Metrics metrics;

    static LaundryPlaytestSession()
    {
        string id = SessionState.GetString(SessionKey, "");
        if (id.Length > 0 && File.Exists(LockPath))
        {
            var saved = JsonUtility.FromJson<Record>(File.ReadAllText(LockPath));
            if (saved.id == id && saved.pid == Process.GetCurrentProcess().Id && saved.project == Project)
                current = saved;
        }
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayMode;
        Application.logMessageReceived += OnLog;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += OnQuit;
    }

    public static string Begin(string scenario = "observe", int seconds = 180)
    {
        if (current != null || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("A session is active. Refusing a duplicate playtest.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Editor must be compiled, idle, and scene changes saved before testing.");
        string expectedScene = scenario == "intro-controls" ? "Intro" : "SampleScene";
        if (SceneManager.GetActiveScene().path != "Assets/Prefabs/Scenes/" + expectedScene + ".unity")
            throw new InvalidOperationException("Open " + expectedScene + " first; this manager never replaces the user's scene.");
        if (Process.GetProcessesByName("Unity").Length != 1)
            throw new InvalidOperationException("Exactly one existing Unity Editor is required.");

        CheckPath(Project);
        Directory.CreateDirectory(Root);
        CheckPath(Root);
        string marker = Path.Combine(Root, ".qa-owned");
        if (File.Exists(marker) && File.ReadAllText(marker) != Owner) throw new IOException("Unknown output owner.");
        if (!File.Exists(marker)) File.WriteAllText(marker, Owner);
        RecoverDeadLock();
        History history = LoadHistory();
        // Old development sessions must not permanently exhaust later work's test allowance.
        int todaySessions = history.sessions.Count(s => DateTime.Parse(s.startedUtc).ToUniversalTime().Date == DateTime.UtcNow.Date);
        if (todaySessions >= 12) throw new InvalidOperationException("Today's 12-session budget reached.");
        if (history.sessions.Count - history.cycleStart >= 3)
            throw new InvalidOperationException("Three attempts in this cycle. Review results and call BeginNextCycle before more testing.");
        if (history.sessions.Count > 0)
        {
            Record previous = history.sessions[history.sessions.Count - 1];
            if (string.IsNullOrEmpty(previous.endedUtc) || (DateTime.UtcNow - DateTime.Parse(previous.endedUtc).ToUniversalTime()).TotalSeconds < 15)
                throw new InvalidOperationException("Wait for confirmed shutdown and the 15 second cooldown.");
            if (history.sessions.Count - history.cycleStart >= 3 && history.sessions.Skip(history.cycleStart).TakeLast(3).All(s => s.scenario == scenario && !s.passed))
                throw new InvalidOperationException("Same scenario failed three times. Diagnose before another attempt.");
        }
        // Retain two completed sessions before creating the third. Only marked directories are eligible.
        var old = Directory.GetDirectories(Root).OrderByDescending(p => p).ToArray();
        foreach (string directory in old.Skip(2)) DeleteOwnedSession(directory);
        if (SafeFiles(OutputRoot).Sum(p => new FileInfo(p).Length) >= MaxBytes - 20 * 1024 * 1024)
            throw new IOException("QA storage budget reached.");
        var process = Process.GetCurrentProcess();
        current = new Record {
            id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6),
            project = Project, pid = process.Id, processStart = process.StartTime.ToUniversalTime().ToString("o"),
            startedUtc = DateTime.UtcNow.ToString("o"), state = "preflight", scenario = scenario,
            seconds = Mathf.Clamp(seconds, 15, 300), index = history.sessions.Count + 1,
            targetFrameRate = Application.targetFrameRate, vSync = QualitySettings.vSyncCount,
            runInBackground = Application.runInBackground, timeScale = Time.timeScale
        };
        try
        {
            // CreateNew is the atomic claim. An existing lock is never overwritten here.
            using (var stream = new FileStream(LockPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(current));
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, ".qa-owned"), Owner);
            SessionState.SetString(SessionKey, current.id);
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var gameView = Resources.FindObjectsOfTypeAll(gameViewType).FirstOrDefault();
            var sizeProperty = gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (gameView != null && sizeProperty != null && sizeProperty.CanWrite)
            {
                current.gameViewSizeIndex = (int)sizeProperty.GetValue(gameView, null);
                current.gameViewMaximized = ((EditorWindow)gameView).maximized;
                PlayModeWindow.GetRenderingResolution(out current.gameViewWidth, out current.gameViewHeight);
                current.gameViewChanged = true;
                Save();
                PlayModeWindow.SetCustomRenderingResolution(1280, 720, "Laundry QA 720p");
                ((EditorWindow)gameView).maximized = true;
            }
            Save();
            history.sessions.Add(current);
            SaveHistory(history);
            var start = new ProcessStartInfo("powershell.exe") {
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + Path.Combine(Project, "Tools", "Playtests", "Monitor.ps1") +
                    "\" -SessionDir \"" + Folder + "\" -EditorPid " + current.pid + " -EditorStart \"" + current.processStart + "\"",
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(start);
        }
        catch
        {
            if (File.Exists(LockPath) && JsonUtility.FromJson<Record>(File.ReadAllText(LockPath)).id == current.id)
                Finish("preflight setup failed", false);
            else current = null;
            throw;
        }
        return "Preflight started: " + current.id;
    }

    private static void RecoverDeadLock()
    {
        if (!File.Exists(LockPath)) return;
        Record old = JsonUtility.FromJson<Record>(File.ReadAllText(LockPath));
        if (old == null || old.project != Project) throw new IOException("Foreign or invalid test lock.");
        Process process = null;
        try { process = Process.GetProcessById(old.pid); }
        catch (ArgumentException) { }
        if (process != null && !process.HasExited)
            throw new InvalidOperationException("Lock owner is alive; refusing to steal its session, even after domain reload.");
        // Only a definitely dead owner can be recovered. Record the interruption first.
        var history = LoadHistory();
        var record = history.sessions.Find(s => s.id == old.id);
        if (record != null && string.IsNullOrEmpty(record.endedUtc)) {
            record.endedUtc = DateTime.UtcNow.ToString("o"); record.state = "interrupted";
            record.reason = "Previous owner process is no longer alive"; SaveHistory(history);
        }
        File.Delete(LockPath);
    }

    private static void Poll()
    {
        if (current == null || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        try
        {
            if (current.state == "stopping")
            {
                // A script refresh can discard EnteredEditMode's delayed cleanup callback.
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    Finish(current.reason, current.passed);
                return;
            }
            string request = Path.Combine(Folder, "stop.request");
            if (File.Exists(request)) { Stop(File.ReadAllText(request)); return; }
            string path = Path.Combine(Folder, "metrics.json");
            if (File.Exists(path)) metrics = JsonUtility.FromJson<Metrics>(File.ReadAllText(path));
            double age = (DateTime.UtcNow - DateTime.Parse(current.startedUtc).ToUniversalTime()).TotalSeconds;
            if (metrics == null) { if (age > 15) Stop("No resource monitor heartbeat"); return; }
            if ((DateTime.UtcNow - DateTime.Parse(metrics.utc).ToUniversalTime()).TotalSeconds > 10) { Stop("Resource monitor heartbeat stale"); return; }
            if (!string.IsNullOrEmpty(metrics.reason)) { Stop(metrics.reason); return; }
            if (SafeFiles(OutputRoot).Sum(p => new FileInfo(p).Length) >= MaxBytes) { Stop("QA storage budget reached"); return; }
            if (current.state == "preflight")
            {
                // User-approved relaxed RAM limit; sustained pressure is still monitored during play.
                if (metrics.cpu >= 90 || metrics.ram >= 97 || metrics.gpu >= 95 || metrics.freeBytes < 5L * 1024 * 1024 * 1024)
                { Stop("Resources above test admission limit"); return; }
                if (metrics.gpu < 0) { Stop("GPU utilization unavailable; playtest deferred"); return; }
                current.state = "starting"; Save();
                EditorApplication.isPlaying = true;
                return;
            }
            if (age >= current.seconds) { Stop("Session time limit reached"); return; }
            if (current.state == "playing")
            {
                if (Time.frameCount != lastFrame) { lastFrame = Time.frameCount; lastFrameUtc = DateTime.UtcNow; }
                else if ((DateTime.UtcNow - lastFrameUtc).TotalSeconds > 10) { Stop("Game frames stopped advancing"); return; }
                Log("resources", JsonUtility.ToJson(metrics));
                var driver = UnityEngine.Object.FindAnyObjectByType<LaundryPlaytestDriver>();
                if (driver != null) Log("state", driver.Snapshot(true));
                File.WriteAllText(Path.Combine(Folder, "editor-heartbeat.txt"), DateTime.UtcNow.ToString("o"));
            }
        }
        catch (Exception e) { Stop("Safety monitor exception: " + e.Message); }
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (current == null) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            current.state = "playing";
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            Application.runInBackground = true;
            GameInput.BeginTestInput();
            LaundryPlaytestDriver.LogEvent = Log;
            LaundryPlaytestDriver.RequestCapture = ReserveScreenshot;
            LaundryPlaytestDriver.FinishSession = Stop;
            var go = new GameObject("Laundry QA - Editor only");
            if (go.AddComponent<LaundryPlaytestDriver>() == null) { Stop("QA driver could not attach"); return; }
            if (current.scenario == "intro-controls")
            {
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<LaundryIntroScenario>();
            }
            else if (current.scenario != "observe" && current.scenario != "baseline-controls")
            {
                var scenario = go.AddComponent<LaundryPlaytestScenario>();
                scenario.Run(current.scenario);
            }
            lastFrameUtc = DateTime.UtcNow;
            var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.Focus();
            Save();
        }
        if (state == PlayModeStateChange.ExitingPlayMode) GameInput.EndTestInput();
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () => {
                if (current != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                    Finish(current.reason ?? "Stopped manually", current.passed);
            };
    }

    public static void Stop(string reason = "Stopped by operator", bool passed = false)
    {
        if (current == null || current.state == "stopping") return; // Never stop a user-owned play session.
        current.reason = reason; current.passed = passed && current.errors == 0;
        current.state = "stopping"; Save();
        var driver = UnityEngine.Object.FindAnyObjectByType<LaundryPlaytestDriver>();
        if (driver != null) driver.StopAllCoroutines();
        GameInput.EndTestInput();
        // Gameplay coroutines may run inside GameView.OnGUI. Leave that call stack before
        // exiting Play Mode or restoring/maximizing windows (native IMGUI can otherwise crash).
        EditorApplication.delayCall += () => {
            if (current == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else Finish(current.reason, current.passed);
        };
    }

    public static string BeginNextCycle(string review)
    {
        if (current != null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop the owned session before reviewing a cycle");
        if (string.IsNullOrWhiteSpace(review)) throw new ArgumentException("A concrete review of the previous cycle is required");
        var history = LoadHistory(); history.cycleStart = history.sessions.Count; history.cycleReview = review;
        SaveHistory(history);
        return "Cycle reviewed; daily 12-attempt budget remains in force";
    }

    private static void Finish(string reason, bool passed)
    {
        if (current == null) return;
        Application.targetFrameRate = current.targetFrameRate;
        QualitySettings.vSyncCount = current.vSync;
        Application.runInBackground = current.runInBackground;
        if (Time.timeScale != current.timeScale) Time.timeScale = current.timeScale;
        try
        {
            if (current.gameViewChanged)
            {
                // Unity's public API updates the current custom size, so restoring the index alone is insufficient.
                if (current.gameViewWidth > 0 && current.gameViewHeight > 0)
                    PlayModeWindow.SetCustomRenderingResolution(current.gameViewWidth, current.gameViewHeight, "Restored Game view");
                var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                var gameView = Resources.FindObjectsOfTypeAll(gameViewType).FirstOrDefault();
                var sizeProperty = gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (gameView != null && sizeProperty != null && sizeProperty.CanWrite)
                {
                    sizeProperty.SetValue(gameView, current.gameViewSizeIndex, null);
                    ((EditorWindow)gameView).maximized = current.gameViewMaximized;
                }
            }
        }
        catch (Exception e) { reason += "; Game view restore failed: " + e.Message; passed = false; }
        current.reason = reason; current.passed = passed;
        current.state = passed ? "passed" : "stopped"; current.endedUtc = DateTime.UtcNow.ToString("o");
        if (Directory.Exists(Folder)) {
            File.WriteAllText(Path.Combine(Folder, "monitor.stop"), reason);
            File.WriteAllText(Path.Combine(Folder, "session.json"), JsonUtility.ToJson(current, true));
        }
        var history = LoadHistory();
        int index = history.sessions.FindIndex(s => s.id == current.id);
        if (index >= 0) history.sessions[index] = current;
        SaveHistory(history);
        if (File.Exists(LockPath) && JsonUtility.FromJson<Record>(File.ReadAllText(LockPath)).id == current.id) File.Delete(LockPath);
        SessionState.EraseString(SessionKey);
        current = null; metrics = null;
    }

    private static void BeforeReload()
    {
        if (current == null) return;
        Save();
        // Entering play naturally reloads assemblies. Mid-test recompilation is an interrupted test.
        if (current.state == "playing") Stop("Scripts reloaded during playtest");
    }
    private static void OnQuit() { if (current != null) Finish("Editor closed by user", false); }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (current == null) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            current.errors++; Log("error", message + "\n" + stack); Stop("Runtime error: " + message);
        }
        else if (type == LogType.Warning) { current.warnings++; if (current.warnings <= 20) Log("warning", message); }
    }
    public static void Log(string kind, string message)
    {
        if (current == null || !Directory.Exists(Folder)) return;
        string path = Path.Combine(Folder, "events.jsonl");
        string line = JsonUtility.ToJson(new EventLine { utc = DateTime.UtcNow.ToString("o"), kind = kind, message = message }) + "\n";
        if ((!File.Exists(path) ? 0 : new FileInfo(path).Length) + System.Text.Encoding.UTF8.GetByteCount(line) <= MaxLogBytes)
            File.AppendAllText(path, line);
    }
    [Serializable] private class EventLine { public string utc, kind, message; }
    public static string ReserveScreenshot(string label)
    {
        if (current == null || current.screenshots >= 10) return null;
        if (SafeFiles(OutputRoot).Sum(p => new FileInfo(p).Length) > MaxBytes - 4 * 1024 * 1024) return null;
        label = new string(label.Where(c => char.IsLetterOrDigit(c) || c == '-').Take(50).ToArray());
        current.screenshots++; Save();
        return Path.Combine(Folder, current.screenshots.ToString("00") + "-" + label + ".png");
    }
    private static void Save() { if (current != null) { File.WriteAllText(LockPath, JsonUtility.ToJson(current)); if (Directory.Exists(Folder)) File.WriteAllText(Path.Combine(Folder, "session.json"), JsonUtility.ToJson(current, true)); } }
    private static History LoadHistory() { string p = Path.Combine(Root, "history.json"); return File.Exists(p) ? JsonUtility.FromJson<History>(File.ReadAllText(p)) : new History(); }
    private static void SaveHistory(History history) { File.WriteAllText(Path.Combine(Root, "history.json"), JsonUtility.ToJson(history, true)); }
    private static void CheckPath(string path)
    {
        for (var d = new DirectoryInfo(Path.GetFullPath(path)); d != null; d = d.Parent)
            if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse point in QA path: " + d.FullName);
    }
    private static IEnumerable<string> SafeFiles(string directory)
    {
        CheckPath(directory);
        foreach (string file in Directory.GetFiles(directory)) {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked QA file");
            yield return file;
        }
        foreach (string child in Directory.GetDirectories(directory)) foreach (string file in SafeFiles(child)) yield return file;
    }
    private static void DeleteOwnedSession(string path)
    {
        string full = Path.GetFullPath(path);
        if (Path.GetDirectoryName(full) != Root || !File.Exists(Path.Combine(full, ".qa-owned")) || File.ReadAllText(Path.Combine(full, ".qa-owned")) != Owner)
            throw new IOException("Refusing cleanup outside a known session directory");
        SafeFiles(full).ToArray(); // Verify the whole tree before recursive deletion.
        Directory.Delete(full, true);
    }

    [MenuItem("Tools/Laundry QA/Run controls regression")]
    private static void RunControls() { Begin("controls-regression", 240); }
    [MenuItem("Tools/Laundry QA/Run a full day")]
    private static void RunDay() { Begin("normal-day", 300); }
    [MenuItem("Tools/Laundry QA/Stop owned test")]
    private static void StopFromMenu() { Stop(); }
    [MenuItem("Tools/Laundry QA/Show test records")]
    private static void ShowRecords() { if (Directory.Exists(Root)) EditorUtility.RevealInFinder(Root); }
}
