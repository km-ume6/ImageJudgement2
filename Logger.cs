using System.Runtime.CompilerServices;
using SysDebug = System.Diagnostics.Debug;

namespace ImageJudgement2
{
    /// <summary>
    /// アプリケーション全体で使用するロガー
    /// </summary>
    public static class Logger
    {
        private static readonly object _lockObj = new();
        private static readonly string? _logFilePath;
        private static readonly string _appName = "AOI-ImageProcessor";

        /// <summary>
        /// ログレベル
        /// </summary>
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 静的コンストラクタ：ログファイルのパスを初期化
        /// </summary>
        static Logger()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolder = Path.Combine(appDataPath, _appName);
                Directory.CreateDirectory(appFolder);
                _logFilePath = Path.Combine(appFolder, $"app_{DateTime.Now:yyyyMMdd}.log");
            }
            catch (Exception ex)
            {
                SysDebug.WriteLine($"ログファイルの初期化に失敗: {ex.Message}");
                _logFilePath = null;
            }
        }

        #region パブリックメソッド
        /// <summary>
        /// デバッグログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="memberName">呼び出し元メンバー名（自動設定）</param>
        /// <param name="sourceFilePath">呼び出し元ファイルパス（自動設定）</param>
        /// <param name="sourceLineNumber">呼び出し元行番号（自動設定）</param>
        public static void Debug(
            string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var context = FormatCallerInfo(memberName, sourceFilePath, sourceLineNumber);
            Log(LogLevel.Debug, message, context);
        }

        /// <summary>
        /// 情報ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="memberName">呼び出し元メンバー名（自動設定）</param>
        public static void Info(
            string message,
            [CallerMemberName] string memberName = "")
        {
            Log(LogLevel.Info, message, memberName);
        }

        /// <summary>
        /// 警告ログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="memberName">呼び出し元メンバー名（自動設定）</param>
        public static void Warning(
            string message,
            [CallerMemberName] string memberName = "")
        {
            Log(LogLevel.Warning, message, memberName);
        }

        /// <summary>
        /// エラーログを出力
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        /// <param name="ex">例外オブジェクト</param>
        /// <param name="memberName">呼び出し元メンバー名（自動設定）</param>
        public static void Error(
            string message,
            Exception? ex = null,
            [CallerMemberName] string memberName = "")
        {
            var fullMessage = ex != null
                ? FormatExceptionMessage(message, ex)
                : message;
            Log(LogLevel.Error, fullMessage, memberName);
        }
        #endregion

        #region プライベートメソッド
        /// <summary>
        /// ログを出力
        /// </summary>
        private static void Log(LogLevel level, string message, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var contextInfo = !string.IsNullOrEmpty(context) ? $" [{context}]" : "";
            var logMessage = $"[{timestamp}] [{level}]{contextInfo} {message}";

            // デバッグ出力
            SysDebug.WriteLine(logMessage);

            // ファイル出力
            WriteToFile(logMessage);
        }

        /// <summary>
        /// ファイルにログを書き込む
        /// </summary>
        private static void WriteToFile(string logMessage)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                return;

            try
            {
                lock (_lockObj)
                {
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // ログ出力の失敗はデバッグ出力のみ
                SysDebug.WriteLine($"ログファイル書き込み失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 例外情報をフォーマット
        /// </summary>
        private static string FormatExceptionMessage(string message, Exception ex)
        {
            return $"{message}\n" +
                   $"例外タイプ: {ex.GetType().Name}\n" +
                   $"メッセージ: {ex.Message}\n" +
                   $"スタックトレース: {ex.StackTrace}";
        }

        /// <summary>
        /// 呼び出し元情報をフォーマット
        /// </summary>
        private static string FormatCallerInfo(string memberName, string sourceFilePath, int sourceLineNumber)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            return $"{fileName}.{memberName}:{sourceLineNumber}";
        }
        #endregion

        #region ユーティリティメソッド
        /// <summary>
        /// ログファイルのパスを取得
        /// </summary>
        public static string? GetLogFilePath() => _logFilePath;

        /// <summary>
        /// ログファイルを開く
        /// </summary>
        public static void OpenLogFile()
        {
            if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
            {
                SysDebug.WriteLine("ログファイルが存在しません。");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _logFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                SysDebug.WriteLine($"ログファイルを開けませんでした: {ex.Message}");
            }
        }

        /// <summary>
        /// 古いログファイルを削除（指定日数より古いものを削除）
        /// </summary>
        /// <param name="daysToKeep">保持する日数</param>
        public static void CleanupOldLogs(int daysToKeep = 7)
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolder = Path.Combine(appDataPath, _appName);

                if (!Directory.Exists(appFolder))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(appFolder, "app_*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                            SysDebug.WriteLine($"古いログファイルを削除: {logFile}");
                        }
                        catch
                        {
                            // 削除失敗は無視
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SysDebug.WriteLine($"ログファイルのクリーンアップ失敗: {ex.Message}");
            }
        }
        #endregion
    }
}
