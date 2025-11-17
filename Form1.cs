namespace ImageJudgement2
{
    /// <summary>
    /// メインフォーム - メインレイアウトとフィールド定義
    /// </summary>
    public partial class Form1 : Form
    {
        // ratio fields
        private double leftRatio = 0.5;
        private double rightTopRatio = 0.5;
        private double leftTopRatio = 0.5;
        private double rightBottomRatio = 0.5; // 右下パネルの左右分割比率

        // UI controls for settings (non-visible, used for storing values)
        private FlowLayoutPanel settingsPanel;
        private NumericUpDown nudLeftRatio;
        private NumericUpDown nudRightTopRatio;
        private NumericUpDown nudLeftTopRatio;
        private Button btnAddNetworkShare;
        private Button btnRemoveFolder;
        private Button btnDetectCircles;
        private CheckBox chkAutoDetect;
        private NumericUpDown nudMinRadius;
        private NumericUpDown nudMaxRadius;
        private NumericUpDown nudParam1;
        private NumericUpDown nudParam2;
        private NumericUpDown nudMinArea;
        private NumericUpDown nudMaxArea;
        private NumericUpDown nudCannyThreshold1;
        private NumericUpDown nudCannyThreshold2;

        // browser controls
        private Panel leftPanel;
        private Panel leftTopPanel;
        private Panel leftBottomPanel;
        private Panel rightPanel;
        private Panel rightTopPanel;
        private Panel rightBottomPanel;
        private Panel rightBottomLeftPanel; // 右下左パネル（AI判定結果詳細）
        private Panel rightBottomRightPanel; // 右下右パネル（判定結果OK/NG大表示）
        private TreeView treeBrowser;
        private ListView imageListView;
        private ToolTip folderTooltip;
        // 右下表示用テキストボックス
        private TextBox? rightBottomTextBox;

        // track last folder added via FolderBrowserDialog
        private string? lastBrowsedFolderPath;

        // persisted top-level folders
        private List<string> topLevelFolders = new();

        // last selected path in tree
        private string? lastSelectedPath;

        // currently displayed image
        private Image? currentImage;
        private string? selectedImagePath;
        private Image? detectedCircleImage;
        private Image? detectedCompositeImage;

        // AI判定結果
        private AiModelResult? currentAiResult;

        // 共有AIクライアント（ソケット枯渇を防ぐため再利用）
        private AiModelClient? _sharedAiClient;
        private readonly object _aiClientLock = new object();

        // アプリケーション終了用キャンセルトークン
        private readonly CancellationTokenSource _appLifetimeCts = new CancellationTokenSource();

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            // 進行中のAI処理をキャンセル
            _appLifetimeCts?.Cancel();

            // 画像リソースを破棄
            currentImage?.Dispose();
            currentImage = null;
            detectedCircleImage?.Dispose();
            detectedCircleImage = null;
            detectedCompositeImage?.Dispose();
            detectedCompositeImage = null;

            // 共有AIクライアントを同期的に破棄
            lock (_aiClientLock)
            {
                if (_sharedAiClient != null)
                {
                    try
                    {
                        // HttpClientのDispose()は進行中のリクエストの完了を適切に待機する
                        _sharedAiClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AIクライアント破棄エラー: {ex.Message}");
                    }
                    _sharedAiClient = null;
                }
            }

            // 設定を保存
            SaveAppSettings();
        }

        public Form1()
        {
            InitializeComponent();
            DoubleBuffered = true;
            ResizeRedraw = true;

            // デフォルトのウィンドウサイズを設定
            this.Width = 1200;
            this.Height = 800;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 未処理例外ハンドラを追加（例外発生時にも設定を保存）
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // アプリケーション終了時にも設定を保存
            Application.ApplicationExit += Application_ApplicationExit;

            // Windows OCRを初期化
            InitializeWindowsOcr();

            CreateSettingsUI();
            CreateBrowserPanels();

            // 右下のテキストボックスを作成して右下左パネルに追加
            rightBottomTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = SystemFonts.DefaultFont,
                ReadOnly = false,
                WordWrap = false
            };
            if (rightBottomLeftPanel != null)
            {
                rightBottomLeftPanel.Controls.Add(rightBottomTextBox);
            }

            LoadAppSettings();

            UpdateLayout();

            this.Resize += (s, e) => UpdateLayout();
        }

        private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            try
            {
                // 例外が発生しても設定を保存
                SaveAppSettings();
                System.Diagnostics.Debug.WriteLine($"ThreadException: {e.Exception.Message}");
            }
            catch
            {
                // 保存に失敗しても例外を投げない
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                // 例外が発生しても設定を保存
                SaveAppSettings();
                System.Diagnostics.Debug.WriteLine($"UnhandledException: {e.ExceptionObject}");
            }
            catch
            {
                // 保存に失敗しても例外を投げない
            }
        }

        private void Application_ApplicationExit(object? sender, EventArgs e)
        {
            try
            {
                // アプリケーション終了時に設定を保存
                SaveAppSettings();
            }
            catch
            {
                // 保存に失敗しても例外を投げない
            }
        }

        private void UpdateLayout()
        {
            // クライアントサイズが十分に大きくない場合は何もしない（最小化時や初期化前）
            if (this.ClientSize.Width < 100 || this.ClientSize.Height < 100)
            {
                return;
            }

            int top = settingsPanel?.Height ?? 0;
            int clientWidth = Math.Max(0, this.ClientSize.Width);
            int clientHeight = Math.Max(0, this.ClientSize.Height - top);

            int leftWidth = Math.Clamp((int)Math.Round(clientWidth * leftRatio), 50, clientWidth - 50);
            int rightWidth = clientWidth - leftWidth;

            if (leftPanel != null)
            {
                leftPanel.SetBounds(0, top, leftWidth, clientHeight);

                // 左パネルを上下に分割（高さが100以上の場合のみ）
                if (clientHeight >= 100)
                {
                    int leftTopHeight = Math.Clamp((int)Math.Round(clientHeight * leftTopRatio), 50, clientHeight - 50);
                    int leftBottomHeight = clientHeight - leftTopHeight;

                    if (leftTopPanel != null)
                    {
                        leftTopPanel.SetBounds(0, 0, leftWidth, leftTopHeight);
                    }

                    if (leftBottomPanel != null)
                    {
                        leftBottomPanel.SetBounds(0, leftTopHeight, leftWidth, leftBottomHeight);
                    }
                }
                else
                {
                    // 高さが小さすぎる場合は全体を上パネルに割り当てる
                    if (leftTopPanel != null)
                    {
                        leftTopPanel.SetBounds(0, 0, leftWidth, clientHeight);
                    }

                    if (leftBottomPanel != null)
                    {
                        leftBottomPanel.SetBounds(0, clientHeight, leftWidth, 0);
                        leftBottomPanel.Visible = false;
                    }
                }
            }

            if (rightPanel != null)
            {
                rightPanel.SetBounds(leftWidth, top, rightWidth, clientHeight);

                // 右パネルを上下に分割（高さが100以上の場合のみ）
                if (clientHeight >= 100)
                {
                    int rightTopHeight = Math.Clamp((int)Math.Round(clientHeight * rightTopRatio), 50, clientHeight - 50);
                    int rightBottomHeight = clientHeight - rightTopHeight;

                    if (rightTopPanel != null)
                    {
                        rightTopPanel.SetBounds(0, 0, rightWidth, rightTopHeight);
                        rightTopPanel.Visible = true;
                        rightTopPanel.Invalidate();
                    }

                    if (rightBottomPanel != null)
                    {
                        rightBottomPanel.SetBounds(0, rightTopHeight, rightWidth, rightBottomHeight);
                        rightBottomPanel.Visible = true;

                        // 右下パネルを左右に分割（幅が100以上の場合のみ）
                        if (rightWidth >= 100)
                        {
                            int rightBottomLeftWidth = Math.Clamp((int)Math.Round(rightWidth * rightBottomRatio), 50, rightWidth - 50);
                            int rightBottomRightWidth = rightWidth - rightBottomLeftWidth;

                            if (rightBottomLeftPanel != null)
                            {
                                rightBottomLeftPanel.SetBounds(0, 0, rightBottomLeftWidth, rightBottomHeight);
                                rightBottomLeftPanel.Visible = true;
                                rightBottomLeftPanel.Invalidate();
                            }

                            // テキストボックスが存在する場合はサイズを合わせる
                            if (rightBottomTextBox != null && rightBottomLeftPanel != null)
                            {
                                rightBottomTextBox.SetBounds(0, 0, Math.Max(1, rightBottomLeftWidth), Math.Max(1, rightBottomHeight));
                                rightBottomTextBox.Invalidate();
                            }

                            if (rightBottomRightPanel != null)
                            {
                                rightBottomRightPanel.SetBounds(rightBottomLeftWidth, 0, rightBottomRightWidth, rightBottomHeight);
                                rightBottomRightPanel.Visible = true;
                                rightBottomRightPanel.Invalidate();
                            }
                        }
                        else
                        {
                            // 幅が小さすぎる場合は全体を左パネルに割り当てる
                            if (rightBottomLeftPanel != null)
                            {
                                rightBottomLeftPanel.SetBounds(0, 0, rightWidth, rightBottomHeight);
                                rightBottomLeftPanel.Visible = true;
                                rightBottomLeftPanel.Invalidate();
                            }

                            if (rightBottomTextBox != null && rightBottomLeftPanel != null)
                            {
                                rightBottomTextBox.SetBounds(0, 0, Math.Max(1, rightWidth), Math.Max(1, rightBottomHeight));
                                rightBottomTextBox.Invalidate();
                            }

                            if (rightBottomRightPanel != null)
                            {
                                rightBottomRightPanel.SetBounds(rightWidth, 0, 0, rightBottomHeight);
                                rightBottomRightPanel.Visible = false;
                                rightBottomRightPanel.Invalidate();
                            }
                        }
                    }
                }
                else
                {
                    // 高さが小さすぎる場合は全体を上パネルに割り当てる
                    if (rightTopPanel != null)
                    {
                        rightTopPanel.SetBounds(0, 0, rightWidth, clientHeight);
                        rightTopPanel.Visible = true;
                        rightTopPanel.Invalidate();
                    }

                    if (rightBottomPanel != null)
                    {
                        rightBottomPanel.SetBounds(0, clientHeight, rightWidth, 0);
                        rightBottomPanel.Visible = false;
                    }
                }
            }
        }
    }
}
