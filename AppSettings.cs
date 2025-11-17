namespace ImageJudgement2
{
    /// <summary>
    /// アプリケーション設定を保持するクラス
    /// </summary>
    public class AppSettings
    {
        #region レイアウト設定
        /// <summary>左右分割比率</summary>
        public double? LeftRatio { get; set; }

        /// <summary>左パネル上下分割比率</summary>
        public double? LeftTopRatio { get; set; }

        /// <summary>右パネル上下分割比率</summary>
        public double? RightTopRatio { get; set; }

        /// <summary>右下パネル左右分割比率</summary>
        public double? RightBottomRatio { get; set; }
        #endregion

        #region ウィンドウ設定
        /// <summary>ウィンドウ幅</summary>
        public int? WindowWidth { get; set; }

        /// <summary>ウィンドウ高さ</summary>
        public int? WindowHeight { get; set; }

        /// <summary>ウィンドウX座標</summary>
        public int? WindowLeft { get; set; }

        /// <summary>ウィンドウY座標</summary>
        public int? WindowTop { get; set; }

        /// <summary>ウィンドウ状態 (0=Normal, 1=Minimized, 2=Maximized)</summary>
        public int? WindowState { get; set; }
        #endregion

        #region フォルダ設定
        /// <summary>最後に参照したフォルダ</summary>
        public string? LastBrowsedFolder { get; set; }

        /// <summary>登録済みトップレベルフォルダ一覧</summary>
        public List<string>? TopLevelFolders { get; set; }

        /// <summary>最後に選択したパス</summary>
        public string? LastSelectedPath { get; set; }
        #endregion

        #region 円検出パラメータ
        /// <summary>最小半径</summary>
        public int? MinRadius { get; set; }

        /// <summary>最大半径</summary>
        public int? MaxRadius { get; set; }

        /// <summary>Canny エッジ検出の高い閾値</summary>
        public int? Param1 { get; set; }

        /// <summary>円中心検出の閾値</summary>
        public int? Param2 { get; set; }
        #endregion

        #region 矩形検出パラメータ
        /// <summary>最小面積</summary>
        public int? MinArea { get; set; }

        /// <summary>最大面積</summary>
        public int? MaxArea { get; set; }

        /// <summary>Cannyエッジ検出の第1閾値</summary>
        public int? CannyThreshold1 { get; set; }

        /// <summary>Cannyエッジ検出の第2閾値</summary>
        public int? CannyThreshold2 { get; set; }
        #endregion

        #region 検出設定
        /// <summary>自動検出を有効化</summary>
        public bool? AutoDetect { get; set; }

        /// <summary>検出モード</summary>
        public JudgeMode? DetectionMode { get; set; }
        #endregion

        #region ツリービュー設定
        /// <summary>サブフォルダ数がこの値以上の場合、全てにダミーノードを追加</summary>
        public int? TreeDummyNodeThreshold { get; set; }

        /// <summary>ツリービューのソート順 (true=昇順, false=降順)</summary>
        public bool? TreeSortAscending { get; set; }
        #endregion

        #region データベース接続設定
        /// <summary>SQLサーバー名</summary>
        public string? DbServer { get; set; }

        /// <summary>データベース名</summary>
        public string? DbName { get; set; }

        /// <summary>ユーザーID</summary>
        public string? DbUserId { get; set; }

        /// <summary>パスワード</summary>
        public string? DbPassword { get; set; }
        #endregion

        #region AI API設定
        /// <summary>APIキー</summary>
        public string? AiApiKey { get; set; }

        /// <summary>AIモデルID</summary>
        public string? AiModelId { get; set; }

        /// <summary>モデルタイプ</summary>
        public int? AiModelType { get; set; }

        /// <summary>API URL</summary>
        public string? AiApiUrl { get; set; }
        #endregion

        #region ヘルパーメソッド
        /// <summary>
        /// デフォルト値を適用した設定を取得
        /// </summary>
        public void ApplyDefaults()
        {
            LeftRatio ??= AppConstants.UI.DefaultLeftRatio;
            LeftTopRatio ??= AppConstants.UI.DefaultLeftTopRatio;
            RightTopRatio ??= AppConstants.UI.DefaultRightTopRatio;
            RightBottomRatio ??= AppConstants.UI.DefaultRightBottomRatio;

            WindowWidth ??= AppConstants.UI.DefaultWindowWidth;
            WindowHeight ??= AppConstants.UI.DefaultWindowHeight;
            WindowState ??= 0; // Normal

            MinRadius ??= AppConstants.Detection.DefaultMinRadius;
            MaxRadius ??= AppConstants.Detection.DefaultMaxRadius;
            Param1 ??= AppConstants.Detection.DefaultParam1;
            Param2 ??= AppConstants.Detection.DefaultParam2;

            MinArea ??= AppConstants.Detection.DefaultMinArea;
            MaxArea ??= AppConstants.Detection.DefaultMaxArea;
            CannyThreshold1 ??= AppConstants.Detection.DefaultCannyThreshold1;
            CannyThreshold2 ??= AppConstants.Detection.DefaultCannyThreshold2;

            AutoDetect ??= true;
            TreeDummyNodeThreshold ??= AppConstants.Detection.DefaultTreeDummyNodeThreshold;
            TreeSortAscending ??= true;

            AiModelType ??= AppConstants.AI.DefaultModelType;
            AiApiUrl ??= AppConstants.AI.DefaultApiUrl;

            DetectionMode ??= JudgeMode.ScoreRanking;

            TopLevelFolders ??= new List<string>();
        }

        /// <summary>
        /// 設定の検証
        /// </summary>
        /// <returns>検証エラーのリスト（エラーがない場合は空リスト）</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (LeftRatio is < 0.05 or > 0.95)
                errors.Add("左分割比率は5%～95%の範囲で設定してください。");

            if (LeftTopRatio is < 0.05 or > 0.95)
                errors.Add("左上下分割比率は5%～95%の範囲で設定してください。");

            if (RightTopRatio is < 0.05 or > 0.95)
                errors.Add("右上下分割比率は5%～95%の範囲で設定してください。");

            if (RightBottomRatio is < 0.05 or > 0.95)
                errors.Add("右下左右分割比率は5%～95%の範囲で設定してください。");

            if (MinRadius is < 1)
                errors.Add("最小半径は1以上を設定してください。");

            if (MaxRadius is < 0)
                errors.Add("最大半径は0以上を設定してください。");

            if (MinArea is < 0)
                errors.Add("最小面積は0以上を設定してください。");

            if (MaxArea is < 0)
                errors.Add("最大面積は0以上を設定してください。");

            return errors;
        }
        #endregion
    }
}
