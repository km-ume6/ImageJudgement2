namespace ImageJudgement2
{
    /// <summary>
    /// 設定ダイアログフォーム
    /// </summary>
    public partial class SettingsDialog : Form
    {
        #region 定数定義
        private const int DIALOG_WIDTH = 600;
        private const int DIALOG_HEIGHT = 600;
        private const int BUTTON_WIDTH = 100;
        private const int BUTTON_HEIGHT = 30;
        private const int BOTTOM_PANEL_HEIGHT = 56;
        private const int CONTROL_SPACING = 10;
        private const int TAB_PADDING = 20;
        private const int LABEL_WIDTH = 200;
        private const int CONTROL_WIDTH = 150;
        private const int TEXTBOX_WIDTH = 280;
        #endregion

        #region プロパティ
        // レイアウト比率
        public double LeftRatio { get; set; }
        public double LeftTopRatio { get; set; }
        public double RightTopRatio { get; set; }
        public double RightBottomRatio { get; set; }

        // 円検出パラメータ
        public int MinRadius { get; set; }
        public int MaxRadius { get; set; }
        public int Param1 { get; set; }
        public int Param2 { get; set; }

        // 矩形検出パラメータ
        public int MinArea { get; set; }
        public int MaxArea { get; set; }
        public int CannyThreshold1 { get; set; }
        public int CannyThreshold2 { get; set; }

        // 自動検出設定
        public bool AutoDetect { get; set; }

        // ツリービュー設定
        public int TreeDummyNodeThreshold { get; set; } = 50;
        public bool TreeSortAscending { get; set; } = true;

        // データベース設定
        public string DbServer { get; set; } = string.Empty;
        public string DbName { get; set; } = string.Empty;
        public string DbUserId { get; set; } = string.Empty;
        public string DbPassword { get; set; } = string.Empty;

        // AI API設定
        public string AiApiKey { get; set; } = string.Empty;
        public string AiModelId { get; set; } = string.Empty;
        public int AiModelType { get; set; } = 11;
        public string AiApiUrl { get; set; } = "https://us.adfi.karakurai.com/API/ap/vit/online/";

        // 検出モード
        public JudgeMode DetectionMode { get; set; } = JudgeMode.TopClass;

        // 登録フォルダ一覧
        public List<string> TopLevelFolders { get; set; } = new();

        public event EventHandler<AppSettings>? SettingsApplied;
        #endregion

        #region UI Controls
        private TabControl tabControl = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;

        // Layout tab controls
        private NumericUpDown nudLeftRatio = null!;
        private NumericUpDown nudLeftTopRatio = null!;
        private NumericUpDown nudRightTopRatio = null!;
        private NumericUpDown nudRightBottomRatio = null!;

        // Circle detection tab controls
        private NumericUpDown nudMinRadius = null!;
        private NumericUpDown nudMaxRadius = null!;
        private NumericUpDown nudParam1 = null!;
        private NumericUpDown nudParam2 = null!;

        // Rectangle detection tab controls
        private NumericUpDown nudMinArea = null!;
        private NumericUpDown nudMaxArea = null!;
        private NumericUpDown nudCannyThreshold1 = null!;
        private NumericUpDown nudCannyThreshold2 = null!;

        // Detection settings tab controls
        private CheckBox chkAutoDetect = null!;
        private RadioButton rdoTopClass = null!;
        private RadioButton rdoScoreAverage = null!;
        private RadioButton rdoScoreRanking = null!;

        // TreeView settings tab controls
        private NumericUpDown nudTreeDummyNodeThreshold = null!;
        private CheckBox chkTreeSortAscending = null!;

        // Database settings tab controls
        private TextBox txtDbServer = null!;
        private TextBox txtDbName = null!;
        private TextBox txtDbUserId = null!;
        private TextBox txtDbPassword = null!;
        private Button btnTestConnection = null!;

        // AI API settings tab controls
        private TextBox txtAiApiKey = null!;
        private TextBox txtAiModelId = null!;
        private NumericUpDown nudAiModelType = null!;
        private TextBox txtAiApiUrl = null!;
        #endregion

        public SettingsDialog()
        {
            InitializeComponent();
        }

        #region 初期化
        private void InitializeComponent()
        {
            InitializeDialogProperties();
            InitializeTabControl();

            var bottomPanel = CreateBottomPanel();

            this.Controls.Add(tabControl);
            this.Controls.Add(bottomPanel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void InitializeDialogProperties()
        {
            this.Text = "設定";
            this.Width = DIALOG_WIDTH;
            this.Height = DIALOG_HEIGHT;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
        }

        private void InitializeTabControl()
        {
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(CONTROL_SPACING, CONTROL_SPACING),
                Multiline = false,
                SizeMode = TabSizeMode.Normal
            };

            tabControl.TabPages.Add(CreateTabPage("検出設定", CreateDetectionTab));
            tabControl.TabPages.Add(CreateTabPage("ツリービュー", CreateTreeViewTab));
            tabControl.TabPages.Add(CreateTabPage("AI判定API", CreateAiApiTab));
            tabControl.TabPages.Add(CreateTabPage("レイアウト", CreateLayoutTab));
            tabControl.TabPages.Add(CreateTabPage("データベース", CreateDatabaseTab));
            tabControl.TabPages.Add(CreateTabPage("円検出", CreateCircleTab));
            tabControl.TabPages.Add(CreateTabPage("矩形検出", CreateRectangleTab));
        }

        private TabPage CreateTabPage(string title, Action<TabPage> createContent)
        {
            var tab = new TabPage(title);
            createContent(tab);
            return tab;
        }

        private Panel CreateBottomPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = BOTTOM_PANEL_HEIGHT,
                Padding = new Padding(CONTROL_SPACING)
            };

            var btnExport = new Button
            {
                Text = "エクスポート",
                Width = BUTTON_WIDTH,
                Height = BUTTON_HEIGHT,
                Left = CONTROL_SPACING,
                Top = CONTROL_SPACING
            };
            btnExport.Click += BtnExport_Click;

            var btnImport = new Button
            {
                Text = "インポート",
                Width = BUTTON_WIDTH,
                Height = BUTTON_HEIGHT,
                Left = btnExport.Right + CONTROL_SPACING,
                Top = CONTROL_SPACING
            };
            btnImport.Click += BtnImport_Click;

            btnOK = new Button
            {
                Text = "OK",
                Width = BUTTON_WIDTH,
                Height = BUTTON_HEIGHT,
                DialogResult = DialogResult.OK
            };
            btnOK.Left = this.ClientSize.Width - btnOK.Width * 2 - CONTROL_SPACING * 2;
            btnOK.Top = CONTROL_SPACING;
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "キャンセル",
                Width = BUTTON_WIDTH,
                Height = BUTTON_HEIGHT,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Left = this.ClientSize.Width - btnCancel.Width - CONTROL_SPACING;
            btnCancel.Top = CONTROL_SPACING;

            panel.Controls.AddRange(new Control[] { btnExport, btnImport, btnOK, btnCancel });
            return panel;
        }
        #endregion

        #region タブ作成（ヘルパーメソッド使用）
        private void CreateLayoutTab(TabPage tab)
        {
            var panel = CreateScrollablePanel();
            int yPos = TAB_PADDING;

            nudLeftRatio = AddNumericUpDownRow(panel, ref yPos, "左分割比率 (%):", 5, 95, 1);
            nudLeftTopRatio = AddNumericUpDownRow(panel, ref yPos, "左上下分割比率 (%):", 5, 95, 1);
            nudRightTopRatio = AddNumericUpDownRow(panel, ref yPos, "右上下分割比率 (%):", 5, 95, 1);
            nudRightBottomRatio = AddNumericUpDownRow(panel, ref yPos, "右下左右分割比率 (%):", 5, 95, 1);

            tab.Controls.Add(panel);
        }

        private void CreateCircleTab(TabPage tab)
        {
            var panel = CreateScrollablePanel();
            int yPos = TAB_PADDING;

            nudMinRadius = AddNumericUpDownRow(panel, ref yPos, "最小半径:", 1, 1000, 5);
            nudMaxRadius = AddNumericUpDownRow(panel, ref yPos, "最大半径:", 0, 5000, 10);
            nudParam1 = AddNumericUpDownRow(panel, ref yPos, "Param1:", 1, 300, 10);
            nudParam2 = AddNumericUpDownRow(panel, ref yPos, "Param2:", 1, 200, 5);

            tab.Controls.Add(panel);
        }

        private void CreateRectangleTab(TabPage tab)
        {
            var panel = CreateScrollablePanel();
            int yPos = TAB_PADDING;

            nudMinArea = AddNumericUpDownRow(panel, ref yPos, "最小面積:", 100, 10000000, 100);
            nudMaxArea = AddNumericUpDownRow(panel, ref yPos, "最大面積:", 0, 10000000, 1000);
            nudCannyThreshold1 = AddNumericUpDownRow(panel, ref yPos, "Canny閾値1:", 1, 300, 10);
            nudCannyThreshold2 = AddNumericUpDownRow(panel, ref yPos, "Canny閾値2:", 1, 300, 10);

            tab.Controls.Add(panel);
        }

        private void CreateDetectionTab(TabPage tab)
        {
            var panel = CreateScrollablePanel();
            int yPos = TAB_PADDING;

            chkAutoDetect = new CheckBox
            {
                Text = "自動検出を有効にする",
                Left = TAB_PADDING,
                Top = yPos,
                Width = 200
            };
            panel.Controls.Add(chkAutoDetect);
            yPos += 40;

            var lblMode = new Label
            {
                Text = "検出モード:",
                Left = TAB_PADDING,
                Top = yPos,
                Width = 100
            };
            panel.Controls.Add(lblMode);

            rdoTopClass = new RadioButton
            {
                Text = "Top Class",
                Left = 130,
                Top = yPos,
                Width = 150
            };
            panel.Controls.Add(rdoTopClass);

            rdoScoreAverage = new RadioButton
            {
                Text = "Score Average",
                Left = 130,
                Top = yPos + 24,
                Width = 150
            };
            panel.Controls.Add(rdoScoreAverage);

            rdoScoreRanking = new RadioButton
            {
                Text = "Score Ranking",
                Left = 130,
                Top = yPos + 48,
                Width = 150
            };
            panel.Controls.Add(rdoScoreRanking);

            tab.Controls.Add(panel);
        }

        private void CreateTreeViewTab(TabPage tab)
        {
            var panel = CreateScrollablePanel();
            int yPos = TAB_PADDING;

            nudTreeDummyNodeThreshold = AddNumericUpDownRow(panel, ref yPos, "ダミーノード追加の閾値:", 1, 1000, 10);

            var lblNote = new Label
            {
                Text = "サブフォルダ数がこの値以上の場合、全てのフォルダにダミーノードを追加します。\n" +
                       "それ以外の場合は、サブフォルダが無いフォルダにはダミーノードを追加しません。\n" +
                       "※パフォーマンスとの兼ね合いで調整してください。",
                Left = TAB_PADDING,
                Top = yPos,
                Width = 400,
                Height = 80,
                ForeColor = Color.DarkBlue
            };
            panel.Controls.Add(lblNote);
            yPos += 90;

            var lblSort = new Label
            {
                Text = "フォルダ表示順: 昇順にする",
                Left = TAB_PADDING,
                Top = yPos,
                Width = 300
            };
            chkTreeSortAscending = new CheckBox
            {
                Left = 320,
                Top = yPos - 2,
                Width = 20
            };
            panel.Controls.Add(lblSort);
            panel.Controls.Add(chkTreeSortAscending);

            tab.Controls.Add(panel);
        }

        private void CreateDatabaseTab(TabPage tab)
        {
            var panel = CreateScrollablePanel();
            int yPos = TAB_PADDING;

            txtDbServer = AddTextBoxRow(panel, ref yPos, "サーバー名:");
            txtDbName = AddTextBoxRow(panel, ref yPos, "データベース名:");
            txtDbUserId = AddTextBoxRow(panel, ref yPos, "ユーザーID:");
            txtDbPassword = AddTextBoxRow(panel, ref yPos, "パスワード:", true);

            var lblNote = new Label
            {
                Text = "注意: SQL Server認証、暗号化無しで接続します。",
                Left = TAB_PADDING,
                Top = yPos,
                Width = 400,
                ForeColor = Color.DarkRed
            };
            panel.Controls.Add(lblNote);
            yPos += 30;

            btnTestConnection = new Button
            {
                Text = "接続テスト",
                Left = TAB_PADDING,
                Top = yPos,
                Width = 120,
                Height = BUTTON_HEIGHT
            };
            btnTestConnection.Click += BtnTestConnection_Click;
            panel.Controls.Add(btnTestConnection);

            tab.Controls.Add(panel);
        }

        private void CreateAiApiTab(TabPage tab)
        {
            var panel = CreateScrollablePanel();
            int yPos = TAB_PADDING;

            txtAiApiKey = AddTextBoxRow(panel, ref yPos, "APIキー:");
            txtAiModelId = AddTextBoxRow(panel, ref yPos, "AIモデルID:");

            var lblModelType = new Label
            {
                Text = "モデルタイプ:",
                Left = TAB_PADDING,
                Top = yPos,
                Width = LABEL_WIDTH
            };
            nudAiModelType = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100,
                Value = 11,
                Left = TAB_PADDING + LABEL_WIDTH,
                Top = yPos,
                Width = CONTROL_WIDTH
            };
            panel.Controls.Add(lblModelType);
            panel.Controls.Add(nudAiModelType);
            yPos += 40;

            txtAiApiUrl = AddTextBoxRow(panel, ref yPos, "API URL:");

            var lblNote = new Label
            {
                Text = "注意: AI判定APIを使用するには正しいAPIキーとモデルIDが必要です。",
                Left = TAB_PADDING,
                Top = yPos,
                Width = 400,
                Height = 40,
                ForeColor = Color.DarkRed
            };
            panel.Controls.Add(lblNote);

            tab.Controls.Add(panel);
        }
        #endregion

        #region ヘルパーメソッド
        private Panel CreateScrollablePanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(TAB_PADDING),
                AutoScroll = true
            };
        }

        private NumericUpDown AddNumericUpDownRow(Panel panel, ref int yPos, string labelText,
            decimal minimum, decimal maximum, decimal increment)
        {
            var label = new Label
            {
                Text = labelText,
                Left = TAB_PADDING,
                Top = yPos,
                Width = LABEL_WIDTH
            };

            var numericUpDown = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = 0,
                Increment = increment,
                Left = TAB_PADDING + LABEL_WIDTH,
                Top = yPos,
                Width = CONTROL_WIDTH
            };

            panel.Controls.Add(label);
            panel.Controls.Add(numericUpDown);
            yPos += 40;

            return numericUpDown;
        }

        private TextBox AddTextBoxRow(Panel panel, ref int yPos, string labelText, bool isPassword = false)
        {
            var label = new Label
            {
                Text = labelText,
                Left = TAB_PADDING,
                Top = yPos,
                Width = LABEL_WIDTH
            };

            var textBox = new TextBox
            {
                Left = TAB_PADDING + LABEL_WIDTH,
                Top = yPos,
                Width = TEXTBOX_WIDTH,
                UseSystemPasswordChar = isPassword
            };

            panel.Controls.Add(label);
            panel.Controls.Add(textBox);
            yPos += 40;

            return textBox;
        }
        #endregion

        #region イベントハンドラ
        private void BtnOK_Click(object sender, EventArgs e)
        {
            SaveSettingsToProperties();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnTestConnection_Click(object sender, EventArgs e)
        {
            string connString = $"Server={txtDbServer.Text};Database={txtDbName.Text};" +
                               $"User Id={txtDbUserId.Text};Password={txtDbPassword.Text};TrustServerCertificate=True;";
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connString))
                {
                    connection.Open();
                    MessageBox.Show("接続成功", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"接続失敗: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "JSON ファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"settings_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var settings = CreateAppSettingsFromControls();
                    var json = System.Text.Json.JsonSerializer.Serialize(settings,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(saveDialog.FileName, json);

                    MessageBox.Show("設定をエクスポートしました。", "成功",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エクスポートに失敗しました。\n{ex.Message}", "エラー",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using var openDialog = new OpenFileDialog
            {
                Filter = "JSON ファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
                DefaultExt = "json"
            };

            if (openDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var json = File.ReadAllText(openDialog.FileName);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings != null)
                    {
                        LoadSettings(settings);

                        var result = MessageBox.Show(
                            "設定をインポートしました。\n現在の設定に適用しますか？",
                            "確認",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            SaveSettingsToProperties();
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                    }
                    else
                    {
                        MessageBox.Show("設定ファイルの読み込みに失敗しました。", "エラー",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"インポートに失敗しました。\n{ex.Message}", "エラー",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        #endregion

        #region データ変換メソッド
        private void SaveSettingsToProperties()
        {
            LeftRatio = (double)nudLeftRatio.Value / 100.0;
            LeftTopRatio = (double)nudLeftTopRatio.Value / 100.0;
            RightTopRatio = (double)nudRightTopRatio.Value / 100.0;
            RightBottomRatio = (double)nudRightBottomRatio.Value / 100.0;

            MinRadius = (int)nudMinRadius.Value;
            MaxRadius = (int)nudMaxRadius.Value;
            Param1 = (int)nudParam1.Value;
            Param2 = (int)nudParam2.Value;

            MinArea = (int)nudMinArea.Value;
            MaxArea = (int)nudMaxArea.Value;
            CannyThreshold1 = (int)nudCannyThreshold1.Value;
            CannyThreshold2 = (int)nudCannyThreshold2.Value;

            AutoDetect = chkAutoDetect.Checked;
            TreeDummyNodeThreshold = (int)nudTreeDummyNodeThreshold.Value;
            TreeSortAscending = chkTreeSortAscending.Checked;

            DbServer = txtDbServer.Text;
            DbName = txtDbName.Text;
            DbUserId = txtDbUserId.Text;
            DbPassword = txtDbPassword.Text;

            AiApiKey = txtAiApiKey.Text;
            AiModelId = txtAiModelId.Text;
            AiModelType = (int)nudAiModelType.Value;
            AiApiUrl = txtAiApiUrl.Text;

            DetectionMode = GetSelectedDetectionMode();
        }

        private AppSettings CreateAppSettingsFromControls()
        {
            return new AppSettings
            {
                LeftRatio = (double)nudLeftRatio.Value / 100.0,
                LeftTopRatio = (double)nudLeftTopRatio.Value / 100.0,
                RightTopRatio = (double)nudRightTopRatio.Value / 100.0,
                RightBottomRatio = (double)nudRightBottomRatio.Value / 100.0,
                MinRadius = (int)nudMinRadius.Value,
                MaxRadius = (int)nudMaxRadius.Value,
                Param1 = (int)nudParam1.Value,
                Param2 = (int)nudParam2.Value,
                MinArea = (int)nudMinArea.Value,
                MaxArea = (int)nudMaxArea.Value,
                CannyThreshold1 = (int)nudCannyThreshold1.Value,
                CannyThreshold2 = (int)nudCannyThreshold2.Value,
                AutoDetect = chkAutoDetect.Checked,
                TreeDummyNodeThreshold = (int)nudTreeDummyNodeThreshold.Value,
                TreeSortAscending = chkTreeSortAscending.Checked,
                DbServer = txtDbServer.Text.Trim(),
                DbName = txtDbName.Text.Trim(),
                DbUserId = txtDbUserId.Text.Trim(),
                DbPassword = txtDbPassword.Text,
                AiApiKey = txtAiApiKey.Text.Trim(),
                AiModelId = txtAiModelId.Text.Trim(),
                AiModelType = (int)nudAiModelType.Value,
                AiApiUrl = txtAiApiUrl.Text.Trim(),
                DetectionMode = GetSelectedDetectionMode(),
                TopLevelFolders = TopLevelFolders
            };
        }

        private JudgeMode GetSelectedDetectionMode()
        {
            if (rdoTopClass.Checked) return JudgeMode.TopClass;
            if (rdoScoreAverage.Checked) return JudgeMode.ScoreAverage;
            return JudgeMode.ScoreRanking;
        }

        public void LoadSettings(AppSettings settings)
        {
            if (settings == null) return;

            nudLeftRatio.Value = (decimal)((settings.LeftRatio ?? 0.5) * 100.0);
            nudLeftTopRatio.Value = (decimal)((settings.LeftTopRatio ?? 0.5) * 100.0);
            nudRightTopRatio.Value = (decimal)((settings.RightTopRatio ?? 0.5) * 100.0);
            nudRightBottomRatio.Value = (decimal)((settings.RightBottomRatio ?? 0.5) * 100.0);

            nudMinRadius.Value = settings.MinRadius ?? 10;
            nudMaxRadius.Value = settings.MaxRadius ?? 0;
            nudParam1.Value = settings.Param1 ?? 100;
            nudParam2.Value = settings.Param2 ?? 30;

            nudMinArea.Value = settings.MinArea ?? 1000;
            nudMaxArea.Value = settings.MaxArea ?? 0;
            nudCannyThreshold1.Value = settings.CannyThreshold1 ?? 50;
            nudCannyThreshold2.Value = settings.CannyThreshold2 ?? 150;

            chkAutoDetect.Checked = settings.AutoDetect ?? true;

            nudTreeDummyNodeThreshold.Value = settings.TreeDummyNodeThreshold ?? 50;
            chkTreeSortAscending.Checked = settings.TreeSortAscending ?? true;

            txtDbServer.Text = settings.DbServer ?? string.Empty;
            txtDbName.Text = settings.DbName ?? string.Empty;
            txtDbUserId.Text = settings.DbUserId ?? string.Empty;
            txtDbPassword.Text = settings.DbPassword ?? string.Empty;

            txtAiApiKey.Text = settings.AiApiKey ?? string.Empty;
            txtAiModelId.Text = settings.AiModelId ?? string.Empty;
            nudAiModelType.Value = settings.AiModelType ?? 11;
            txtAiApiUrl.Text = settings.AiApiUrl ?? string.Empty;

            SetDetectionMode(settings.DetectionMode ?? JudgeMode.TopClass);

            if (settings.TopLevelFolders != null && settings.TopLevelFolders.Count > 0)
            {
                TopLevelFolders = new List<string>(settings.TopLevelFolders);
            }
        }

        private void SetDetectionMode(JudgeMode mode)
        {
            rdoTopClass.Checked = mode == JudgeMode.TopClass;
            rdoScoreAverage.Checked = mode == JudgeMode.ScoreAverage;
            rdoScoreRanking.Checked = mode == JudgeMode.ScoreRanking;
        }
        #endregion
    }
}
