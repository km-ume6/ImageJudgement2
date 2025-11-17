namespace ImageJudgement2
{
    /// <summary>
    /// フォームのレイアウト管理を担当するクラス
    /// </summary>
    public class LayoutManager
    {
        #region フィールド
        private readonly Form _form;
        private readonly Panel? _settingsPanel;

        // パネル参照
        private readonly Panel? _leftPanel;
        private readonly Panel? _leftTopPanel;
        private readonly Panel? _leftBottomPanel;
        private readonly Panel? _rightPanel;
        private readonly Panel? _rightTopPanel;
        private readonly Panel? _rightBottomPanel;
        private readonly Panel? _rightBottomLeftPanel;
        private readonly Panel? _rightBottomRightPanel;
        private readonly TextBox? _rightBottomTextBox;
        #endregion

        #region プロパティ
        /// <summary>左右分割比率</summary>
        public double LeftRatio { get; set; }

        /// <summary>右パネル上下分割比率</summary>
        public double RightTopRatio { get; set; }

        /// <summary>左パネル上下分割比率</summary>
        public double LeftTopRatio { get; set; }

        /// <summary>右下パネル左右分割比率</summary>
        public double RightBottomRatio { get; set; }
        #endregion

        #region コンストラクタ
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public LayoutManager(
            Form form,
            Panel? settingsPanel,
            Panel? leftPanel,
            Panel? leftTopPanel,
            Panel? leftBottomPanel,
            Panel? rightPanel,
            Panel? rightTopPanel,
            Panel? rightBottomPanel,
            Panel? rightBottomLeftPanel,
            Panel? rightBottomRightPanel,
            TextBox? rightBottomTextBox)
        {
            _form = form ?? throw new ArgumentNullException(nameof(form));
            _settingsPanel = settingsPanel;
            _leftPanel = leftPanel;
            _leftTopPanel = leftTopPanel;
            _leftBottomPanel = leftBottomPanel;
            _rightPanel = rightPanel;
            _rightTopPanel = rightTopPanel;
            _rightBottomPanel = rightBottomPanel;
            _rightBottomLeftPanel = rightBottomLeftPanel;
            _rightBottomRightPanel = rightBottomRightPanel;
            _rightBottomTextBox = rightBottomTextBox;

            // デフォルト比率を設定
            LoadDefaultRatios();
        }
        #endregion

        #region パブリックメソッド
        /// <summary>
        /// レイアウトを更新
        /// </summary>
        public void UpdateLayout()
        {
            // クライアントサイズが異常に小さい場合は何もしない（最小化時や初期化前）
            if (!IsValidClientSize())
                return;

            var (clientWidth, clientHeight, top) = GetLayoutDimensions();
            var leftWidth = CalculateLeftWidth(clientWidth);
            var rightWidth = clientWidth - leftWidth;

            UpdatePanelLayout(leftWidth, rightWidth, clientHeight, top);
        }

        /// <summary>
        /// デフォルト比率を読み込む
        /// </summary>
        public void LoadDefaultRatios()
        {
            LeftRatio = AppConstants.UI.DefaultLeftRatio;
            RightTopRatio = AppConstants.UI.DefaultRightTopRatio;
            LeftTopRatio = AppConstants.UI.DefaultLeftTopRatio;
            RightBottomRatio = AppConstants.UI.DefaultRightBottomRatio;
        }

        /// <summary>
        /// 比率を設定
        /// </summary>
        public void SetRatios(double leftRatio, double leftTopRatio, double rightTopRatio, double rightBottomRatio)
        {
            LeftRatio = Clamp(leftRatio, 0.05, 0.95);
            LeftTopRatio = Clamp(leftTopRatio, 0.05, 0.95);
            RightTopRatio = Clamp(rightTopRatio, 0.05, 0.95);
            RightBottomRatio = Clamp(rightBottomRatio, 0.05, 0.95);
        }
        #endregion

        #region プライベートメソッド - 検証と計算
        /// <summary>
        /// クライアントサイズが有効かチェック
        /// </summary>
        private bool IsValidClientSize()
        {
            return _form.ClientSize.Width >= AppConstants.UI.MinClientSize &&
                   _form.ClientSize.Height >= AppConstants.UI.MinClientSize;
        }

        /// <summary>
        /// レイアウト寸法を取得
        /// </summary>
        private (int width, int height, int top) GetLayoutDimensions()
        {
            int top = _settingsPanel?.Height ?? 0;
            int clientWidth = Math.Max(0, _form.ClientSize.Width);
            int clientHeight = Math.Max(0, _form.ClientSize.Height - top);
            return (clientWidth, clientHeight, top);
        }

        /// <summary>
        /// 左パネルの幅を計算
        /// </summary>
        private int CalculateLeftWidth(int clientWidth)
        {
            return ClampInt(
                (int)Math.Round(clientWidth * LeftRatio),
                AppConstants.UI.MinPanelSize,
                clientWidth - AppConstants.UI.MinPanelSize);
        }

        /// <summary>
        /// パネルレイアウトを更新
        /// </summary>
        private void UpdatePanelLayout(int leftWidth, int rightWidth, int clientHeight, int top)
        {
            UpdateLeftPanelLayout(leftWidth, clientHeight, top);
            UpdateRightPanelLayout(leftWidth, rightWidth, clientHeight, top);
        }
        #endregion

        #region プライベートメソッド - 左パネル
        /// <summary>
        /// 左パネルのレイアウトを更新
        /// </summary>
        private void UpdateLeftPanelLayout(int leftWidth, int clientHeight, int top)
        {
            if (_leftPanel == null) return;

            _leftPanel.SetBounds(0, top, leftWidth, clientHeight);

            if (clientHeight >= AppConstants.UI.MinClientSize)
            {
                var (topHeight, bottomHeight) = CalculateVerticalSplit(clientHeight, LeftTopRatio);
                SetPanelBounds(_leftTopPanel, 0, 0, leftWidth, topHeight, true);
                SetPanelBounds(_leftBottomPanel, 0, topHeight, leftWidth, bottomHeight, true);
            }
            else
            {
                // 高さが小さい場合は全体を上パネルに割り当てる
                SetPanelBounds(_leftTopPanel, 0, 0, leftWidth, clientHeight, true);
                SetPanelBounds(_leftBottomPanel, 0, clientHeight, leftWidth, 0, false);
            }
        }
        #endregion

        #region プライベートメソッド - 右パネル
        /// <summary>
        /// 右パネルのレイアウトを更新
        /// </summary>
        private void UpdateRightPanelLayout(int leftWidth, int rightWidth, int clientHeight, int top)
        {
            if (_rightPanel == null) return;

            _rightPanel.SetBounds(leftWidth, top, rightWidth, clientHeight);

            if (clientHeight >= AppConstants.UI.MinClientSize)
            {
                var (topHeight, bottomHeight) = CalculateVerticalSplit(clientHeight, RightTopRatio);
                UpdateRightTopPanelLayout(rightWidth, topHeight);
                UpdateRightBottomPanelLayout(rightWidth, bottomHeight, topHeight);
            }
            else
            {
                // 高さが小さい場合は全体を上パネルに割り当てる
                SetPanelBounds(_rightTopPanel, 0, 0, rightWidth, clientHeight, true, true);
                SetPanelBounds(_rightBottomPanel, 0, clientHeight, rightWidth, 0, false);
            }
        }

        /// <summary>
        /// 右上パネルのレイアウトを更新
        /// </summary>
        private void UpdateRightTopPanelLayout(int rightWidth, int rightTopHeight)
        {
            SetPanelBounds(_rightTopPanel, 0, 0, rightWidth, rightTopHeight, true, true);
        }

        /// <summary>
        /// 右下パネルのレイアウトを更新
        /// </summary>
        private void UpdateRightBottomPanelLayout(int rightWidth, int rightBottomHeight, int rightTopHeight)
        {
            if (_rightBottomPanel == null) return;

            _rightBottomPanel.SetBounds(0, rightTopHeight, rightWidth, rightBottomHeight);
            _rightBottomPanel.Visible = true;

            if (rightWidth >= AppConstants.UI.MinClientSize)
            {
                var (leftWidth, rightWidthSplit) = CalculateHorizontalSplit(rightWidth, RightBottomRatio);
                UpdateRightBottomLeftPanelLayout(leftWidth, rightBottomHeight);
                UpdateRightBottomRightPanelLayout(leftWidth, rightWidthSplit, rightBottomHeight);
            }
            else
            {
                // 幅が小さい場合は全体を左パネルに割り当てる
                UpdateRightBottomLeftPanelLayout(rightWidth, rightBottomHeight);
                SetPanelBounds(_rightBottomRightPanel, rightWidth, 0, 0, rightBottomHeight, false, true);
            }
        }

        /// <summary>
        /// 右下左パネルのレイアウトを更新
        /// </summary>
        private void UpdateRightBottomLeftPanelLayout(int width, int height)
        {
            SetPanelBounds(_rightBottomLeftPanel, 0, 0, width, height, true, true);

            // テキストボックスが存在する場合はサイズを合わせる
            if (_rightBottomTextBox != null && _rightBottomLeftPanel != null)
            {
                _rightBottomTextBox.SetBounds(0, 0, Math.Max(1, width), Math.Max(1, height));
                _rightBottomTextBox.Invalidate();
            }
        }

        /// <summary>
        /// 右下右パネルのレイアウトを更新
        /// </summary>
        private void UpdateRightBottomRightPanelLayout(int leftWidth, int rightWidth, int height)
        {
            SetPanelBounds(_rightBottomRightPanel, leftWidth, 0, rightWidth, height, true, true);
        }
        #endregion

        #region ユーティリティメソッド
        /// <summary>
        /// 垂直分割の寸法を計算
        /// </summary>
        private (int topHeight, int bottomHeight) CalculateVerticalSplit(int totalHeight, double topRatio)
        {
            int topHeight = ClampInt(
                (int)Math.Round(totalHeight * topRatio),
                AppConstants.UI.MinPanelSize,
                totalHeight - AppConstants.UI.MinPanelSize);
            int bottomHeight = totalHeight - topHeight;
            return (topHeight, bottomHeight);
        }

        /// <summary>
        /// 水平分割の寸法を計算
        /// </summary>
        private (int leftWidth, int rightWidth) CalculateHorizontalSplit(int totalWidth, double leftRatio)
        {
            int leftWidth = ClampInt(
                (int)Math.Round(totalWidth * leftRatio),
                AppConstants.UI.MinPanelSize,
                totalWidth - AppConstants.UI.MinPanelSize);
            int rightWidth = totalWidth - leftWidth;
            return (leftWidth, rightWidth);
        }

        /// <summary>
        /// パネルの境界を設定
        /// </summary>
        private static void SetPanelBounds(
            Panel? panel,
            int x, int y, int width, int height,
            bool visible,
            bool invalidate = false)
        {
            if (panel == null) return;

            panel.SetBounds(x, y, width, height);
            panel.Visible = visible;

            if (invalidate)
                panel.Invalidate();
        }

        /// <summary>
        /// double値を範囲内に制限
        /// </summary>
        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// int値を範囲内に制限
        /// </summary>
        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        #endregion
    }
}
