using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace ImageJudgement2
{
    /// <summary>
    /// 設定関連の処理
    /// </summary>
    public partial class Form1
    {
        private const string SettingsFileName = "AppSettings.json";
        // AppSettings.json をユーザープロファイル下の専用フォルダに保存（プロジェクト名を使用）
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            // エントリポイントのアセンブル名 または Application.ProductName を使用。どちらも取得できない場合は既定値。
            Assembly.GetEntryAssembly()?.GetName().Name ?? Application.ProductName ?? "AOI-ImageProcessor",
            SettingsFileName);

        // データベース設定を保持するフィールド
        private string dbServer = string.Empty;
        private string dbName = string.Empty;
        private string dbUserId = string.Empty;
        private string dbPassword = string.Empty;

        // AI判定API設定を保持するフィールド
        private string aiApiKey = string.Empty;
        private string aiModelId = string.Empty;
        private int aiModelType = 11;
        private string aiApiUrl = "https://us.adfi.karakurai.com/API/ap/vit/online/";

        // ツリービュー設定を保持するフィールド
        private int treeDummyNodeThreshold = 50;
        private bool treeSortAscending = true;

        // 検出モード: 1=Top Class, 2=Score Average, 3=Score Ranking
        private JudgeMode detectionMode = JudgeMode.ScoreRanking;

        // 設定が完全に読み込まれたかのフラグ
        private bool settingsLoaded = false;

        // OCR 前処理設定（フォームで保持）
        private int ocrAdaptiveBlockSize = 31;
        private double ocrAdaptiveC = 3.0;
        private int ocrScale = 1;
        private bool ocrUseClahe = true;
        private int ocrMedianKsize = 3;
        private int ocrMorphOpenSize = 3;
        private int ocrMorphCloseSize = 3;
        private double ocrGamma = 1.0;
        private int ocrMinComponentArea = 40;

        private NumericUpDown? nudAdaptiveBlockSize;
        private NumericUpDown? nudAdaptiveC;
        private NumericUpDown? nudScale;
        private NumericUpDown? nudMedianKsize;
        private NumericUpDown? nudMorphOpenSize;
        private NumericUpDown? nudMorphCloseSize;
        private NumericUpDown? nudGamma;
        private NumericUpDown? nudMinComponentArea;
        private CheckBox? chkUseClahe;

        private void CreateSettingsUI()
        {
            settingsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false
            };

            // ヘルプボタン
            var btnHelp = new Button { Text = "ヘルプ", AutoSize = true, Height = 35 };
            btnHelp.Click += (s, e) => ShowHelp();
            settingsPanel.Controls.Add(btnHelp);

            // フォルダ追加
            var btnAddNetworkShare = new Button { Text = "フォルダ追加", AutoSize = true, Height = 35 };
            btnAddNetworkShare.Click += (s, e) => BtnAddNetworkShare_Click(s, e);
            settingsPanel.Controls.Add(btnAddNetworkShare);

            // フォルダ削除
            var btnRemoveFolder = new Button { Text = "フォルダ削除", AutoSize = true, Height = 35 };
            btnRemoveFolder.Click += (s, e) => BtnRemoveFolder_Click(s, e);
            settingsPanel.Controls.Add(btnRemoveFolder);

            // 設定ダイアログ
            var btnShowSettings = new Button { Text = "設定", AutoSize = true, Height = 35 };
            btnShowSettings.Click += (s, e) => BtnShowSettings_Click(s, e);
            settingsPanel.Controls.Add(btnShowSettings);

            // 判定実施
            btnDetectCircles = new Button { Text = "判定実施", AutoSize = true, Height = 35 };
            btnDetectCircles.Click += async (s, e) => await AutoDetectCircleAndAiAsync();
            settingsPanel.Controls.Add(btnDetectCircles);

            // 自動連続判定関連ボタン
            btnAutoBatch = new Button { Text = "自動連続判定開始", AutoSize = true, Height = 35 };
            btnAutoBatch.Click += async (s, e) => await StartAutoBatchProcessAsync();
            settingsPanel.Controls.Add(btnAutoBatch);

            btnAutoBatchCompare = new Button { Text = "判定モード比較", AutoSize = true, Height = 35 };
            btnAutoBatchCompare.Click += async (s, e) => await StartAutoBatchComparisonAsync();
            settingsPanel.Controls.Add(btnAutoBatchCompare);

            // OCRテスト
            var btnTestOcr = new Button { Text = "OCRテスト", AutoSize = true, Height = 35 };
            btnTestOcr.Click += async (s, e) => await IsAoiOK(true);
            settingsPanel.Controls.Add(btnTestOcr);

            this.Controls.Add(settingsPanel);
            settingsPanel.BringToFront();

            // 設定用のコントロールを初期化（ダイアログで使用）
            InitializeSettingsControls();
        }

        private void ShowHelp()
        {
            using var dlg = new HelpDialog();
            dlg.ShowDialog(this);
        }

        private void InitializeSettingsControls()
        {
            // 設定値を保持するためのコントロールを初期化（非表示）
            nudLeftRatio = new NumericUpDown { Minimum = 5, Maximum = 95, Value = 50 };
            nudLeftTopRatio = new NumericUpDown { Minimum = 5, Maximum = 95, Value = 50 };
            nudRightTopRatio = new NumericUpDown { Minimum = 5, Maximum = 95, Value = 50 };

            nudMinRadius = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 10 };
            nudMaxRadius = new NumericUpDown { Minimum = 0, Maximum = 5000, Value = 0 };
            nudParam1 = new NumericUpDown { Minimum = 1, Maximum = 300, Value = 100 };
            nudParam2 = new NumericUpDown { Minimum = 1, Maximum = 200, Value = 30 };

            nudMinArea = new NumericUpDown { Minimum = 100, Maximum = 10000000, Value = 1000 };
            nudMaxArea = new NumericUpDown { Minimum = 0, Maximum = 10000000, Value = 0 };
            nudCannyThreshold1 = new NumericUpDown { Minimum = 1, Maximum = 300, Value = 50 };
            nudCannyThreshold2 = new NumericUpDown { Minimum = 1, Maximum = 300, Value = 150 };

            chkAutoDetect = new CheckBox { Checked = true };

            // OCR設定用コントロールを初期化
            nudAdaptiveBlockSize = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 31 };
            nudAdaptiveC = new NumericUpDown { Minimum = -10, Maximum = 10, Value = 3, DecimalPlaces = 1 };
            nudScale = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 1 };
            nudMedianKsize = new NumericUpDown { Minimum = 1, Maximum = 31, Value = 3 };
            nudMorphOpenSize = new NumericUpDown { Minimum = 1, Maximum = 21, Value = 3 };
            nudMorphCloseSize = new NumericUpDown { Minimum = 1, Maximum = 21, Value = 3 };
            nudGamma = new NumericUpDown { Minimum = 0.1M, Maximum = 3.0M, Value = 1.0M, DecimalPlaces = 1 };
            nudMinComponentArea = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 40 };
        }

        private void BtnShowSettings_Click(object? sender, EventArgs e)
        {
            using var dialog = new SettingsDialog();

            // 現在の設定をダイアログに読み込む
            var currentSettings = new AppSettings
            {
                LeftRatio = leftRatio,
                LeftTopRatio = leftTopRatio,
                RightTopRatio = rightTopRatio,
                RightBottomRatio = rightBottomRatio,
                MinRadius = nudMinRadius != null ? (int)nudMinRadius.Value : 10,
                MaxRadius = nudMaxRadius != null ? (int)nudMaxRadius.Value : 0,
                Param1 = nudParam1 != null ? (int)nudParam1.Value : 100,
                Param2 = nudParam2 != null ? (int)nudParam2.Value : 30,
                MinArea = nudMinArea != null ? (int)nudMinArea.Value : 1000,
                MaxArea = nudMaxArea != null ? (int)nudMaxArea.Value : 0,
                CannyThreshold1 = nudCannyThreshold1 != null ? (int)nudCannyThreshold1.Value : 50,
                CannyThreshold2 = nudCannyThreshold2 != null ? (int)nudCannyThreshold2.Value : 150,
                AutoDetect = chkAutoDetect?.Checked ?? true,
                TreeDummyNodeThreshold = treeDummyNodeThreshold,
                TreeSortAscending = treeSortAscending,
                DbServer = dbServer,
                DbName = dbName,
                DbUserId = dbUserId,
                DbPassword = dbPassword,
                AiApiKey = aiApiKey,
                AiModelId = aiModelId,
                AiModelType = aiModelType,
                AiApiUrl = aiApiUrl,
                DetectionMode = detectionMode,
                TopLevelFolders = topLevelFolders
            };

            dialog.LoadSettings(currentSettings);

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // ダイアログから設定を取得（SettingsDialogでは0-1の範囲で返される）
                leftRatio = Math.Clamp(dialog.LeftRatio, 0.05, 0.95);
                leftTopRatio = Math.Clamp(dialog.LeftTopRatio, 0.05, 0.95);
                rightTopRatio = Math.Clamp(dialog.RightTopRatio, 0.05, 0.95);
                rightBottomRatio = Math.Clamp(dialog.RightBottomRatio, 0.05, 0.95);

                if (nudMinRadius != null) nudMinRadius.Value = dialog.MinRadius;
                if (nudMaxRadius != null) nudMaxRadius.Value = dialog.MaxRadius;
                if (nudParam1 != null) nudParam1.Value = dialog.Param1;
                if (nudParam2 != null) nudParam2.Value = dialog.Param2;

                if (nudMinArea != null) nudMinArea.Value = dialog.MinArea;
                if (nudMaxArea != null) nudMaxArea.Value = dialog.MaxArea;
                if (nudCannyThreshold1 != null) nudCannyThreshold1.Value = dialog.CannyThreshold1;
                if (nudCannyThreshold2 != null) nudCannyThreshold2.Value = dialog.CannyThreshold2;

                if (chkAutoDetect != null) chkAutoDetect.Checked = dialog.AutoDetect;

                treeDummyNodeThreshold = dialog.TreeDummyNodeThreshold;
                treeSortAscending = dialog.TreeSortAscending;

                // データベース設定を取得
                dbServer = dialog.DbServer;
                dbName = dialog.DbName;
                dbUserId = dialog.DbUserId;
                dbPassword = dialog.DbPassword;

                // AI設定を取得
                aiApiKey = dialog.AiApiKey;
                aiModelId = dialog.AiModelId;
                aiModelType = dialog.AiModelType;
                aiApiUrl = dialog.AiApiUrl;

                // 検出モードを取得
                detectionMode = dialog.DetectionMode;

                // 登録フォルダを取得
                if (dialog.TopLevelFolders.Count > 0)
                {
                    topLevelFolders.Clear();
                    topLevelFolders.AddRange(dialog.TopLevelFolders);

                    // ツリービューを更新
                    treeBrowser?.Nodes.Clear();
                    foreach (var folder in topLevelFolders)
                    {
                        try
                        {
                            var folderName = Path.GetFileName(folder);
                            if (string.IsNullOrEmpty(folderName))
                            {
                                folderName = folder;
                            }

                            var node = new TreeNode(folderName) { Tag = folder };
                            if (HasAnyChild(folder))
                            {
                                node.Nodes.Add(new TreeNode());
                            }
                            else
                            {
                                int count = GetImageFileCount(folder);
                                node.Text = $"{folderName} ({count})";
                            }

                            treeBrowser?.Nodes.Add(node);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"トップレベルフォルダ '{folder}' の追加に失敗: {ex.Message}");
                        }
                    }
                }

                // OCR settings
                ocrAdaptiveBlockSize = (int)(nudAdaptiveBlockSize?.Value ?? 31);
                ocrAdaptiveC = (double)(nudAdaptiveC?.Value ?? 3);
                ocrScale = (int)(nudScale?.Value ?? 1);
                ocrUseClahe = chkUseClahe?.Checked ?? true;
                ocrMedianKsize = (int)(nudMedianKsize?.Value ?? 3);
                ocrMorphOpenSize = (int)(nudMorphOpenSize?.Value ?? 3);
                ocrMorphCloseSize = (int)(nudMorphCloseSize?.Value ?? 3);
                ocrGamma = (double)(nudGamma?.Value ?? 1);
                ocrMinComponentArea = (int)(nudMinComponentArea?.Value ?? 40);

                // Store into Form1 fields for persistence
                ocrAdaptiveBlockSize = (int)(nudAdaptiveBlockSize?.Value ?? 31);
                ocrAdaptiveC = (double)(nudAdaptiveC?.Value ?? 3);
                ocrScale = (int)(nudScale?.Value ?? 1);
                ocrUseClahe = chkUseClahe?.Checked ?? true;
                ocrMedianKsize = (int)(nudMedianKsize?.Value ?? 3);
                ocrMorphOpenSize = (int)(nudMorphOpenSize?.Value ?? 3);
                ocrMorphCloseSize = (int)(nudMorphCloseSize?.Value ?? 3);
                ocrGamma = (double)(nudGamma?.Value ?? 1);
                ocrMinComponentArea = (int)(nudMinComponentArea?.Value ?? 40);

                // 設定を保存してレイアウトを更新
                SaveAppSettings();
                UpdateLayout();
            }
        }

        private void BtnAddNetworkShare_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "表示するフォルダを選択してください",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                AddNetworkShare(dlg.SelectedPath);
            }
        }

        private void BtnRemoveFolder_Click(object? sender, EventArgs e)
        {
            if (treeBrowser?.SelectedNode == null)
            {
                MessageBox.Show("削除するフォルダを選択してください。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedNode = treeBrowser.SelectedNode;

            // ルートノードのみ削除可
            if (selectedNode.Parent != null)
            {
                MessageBox.Show("ルートフォルダのみ削除できます。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var path = selectedNode.Tag as string;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var result = MessageBox.Show(
          $"フォルダ '{selectedNode.Text}' を一覧から削除しますか?\n\n注意: フォルダ自体は削除されません。",
               "確認",
         MessageBoxButtons.YesNo,
             MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                RemoveTopLevelFolder(path);
            }
        }

        private void LoadAppSettings()
        {
            try
            {
                // 設定ファイルが存在しない場合はデフォルトを作成して保存
                if (!File.Exists(SettingsFilePath))
                {
                    try
                    {
                        var defaultSettings = new AppSettings
                        {
                            LeftRatio = 0.2,
                            LeftTopRatio = 0.3,
                            RightTopRatio = 0.8,
                            RightBottomRatio = 0.3,
                            LastBrowsedFolder = null,
                            TopLevelFolders = new System.Collections.Generic.List<string>
                            {
                                @"C:\Users\ume6\OneDrive - ume6\Data\AI学習素材",
                                @"\\192.168.1.104\share\08_共有　精密研磨加工部\03 本社検査\01 検査データ\6LT検査データ\EPC032-02(RF360)検査データ"
                            },
                            LastSelectedPath = @"\\192.168.1.104\share\08_共有　精密研磨加工部\03 本社検査\01 検査データ\6LT検査データ\EPC032-02(RF360)検査データ\915161\915161\管理用\AOI\再加工",
                            MinRadius = 190,
                            MaxRadius = 0,
                            Param1 = 100,
                            Param2 = 30,
                            MinArea = 200000,
                            MaxArea = 0,
                            CannyThreshold1 = 50,
                            CannyThreshold2 = 150,
                            AutoDetect = true,
                            TreeDummyNodeThreshold = 50,
                            TreeSortAscending = true,
                            WindowWidth = 1200,
                            WindowHeight = 800,
                            WindowLeft = 360,
                            WindowTop = 170,
                            WindowState = 2,
                            DbServer = "192.168.11.15",
                            DbName = "AOI",
                            DbUserId = "SangoKENSA",
                            DbPassword = "227663m2",
                            AiApiKey = "0250c4ef50104408883ec28fb2292664",
                            AiModelId = "506f30c4-687d-44f5-9d0a-4b8c64c0b588",
                            AiModelType = 11,
                            AiApiUrl = "https://us.adfi.karakurai.com/API/ap/vit/online/",
                            DetectionMode = JudgeMode.TopClass,
                        };

                        var dir = Path.GetDirectoryName(SettingsFilePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        var options = new JsonSerializerOptions { WriteIndented = true };
                        var defaultJson = JsonSerializer.Serialize(defaultSettings, options);
                        File.WriteAllText(SettingsFilePath, defaultJson);

                        // デフォルト値をフィールドに反映
                        leftRatio = 0.2;
                        leftTopRatio = 0.3;
                        rightTopRatio = 0.8;
                        rightBottomRatio = 0.3;

                        topLevelFolders.Clear();
                        topLevelFolders.AddRange(defaultSettings.TopLevelFolders);

                        dbServer = "192.168.11.15";
                        dbName = "AOI";
                        dbUserId = "SangoKENSA";
                        dbPassword = "227663m2";

                        aiApiKey = "0250c4ef50104408883ec28fb2292664";
                        aiModelId = "506f30c4-687d-44f5-9d0a-4b8c64c0b588";
                        aiModelType = 11;
                        aiApiUrl = "https://us.adfi.karakurai.com/API/ap/vit/online/";

                        treeDummyNodeThreshold = 50;

                        detectionMode = JudgeMode.ScoreRanking;

                        // OCR settings
                        ocrAdaptiveBlockSize = 31;
                        ocrAdaptiveC = 3.0;
                        ocrScale = 1;
                        ocrUseClahe = true;
                        ocrMedianKsize = 3;
                        ocrMorphOpenSize = 3;
                        ocrMorphCloseSize = 3;
                        ocrGamma = 1.0;
                        ocrMinComponentArea = 40;

                        settingsLoaded = true;
                        Debug.WriteLine($"設定ファイルが存在しなかったため、デフォルトを作成しました: {SettingsFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Default settings creation failed: {ex}");
                    }

                    return;
                }

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings == null)
                    return;

                // 比率
                if (settings.LeftRatio.HasValue)
                    leftRatio = Math.Clamp(settings.LeftRatio.Value, 0.05, 0.95);
                if (settings.LeftTopRatio.HasValue)
                    leftTopRatio = Math.Clamp(settings.LeftTopRatio.Value, 0.05, 0.95);
                if (settings.RightTopRatio.HasValue)
                    rightTopRatio = Math.Clamp(settings.RightTopRatio.Value, 0.05, 0.95);
                if (settings.RightBottomRatio.HasValue)
                    rightBottomRatio = Math.Clamp(settings.RightBottomRatio.Value, 0.05, 0.95);

                // フォルダ - AddNetworkShareを呼ばずに直接追加（SaveSettingsの重複呼び出しを防止）
                if (settings.TopLevelFolders != null)
                {
                    topLevelFolders.Clear();
                    topLevelFolders.AddRange(settings.TopLevelFolders);

                    // ツリー表示順を適用（設定に従ってソート）
                    if (settings.TreeSortAscending.HasValue && !settings.TreeSortAscending.Value)
                    {
                        topLevelFolders.Sort((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase));
                        treeSortAscending = false;
                    }
                    else
                    {
                        topLevelFolders.Sort(StringComparer.OrdinalIgnoreCase);
                        treeSortAscending = true;
                    }

                    // ツリービューに追加
                    foreach (var folder in topLevelFolders)
                    {
                        try
                        {
                            var folderName = Path.GetFileName(folder);
                            if (string.IsNullOrEmpty(folderName))
                            {
                                folderName = folder;
                            }

                            var node = new TreeNode(folderName) { Tag = folder };
                            // サブフォルダが存在する場合のみダミーノードを追加し、
                            // サブフォルダがない場合は画像ファイル数を表示する
                            try
                            {
                                if (HasAnyChild(folder))
                                {
                                    node.Nodes.Add(new TreeNode()); // 展開可能にするダミーノード
                                }
                                else
                                {
                                    int count = GetImageFileCount(folder);
                                    node.Text = $"{folderName} ({count})";
                                }
                            }
                            catch { }

                            treeBrowser.Nodes.Add(node);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"トップレベルフォルダ '{folder}' の追加に失敗: {ex.Message}");
                        }
                    }
                }

                // 最後に選択したパス
                lastSelectedPath = settings.LastSelectedPath;

                // 円検出パラメータ
                if (settings.MinRadius.HasValue && nudMinRadius != null)
                    nudMinRadius.Value = Math.Clamp(settings.MinRadius.Value, (int)nudMinRadius.Minimum, (int)nudMinRadius.Maximum);
                if (settings.MaxRadius.HasValue && nudMaxRadius != null)
                    nudMaxRadius.Value = Math.Clamp(settings.MaxRadius.Value, (int)nudMaxRadius.Minimum, (int)nudMaxRadius.Maximum);
                if (settings.Param1.HasValue && nudParam1 != null)
                    nudParam1.Value = Math.Clamp(settings.Param1.Value, (int)nudParam1.Minimum, (int)nudParam1.Maximum);
                if (settings.Param2.HasValue && nudParam2 != null)
                    nudParam2.Value = Math.Clamp(settings.Param2.Value, (int)nudParam2.Minimum, (int)nudParam2.Maximum);

                // 矩形検出パラメータ
                if (settings.MinArea.HasValue && nudMinArea != null)
                    nudMinArea.Value = Math.Clamp(settings.MinArea.Value, (int)nudMinArea.Minimum, (int)nudMinArea.Maximum);
                if (settings.MaxArea.HasValue && nudMaxArea != null)
                    nudMaxArea.Value = Math.Clamp(settings.MaxArea.Value, (int)nudMaxArea.Minimum, (int)nudMaxArea.Maximum);
                if (settings.CannyThreshold1.HasValue && nudCannyThreshold1 != null)
                    nudCannyThreshold1.Value = Math.Clamp(settings.CannyThreshold1.Value, (int)nudCannyThreshold1.Minimum, (int)nudCannyThreshold1.Maximum);
                if (settings.CannyThreshold2.HasValue && nudCannyThreshold2 != null)
                    nudCannyThreshold2.Value = Math.Clamp(settings.CannyThreshold2.Value, (int)nudCannyThreshold2.Minimum, (int)nudCannyThreshold2.Maximum);

                // 自動検出設定
                if (settings.AutoDetect.HasValue && chkAutoDetect != null)
                    chkAutoDetect.Checked = settings.AutoDetect.Value;

                // ツリービュー設定
                if (settings.TreeDummyNodeThreshold.HasValue)
                    treeDummyNodeThreshold = settings.TreeDummyNodeThreshold.Value;

                // ツリーソート順設定を反映
                if (settings.TreeSortAscending.HasValue)
                    treeSortAscending = settings.TreeSortAscending.Value;

                // Tree sort already applied above when loading TopLevelFolders

                // データベース設定
                dbServer = settings.DbServer ?? string.Empty;
                dbName = settings.DbName ?? string.Empty;
                dbUserId = settings.DbUserId ?? string.Empty;
                dbPassword = settings.DbPassword ?? string.Empty;

                // AI判定API設定
                aiApiKey = settings.AiApiKey ?? string.Empty;
                aiModelId = settings.AiModelId ?? string.Empty;
                aiModelType = settings.AiModelType ?? 11;
                aiApiUrl = settings.AiApiUrl ?? "https://us.adfi.karakurai.com/API/ap/vit/online/";

                // 検出モード
                if (settings.DetectionMode.HasValue)
                    detectionMode = settings.DetectionMode.Value;
                else
                    detectionMode = JudgeMode.ScoreRanking;

                Debug.WriteLine($"データベース設定読み込み: Server={dbServer}, DB={dbName}, User={dbUserId}");

                // ウィンドウサイズと位置を復元
                if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue)
                {
                    this.Width = Math.Max(800, settings.WindowWidth.Value);
                    this.Height = Math.Max(450, settings.WindowHeight.Value);
                }

                if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
                {
                    // 画面外に出ないようにチェック
                    var screenBounds = Screen.PrimaryScreen.WorkingArea;
                    var left = Math.Max(0, Math.Min(settings.WindowLeft.Value, screenBounds.Width - this.Width));
                    var top = Math.Max(0, Math.Min(settings.WindowTop.Value, screenBounds.Height - this.Height));
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new System.Drawing.Point(left, top);
                }

                if (settings.WindowState.HasValue)
                {
                    var state = (FormWindowState)settings.WindowState.Value;
                    if (state == FormWindowState.Maximized || state == FormWindowState.Normal)
                    {
                        this.WindowState = state;
                    }
                }

                settingsLoaded = true;
                Debug.WriteLine("設定の読み込みが完了しました。");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAppSettings failed: {ex}");
            }
        }

        private void SaveAppSettings()
        {
            try
            {
                // 設定が読み込まれる前の保存を防ぐ
                if (!settingsLoaded)
                {
                    Debug.WriteLine("SaveAppSettings: 設定がまだ読み込まれていないため、保存をスキップします。");
                    return;
                }

                // 既存の設定ファイルを読み込んでDB設定とAI設定を保持
                string? existingDbServer = dbServer;
                string? existingDbName = dbName;
                string? existingDbUserId = dbUserId;
                string? existingDbPassword = dbPassword;
                string? existingAiApiKey = aiApiKey;
                string? existingAiModelId = aiModelId;
                int? existingAiModelType = aiModelType;
                string? existingAiApiUrl = aiApiUrl;

                if (File.Exists(SettingsFilePath))
                {
                    try
                    {
                        var existingJson = File.ReadAllText(SettingsFilePath);
                        var existingSettings = JsonSerializer.Deserialize<AppSettings>(existingJson);

                        // 現在のDB設定が空の場合、既存の設定を保持
                        if (string.IsNullOrWhiteSpace(existingDbServer) && !string.IsNullOrWhiteSpace(existingSettings?.DbServer))
                            existingDbServer = existingSettings.DbServer;
                        if (string.IsNullOrWhiteSpace(existingDbName) && !string.IsNullOrWhiteSpace(existingSettings?.DbName))
                            existingDbName = existingSettings.DbName;
                        if (string.IsNullOrWhiteSpace(existingDbUserId) && !string.IsNullOrWhiteSpace(existingSettings?.DbUserId))
                            existingDbUserId = existingSettings.DbUserId;
                        if (string.IsNullOrWhiteSpace(existingDbPassword) && !string.IsNullOrWhiteSpace(existingSettings?.DbPassword))
                            existingDbPassword = existingSettings.DbPassword;

                        // 現在のAI設定が空の場合、既存の設定を保持
                        if (string.IsNullOrWhiteSpace(existingAiApiKey) && !string.IsNullOrWhiteSpace(existingSettings?.AiApiKey))
                            existingAiApiKey = existingSettings.AiApiKey;
                        if (string.IsNullOrWhiteSpace(existingAiModelId) && !string.IsNullOrWhiteSpace(existingSettings?.AiModelId))
                            existingAiModelId = existingSettings.AiModelId;
                        if (existingAiModelType == 0 && existingSettings?.AiModelType.HasValue == true)
                            existingAiModelType = existingSettings.AiModelType.Value;
                        if (string.IsNullOrWhiteSpace(existingAiApiUrl) && !string.IsNullOrWhiteSpace(existingSettings?.AiApiUrl))
                            existingAiApiUrl = existingSettings.AiApiUrl;

                        // Keep detection mode from existing file if current is default/unchanged? We'll prefer current value.
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"既存設定の読み込みエラー: {ex.Message}");
                    }
                }

                // 最小化状態の場合は、通常状態のサイズと位置を保存
                var bounds = this.WindowState == FormWindowState.Normal
                       ? new System.Drawing.Rectangle(this.Location, this.Size)
                       : this.RestoreBounds;

                var settings = new AppSettings
                {
                    LeftRatio = leftRatio,
                    LeftTopRatio = leftTopRatio,
                    RightTopRatio = rightTopRatio,
                    RightBottomRatio = rightBottomRatio,
                    LastBrowsedFolder = lastBrowsedFolderPath,
                    TopLevelFolders = topLevelFolders,
                    LastSelectedPath = lastSelectedPath,
                    MinRadius = nudMinRadius != null ? (int)nudMinRadius.Value : null,
                    MaxRadius = nudMaxRadius != null ? (int)nudMaxRadius.Value : null,
                    Param1 = nudParam1 != null ? (int)nudParam1.Value : null,
                    Param2 = nudParam2 != null ? (int)nudParam2.Value : null,
                    MinArea = nudMinArea != null ? (int)nudMinArea.Value : null,
                    MaxArea = nudMaxArea != null ? (int)nudMaxArea.Value : null,
                    CannyThreshold1 = nudCannyThreshold1 != null ? (int)nudCannyThreshold1.Value : null,
                    CannyThreshold2 = nudCannyThreshold2 != null ? (int)nudCannyThreshold2.Value : null,
                    AutoDetect = chkAutoDetect?.Checked,
                    TreeDummyNodeThreshold = treeDummyNodeThreshold,
                    TreeSortAscending = treeSortAscending,
                    WindowWidth = bounds.Width,
                    WindowHeight = bounds.Height,
                    WindowLeft = bounds.Left,
                    WindowTop = bounds.Top,
                    WindowState = (int)this.WindowState,
                    DbServer = existingDbServer,
                    DbName = existingDbName,
                    DbUserId = existingDbUserId,
                    DbPassword = existingDbPassword,
                    AiApiKey = existingAiApiKey,
                    AiModelId = existingAiModelId,
                    AiModelType = existingAiModelType,
                    AiApiUrl = existingAiApiUrl,
                    DetectionMode = detectionMode,
                };

                Debug.WriteLine($"データベース設定保存: Server={settings.DbServer}, DB={settings.DbName}, User={settings.DbUserId}");

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);

                // ディレクトリが存在しない場合は作成
                try
                {
                    var dir = Path.GetDirectoryName(SettingsFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllText(SettingsFilePath, json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"設定ファイル書き込みエラー: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveAppSettings failed: {ex}");
            }
        }

        /// <summary>
        /// データベースヘルパーのインスタンスを作成
        /// </summary>
        private DatabaseHelper? CreateDatabaseHelper()
        {
            // データベース機能は現在使用しないため無効化
            Debug.WriteLine("データベース接続は無効化されています。");
            return null;

            /* データベース接続が必要な場合は以下のコメントを解除
            if (string.IsNullOrWhiteSpace(dbServer) ||
           string.IsNullOrWhiteSpace(dbName) ||
           string.IsNullOrWhiteSpace(dbUserId))
            {
                Debug.WriteLine("データベース接続情報が設定されていません。");
                return null;
            }

            try
            {
                return new DatabaseHelper(dbServer, dbName, dbUserId, dbPassword);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseHelper作成エラー: {ex.Message}");
                return null;
            }
            */
        }

        /// <summary>
        /// AI判定クライアントのインスタンスを作成
        /// </summary>
        private AiModelClient? CreateAiModelClient()
        {
            if (string.IsNullOrWhiteSpace(aiApiKey) ||
                string.IsNullOrWhiteSpace(aiModelId) ||
                string.IsNullOrWhiteSpace(aiApiUrl))
            {
                Debug.WriteLine("AI判定API接続情報が設定されていません。");
                return null;
            }

            try
            {
                return new AiModelClient(aiApiKey, aiModelId, aiModelType, aiApiUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AiModelClient作成エラー: {ex.Message}");
                return null;
            }
        }

        // AutoDetectCircleAndAiAsync is implemented in another partial file; the button simply calls it
    }
}
