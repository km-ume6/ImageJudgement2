namespace ImageJudgement2
{
    /// <summary>
    /// ヘルプダイアログ
    /// </summary>
    public class HelpDialog : Form
    {
        private const int DEFAULT_WIDTH = 800;
        private const int DEFAULT_HEIGHT = 600;
        private const int FONT_SIZE = 10;

        public HelpDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "ImageJudgement2 ヘルプ";
            Width = DEFAULT_WIDTH;
            Height = DEFAULT_HEIGHT;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = true;

            var textBox = CreateHelpTextBox();
            Controls.Add(textBox);

            // フォーム表示後に選択を解除
            Shown += OnFormShown;
        }

        private TextBox CreateHelpTextBox()
        {
            return new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", FONT_SIZE),
                Text = GetHelpText(),
                TabStop = false,
                HideSelection = true,
                BackColor = SystemColors.Window,
                BorderStyle = BorderStyle.None
            };
        }

        private void OnFormShown(object? sender, EventArgs e)
        {
            try
            {
                // テキストボックスの選択を解除
                if (Controls.Count > 0 && Controls[0] is TextBox textBox)
                {
                    textBox.SelectionStart = 0;
                    textBox.SelectionLength = 0;
                }

                // フォーカスを解除
                ActiveControl = null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"フォーム表示後の処理でエラー: {ex.Message}");
            }
        }

        private static string GetHelpText()
        {
            return @"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 ImageJudgement2 ヘルプ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

【概要】
  画像から円や矩形を検出し、AIモデルで判定を行うアプリケーションです。
  画像の品質検査や分類作業を効率化します。

【主要機能】

■ フォルダ管理
  ・フォルダ追加
    画像フォルダをツリービューに追加します。
    
  ・フォルダ削除
    選択したルートフォルダを一覧から削除します。
    ※ディスク上のフォルダ自体は削除されません。

■ 画像判定
  ・判定実施
    選択した画像に対して円検出とAI判定を実行します。
    結果は右下パネルに表示されます。
    
  ・自動連続判定
    選択フォルダ内の全画像を連続で判定します。
    進捗ダイアログで処理状況を確認できます。
    
  ・判定モード比較
    3つの判定モード（TopClass、ScoreAverage、ScoreRanking）で
    比較し、レポートを表示します。

■ 検出機能
  ・円検出
    画像から円形の領域を検出します。
    最小/最大半径、検出パラメータを調整可能です。
    
  ・矩形検出
    画像から矩形領域を検出します。
    面積やエッジ検出の閾値を調整可能です。

■ その他
  ・OCRテスト
    OCR前処理の動作確認を行います。
    
  ・設定
    各種検出パラメータ、AI接続情報、ウィンドウ配置等を設定します。
    設定はエクスポート/インポート可能です。

【使い方】

1. フォルダの追加
   「フォルダ追加」ボタンをクリックし、画像フォルダを選択します。

2. 画像の選択
   左のツリービューでフォルダを選択すると、
   画像一覧が表示されます。

3. 判定の実行
   画像を選択し、「判定実施」ボタンをクリックします。
   結果が右下のテキストエリアに表示されます。

4. 設定の調整
   「設定」ボタンから各種パラメータを調整できます。
   ・レイアウト比率
   ・検出パラメータ（円・矩形）
   ・AI API設定
   ・データベース設定

【判定モード】

■ TopClass モード
  最もスコアの高いクラスのカテゴリ名で判定します。

■ ScoreAverage モード
  OK/NGカテゴリのスコア平均値で判定します。

■ ScoreRanking モード（推奨）
  OK/NGカテゴリのランキング合計で判定します。
  より安定した判定結果が得られます。

【設定の保存】

設定は以下の場所に自動保存されます：
  %LocalAppData%\AOI-ImageProcessor\AppSettings.json

エクスポート/インポート機能を使用して、
設定を他の環境に移行することも可能です。

【注意事項】

・AI判定APIのキーやモデルIDが設定されていない場合、
  AI判定は実行されません。

・大量の画像を処理する場合、自動連続判定の使用を推奨します。

・一時ファイルの削除に失敗しても処理は継続されます。

・データベース機能を使用する場合は、SQL Server認証が必要です。

【サポート】

問題が発生した場合は、ログファイルを確認してください：
  %LocalAppData%\AOI-ImageProcessor\app_YYYYMMDD.log

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
";
        }
    }
}
