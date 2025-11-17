namespace ImageJudgement2
{
    /// <summary>
    /// アプリケーション全体で使用される定数
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// UI関連の定数
        /// </summary>
        public static class UI
        {
            public const int DefaultWindowWidth = 1200;
            public const int DefaultWindowHeight = 800;
            public const int SettingsPanelHeight = 50;
            public const int SettingsPanelPadding = 6;
            public const int DefaultButtonHeight = 35;
            public const int MinPanelSize = 50;
            public const int MinClientSize = 100;

            // レイアウト比率のデフォルト値
            public const double DefaultLeftRatio = 0.5;
            public const double DefaultRightTopRatio = 0.5;
            public const double DefaultLeftTopRatio = 0.5;
            public const double DefaultRightBottomRatio = 0.5;
        }

        /// <summary>
        /// ファイルとディレクトリ関連の定数
        /// </summary>
        public static class Files
        {
            public const string SettingsFileName = "AppSettings.json";
            public const string TessdataFolderName = "tessdata";

            // サポートされる画像拡張子
            public static readonly string[] SupportedImageExtensions =
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff"
            };
        }

        /// <summary>
        /// AI処理関連の定数
        /// </summary>
        public static class AI
        {
            public const int MaxImageSize = 1200;
            public const int MaxRetryCount = 10;
            public const int DefaultModelType = 11;
            public const int ApiTimeoutSeconds = 60;
            public const string DefaultApiUrl = "https://us.adfi.karakurai.com/API/ap/vit/online/";
        }

        /// <summary>
        /// データベース関連の定数
        /// </summary>
        public static class Database
        {
            public const int DefaultConnectionTimeout = 30;
            public const int DefaultMaxPoolSize = 100;
            public const int DefaultMinPoolSize = 5;
            public const int DefaultCommandTimeout = 30;
        }

        /// <summary>
        /// 検出処理関連の定数
        /// </summary>
        public static class Detection
        {
            // 円検出のデフォルトパラメータ
            public const int DefaultMinRadius = 10;
            public const int DefaultMaxRadius = 100;
            public const int DefaultParam1 = 100;
            public const int DefaultParam2 = 30;

            // 矩形検出のデフォルトパラメータ
            public const int DefaultMinArea = 100;
            public const int DefaultMaxArea = 10000;
            public const int DefaultCannyThreshold1 = 50;
            public const int DefaultCannyThreshold2 = 150;

            // ツリービュー関連
            public const int DefaultTreeDummyNodeThreshold = 50;
        }

        /// <summary>
        /// OCR処理関連の定数
        /// </summary>
        public static class OCR
        {
            public const int DefaultAdaptiveBlockSize = 31;
            public const double DefaultAdaptiveC = 3.0;
            public const int DefaultScale = 1;
            public const bool DefaultUseClahe = true;
            public const int DefaultMedianKsize = 3;
            public const int DefaultMorphOpenSize = 3;
            public const int DefaultMorphCloseSize = 3;
            public const double DefaultGamma = 1.0;
            public const int DefaultMinComponentArea = 40;
        }

        /// <summary>
        /// ツールチップ関連の定数
        /// </summary>
        public static class ToolTips
        {
            public const int AutoPopDelay = 5000;
            public const int InitialDelay = 500;
            public const int ReshowDelay = 100;
        }

        /// <summary>
        /// その他の定数
        /// </summary>
        public static class Misc
        {
            public const int MaxOutputTruncationSize = 60 * 1024; // 60KB
        }
    }
}
