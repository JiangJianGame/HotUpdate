// ================================================================
// 文件名：EditorGitCommitTools.cs
// 描述：Git 自动提交编辑器工具，支持一键 add/commit/push，自动计算工作时长并记录日志
// 作者：LuoZuDe
// 创建时间：XXX
// ================================================================

using System;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;

namespace JiangJian
{
    /// <summary>
    /// 提交类型枚举，与 commitTemplates 模板数组下标一一对应。
    /// </summary>
    public enum CommitType
    {
        完成功能 = 0,
        修复bug,
        做了优化,
        测试,
    }

    /// <summary>
    /// Git 提交工具 - 偏好设置持久化（EditorPrefs）。
    /// </summary>
    public class EditorGitCommitTools : Editor
    {
        private const string PREFS_COMMIT_TYPE = "LuoZuDe.GitCommit.CommitType";
        private const string PREFS_TIME_CONSUMING = "LuoZuDe.GitCommit.TimeConsuming";
        private const string PREFS_WINDOW_WIDTH = "LuoZuDe.GitCommit.WindowWidth";
        private const string PREFS_WINDOW_HEIGHT = "LuoZuDe.GitCommit.WindowHeight";

        public static CommitType SavedCommitType
        {
            get => (CommitType)EditorPrefs.GetInt(PREFS_COMMIT_TYPE, 0);
            set => EditorPrefs.SetInt(PREFS_COMMIT_TYPE, (int)value);
        }

        public static string SavedTimeConsuming
        {
            get => EditorPrefs.GetString(PREFS_TIME_CONSUMING, "");
            set => EditorPrefs.SetString(PREFS_TIME_CONSUMING, value ?? "");
        }

        public static float SavedWindowWidth
        {
            get => EditorPrefs.GetFloat(PREFS_WINDOW_WIDTH, 500f);
            set => EditorPrefs.SetFloat(PREFS_WINDOW_WIDTH, value);
        }

        public static float SavedWindowHeight
        {
            get => EditorPrefs.GetFloat(PREFS_WINDOW_HEIGHT, 500f);
            set => EditorPrefs.SetFloat(PREFS_WINDOW_HEIGHT, value);
        }
    }

    /// <summary>
    /// Git 自动提交编辑器窗口。快捷键 F4 打开。
    /// 支持自动计算工作时长（扣除午休）、一键 add/commit/push、记录提交日志到桌面。
    /// </summary>
    public class GitCommitWindow : EditorWindow
    {
        // ==================== 常量 ====================

        /// <summary>午休开始时间（12:00）</summary>
        private static readonly TimeSpan LunchBreakStart = new TimeSpan(12, 0, 0);
        /// <summary>午休结束时间（13:30）</summary>
        private static readonly TimeSpan LunchBreakEnd = new TimeSpan(13, 30, 0);
        /// <summary>午休时长</summary>
        private static readonly TimeSpan LunchBreakDuration = LunchBreakEnd - LunchBreakStart;

        /// <summary>提交信息模板，下标与 CommitType 枚举值对应</summary>
        private static readonly string[] commitTemplates =
        {
            "[feature][&time][&info]",
            "[fix][&time][&info]",
            "[perf][&time][&info]",
            "[test][&time][&info]",
        };

        // ==================== 字段 ====================

        private string commitRecordPath;
        private string timeConsuming;
        private string commitInfo;
        private CommitType commitType;
        private Vector2 scrollPosition;

        // 缓存 GUIStyle，避免 OnGUI 每帧 new
        private GUIStyle labelStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle normalButtonStyle;
        private GUIStyle titleStyle;

        // ==================== 入口 & 生命周期 ====================

        [MenuItem("Tools/Git Commit _F4")]
        public static void ShowCommitWindow()
        {
            var window = GetWindow<GitCommitWindow>("Git自动提交工具");
            window.ShowPopup();
            window.minSize = new Vector2(400, 400);
            window.maxSize = new Vector2(800, 800);
            window.position = new Rect(
                Screen.width / 3,
                Screen.height / 3,
                EditorGitCommitTools.SavedWindowWidth,
                EditorGitCommitTools.SavedWindowHeight
            );
        }

        private void OnEnable()
        {
            commitRecordPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "git提交记录.txt");
            commitType = EditorGitCommitTools.SavedCommitType;
            timeConsuming = EditorGitCommitTools.SavedTimeConsuming;
            commitInfo = "";
        }

        private void OnDisable()
        {
            EditorGitCommitTools.SavedCommitType = commitType;
            EditorGitCommitTools.SavedTimeConsuming = timeConsuming;
            EditorGitCommitTools.SavedWindowWidth = position.width;
            EditorGitCommitTools.SavedWindowHeight = position.height;
        }

        // ==================== UI ====================

        /// <summary>初始化或获取缓存的 GUIStyle</summary>
        private void EnsureStylesInitialized()
        {
            if (labelStyle != null) return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color32(30, 144, 255, 255) }
            };

            primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                normal = { textColor = new Color32(30, 144, 255, 255) }
            };

            normalButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14
            };

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        }

        private void OnGUI()
        {
            EnsureStylesInitialized();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Git提交工具", titleStyle, GUILayout.Height(30));

            GUILayout.Label("1.提交类型 [可选]：", labelStyle);
            commitType = (CommitType)EditorGUILayout.EnumPopup(commitType);
            GUILayout.Label("2.耗时 [可选]：", labelStyle);
            timeConsuming = EditorGUILayout.TextField(timeConsuming);
            GUILayout.Label("3.日志信息 [必填]：", labelStyle);
            commitInfo = EditorGUILayout.TextField(commitInfo);

            if (GUILayout.Button("发射!", normalButtonStyle, GUILayout.Height(30)))
            {
                CommitGit();
            }
            GUILayout.Space(5);
            if (GUILayout.Button("4.发射&推送!", primaryButtonStyle, GUILayout.Height(30)))
            {
                CommitGit(true);
            }

            GUILayout.Space(20);
            GUILayout.Label("其他功能====================");

            if (GUILayout.Button("查看当前分支网页", normalButtonStyle, GUILayout.Height(30)))
            {
                string branchUrl = GetBranchUrl();
                if (!string.IsNullOrEmpty(branchUrl))
                    Application.OpenURL(branchUrl);
            }

            GUILayout.Space(5);
            if (GUILayout.Button("打开今天的提交记录", normalButtonStyle, GUILayout.Height(30)))
            {
                EnsureRecordFileExists();
                Application.OpenURL(commitRecordPath);
            }

            EditorGUILayout.EndScrollView();

            // 快捷键：Ctrl+Enter 触发提交&推送
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.control
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                CommitGit(true);
                e.Use();
            }
        }

        // ==================== 提交逻辑 ====================

        /// <summary>执行 git add → commit → (可选) push，并记录日志</summary>
        private void CommitGit(bool isPush = false)
        {
            // 1. 确定耗时
            string timeStr;
            if (string.IsNullOrEmpty(timeConsuming))
            {
                if (!TryCalculateWorkTime(out timeStr))
                    return;
            }
            else
            {
                timeStr = timeConsuming;
            }

            // 2. 检查是否有变更
            string statusOutput = GitCommandUtil.Run("status");
            UnityEngine.Debug.Log($"[git status:] {statusOutput}");

            if (statusOutput.Contains("working tree clean"))
            {
                UnityEngine.Debug.Log("[不需要提交] 当前没有变更产生。");
                return;
            }

            // 3. add + commit
            string addOutput = GitCommandUtil.Run("add .");
            UnityEngine.Debug.Log($"[git add.:] {addOutput}");

            string fullCommitInfo = BuildCommitMessage(timeStr);
            string commitCmd = "commit -m \"" + EscapeForShell(fullCommitInfo) + "\"";
            string commitOutput = GitCommandUtil.Run(commitCmd);
            UnityEngine.Debug.Log($"[git commit:] {commitOutput}");

            // 4. 保存提交记录到桌面文件
            SaveCommitRecord(fullCommitInfo);

            // 5. 可选 push
            string tips;
            if (isPush)
            {
                string pushOutput = GitCommandUtil.Run("push");
                UnityEngine.Debug.Log($"[git push:] {pushOutput}");
                tips = "[提交&上传操作]，请留意控制台输出情况!";
            }
            else
            {
                tips = "[提交操作]，请留意控制台输出情况!";
            }

            ShowNotification(new GUIContent(tips));

            // 提交成功后清空输入，方便下次使用
            commitInfo = "";
            GUI.FocusControl(null);
            Repaint();
        }

        /// <summary>根据提交类型和耗时，拼接完整的 commit message</summary>
        private string BuildCommitMessage(string timeStr)
        {
            string template = commitTemplates[(int)commitType];
            string info = string.IsNullOrEmpty(commitInfo)
                ? Enum.GetName(typeof(CommitType), commitType)
                : commitInfo;
            return template.Replace("&time", timeStr).Replace("&info", info);
        }

        /// <summary>对 shell 参数中的双引号进行转义，防止 commit message 含引号时命令断裂</summary>
        private static string EscapeForShell(string input)
        {
            return input.Replace("\"", "\\\"");
        }

        // ==================== 时间计算 ====================

        /// <summary>
        /// 根据上次 commit 时间与当前时间，自动计算工作时长（扣除午休 13:00-14:30）。
        /// </summary>
        private bool TryCalculateWorkTime(out string timeDescription)
        {
            timeDescription = string.Empty;

            // 解析上次 commit 时间
            string lastCommitRaw = GetLastCommitDateTime();
            UnityEngine.Debug.Log($"[git lastCommitDateTime:] {lastCommitRaw}");

            if (!DateTime.TryParse(lastCommitRaw, out DateTime lastCommitTime))
            {
                UnityEngine.Debug.LogError($"[GitCommit] 无法解析上次提交时间：{lastCommitRaw}");
                return false;
            }

            DateTime now = DateTime.Now;

            if (now.Date < lastCommitTime.Date)
            {
                UnityEngine.Debug.LogError("当前电脑日期小于最后提交日期，请检查电脑时间是否正确。");
                return false;
            }

            // 跨天：以上午 9:00 作为起始时间
            DateTime startTime = now.Date > lastCommitTime.Date
                ? now.Date.AddHours(8.5)
                : lastCommitTime;

            TimeSpan worked = now - startTime;

            // 扣除午休：工作开始时间在午休前，且当前时间在午休后，说明跨过了午休
            if (startTime.TimeOfDay < LunchBreakStart && now.TimeOfDay > LunchBreakEnd)
            {
                worked -= LunchBreakDuration;
            }

            if (worked.TotalMinutes <= 0)
            {
                UnityEngine.Debug.LogError("计算的工作时长为零或负数，请检查时间。");
                return false;
            }

            int totalMinutes = (int)worked.TotalMinutes;
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;

            timeDescription = hours > 0
                ? $"{hours}小时{minutes}分钟"
                : $"{minutes}分钟";

            return true;
        }

        /// <summary>获取上次 commit 的日期时间字符串（git log --date=iso）</summary>
        private static string GetLastCommitDateTime()
        {
            string output = GitCommandUtil.Run("log -1 --date=iso");
            foreach (string line in output.Split('\n'))
            {
                if (line.Contains("Date:"))
                {
                    // git 输出格式: "Date:   2024-01-15 14:30:00 +0800"
                    return line.Replace("Date:", "").Trim();
                }
            }
            return string.Empty;
        }

        // ==================== 提交记录文件 ====================

        /// <summary>确保记录文件存在</summary>
        private void EnsureRecordFileExists()
        {
            if (!File.Exists(commitRecordPath))
            {
                UnityEngine.Debug.Log("git提交记录.txt 不存在，已创建。");
                File.WriteAllText(commitRecordPath, "");
            }
        }

        /// <summary>将本次提交信息追加到桌面日志文件</summary>
        private void SaveCommitRecord(string commitMessage)
        {
            EnsureRecordFileExists();

            string content = File.ReadAllText(commitRecordPath);
            string dayHeader = "git 提交日志：" + DateTime.Now.ToString("yyyy年MM月dd日");

            // 如果当天日志头不存在，追加
            if (!content.Contains(dayHeader))
            {
                content = string.IsNullOrEmpty(content)
                    ? dayHeader
                    : content + "\n" + dayHeader;
            }

            // 在分支标签前插入本次提交，如果没有标签则追加
            string branchTag = GetBranchUrl();
            if (!string.IsNullOrEmpty(branchTag) && content.Contains(branchTag))
            {
                int tagIndex = content.LastIndexOf(branchTag);
                content = content.Insert(tagIndex, commitMessage + "\n");
            }
            else
            {
                content = content + "\n" + commitMessage + "\n" + branchTag + "\n";
            }

            File.WriteAllText(commitRecordPath, content);
        }

        // ==================== Git 信息查询 ====================

        /// <summary>
        /// 获取当前分支的远程仓库网页地址。
        /// master 分支直接返回 remote URL；其他分支返回 /src/branch/{branchName}。
        /// </summary>
        private string GetBranchUrl()
        {
            string remoteUrl = GetRemoteUrl();
            if (string.IsNullOrEmpty(remoteUrl)) return string.Empty;

            string branch = GetLocalBranch();
            if (branch.Equals("master"))
                return remoteUrl;

            // 去掉 .git 后缀，拼接分支路径
            int gitSuffixIndex = remoteUrl.LastIndexOf(".git");
            string baseUrl = gitSuffixIndex >= 0
                ? remoteUrl.Remove(gitSuffixIndex)
                : remoteUrl;
            return baseUrl + "/src/branch/" + branch;
        }

        /// <summary>获取 git remote push 地址</summary>
        private static string GetRemoteUrl()
        {
            string output = GitCommandUtil.Run("remote -v");
            UnityEngine.Debug.Log($"[git remote:] {output}");

            foreach (string line in output.Split('\n'))
            {
                // 取 push 行
                if (line.Contains("(push)"))
                {
                    int httpIndex = line.IndexOf("http");
                    if (httpIndex >= 0)
                    {
                        string url = line.Substring(httpIndex).Replace("(push)", "").Trim();
                        UnityEngine.Debug.Log($"remoteUrl = {url}");
                        return url;
                    }
                }
            }

            UnityEngine.Debug.LogError("获取 git 远程地址失败！");
            return string.Empty;
        }

        /// <summary>获取当前本地分支名</summary>
        private static string GetLocalBranch()
        {
            string output = GitCommandUtil.Run("symbolic-ref -q --short HEAD");
            UnityEngine.Debug.Log($"[git branch:] {output}");
            return output.Trim();
        }
    }

    /// <summary>
    /// Git 命令行工具，封装 Process 调用。
    /// </summary>
    public static class GitCommandUtil
    {
        /// <summary>执行 git 命令并返回标准输出</summary>
        public static string Run(string arguments)
        {
            var startInfo = new ProcessStartInfo("git", arguments)
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = Application.dataPath,
            };

            using (var process = Process.Start(startInfo))
            {
                return process.StandardOutput.ReadToEnd();
            }
        }
    }
}
