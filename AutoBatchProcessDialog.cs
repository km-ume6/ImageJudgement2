using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ImageJudgement2
{
    /// <summary>
    /// 自動連続処理用の進捗ダイアログ
    /// </summary>
    public class AutoBatchProcessDialog : Form
    {
        private Label lblStatus;
        private Label lblProgress;
        private Label lblAccuracy; // 正解率表示用ラベル
        private ProgressBar progressBar;
        private Button btnPause;
        private Button btnStop;
        private TextBox txtLog;

        private bool isPaused = false;
        private bool isStopped = false;
        // フォームをプログラムから閉じる際に FormClosing の確認ダイアログを抑制するフラグ
        private bool suppressClosePrompt = false;

        public bool IsPaused => isPaused;
        public bool IsStopped => isStopped;

        public AutoBatchProcessDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "自動連続処理";
            this.Size = new System.Drawing.Size(600, 480); // 高さを30px増やす
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            // 状態ラベル
            lblStatus = new Label
            {
                Text = "処理を開始しています...",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(540, 23),
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };

            // 進捗ラベル
            lblProgress = new Label
            {
                Text = "0 / 0 (0%)",
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(270, 20)
            };

            // 正解率ラベル（新規追加）
            lblAccuracy = new Label
            {
                Text = "正解率: --- (0/0)",
                Location = new System.Drawing.Point(300, 50),
                Size = new System.Drawing.Size(260, 20),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            // プログレスバー
            progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(20, 75),
                Size = new System.Drawing.Size(540, 23),
                Minimum = 0,
                Maximum = 100
            };

            // ログテキスト
            txtLog = new TextBox
            {
                Location = new System.Drawing.Point(20, 110),
                Size = new System.Drawing.Size(540, 260), // 高さを30px増やす
                Multiline = true,
                ScrollBars = ScrollBars.Both, // 縦横両方のスクロールバーを表示
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                WordWrap = false // テキストの折り返しを無効化
            };

            // 一時停止ボタン
            btnPause = new Button
            {
                Text = "一時停止",
                Location = new System.Drawing.Point(340, 390), // Y座標を30px下げる
                Size = new System.Drawing.Size(100, 30)
            };
            btnPause.Click += BtnPause_Click;

            // 停止ボタン
            btnStop = new Button
            {
                Text = "停止",
                Location = new System.Drawing.Point(460, 390), // Y座標を30px下げる
                Size = new System.Drawing.Size(100, 30)
            };
            btnStop.Click += BtnStop_Click;

            // コントロール追加
            this.Controls.Add(lblStatus);
            this.Controls.Add(lblProgress);
            this.Controls.Add(lblAccuracy); // 正解率ラベルを追加
            this.Controls.Add(progressBar);
            this.Controls.Add(txtLog);
            this.Controls.Add(btnPause);
            this.Controls.Add(btnStop);

            // フォーム閉じる時の処理
            this.FormClosing += AutoBatchProcessDialog_FormClosing;
            // 閉じた後にログの保存確認を行う（プログラム/ユーザーいずれの場合も）
            this.FormClosed += AutoBatchProcessDialog_FormClosed;
        }

        private void BtnPause_Click(object? sender, EventArgs e)
        {
            if (isPaused)
            {
                // 再開
                isPaused = false;
                btnPause.Text = "一時停止";
                UpdateStatus("処理を再開しました");
            }
            else
            {
                // 一時停止
                isPaused = true;
                btnPause.Text = "再開";
                UpdateStatus("一時停止中...");
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[BtnStop_Click] 停止ボタンがクリックされました");
            
            var result = MessageBox.Show(
                "本当に処理を中断しますか？",
                "確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                System.Diagnostics.Debug.WriteLine("[BtnStop_Click] ユーザーが中断を確認しました");
                isStopped = true;
                UpdateStatus("処理を中断しています...");
                btnPause.Enabled = false;
                System.Diagnostics.Debug.WriteLine($"[BtnStop_Click] isStopped={isStopped}, isPaused={isPaused}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[BtnStop_Click] ユーザーが中断をキャンセルしました");
            }
        }

        private void AutoBatchProcessDialog_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // ユーザー操作による閉じる動作のときのみ確認を行う（プログラムから閉じる場合は抑制）
            if (!isStopped && e.CloseReason == CloseReason.UserClosing && !suppressClosePrompt)
            {
                var result = MessageBox.Show(
                    "処理中にダイアログを閉じますか？",
                    "確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    isStopped = true;
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

        // フォームが閉じられた後にログ保存を確認する
        private void AutoBatchProcessDialog_FormClosed(object? sender, FormClosedEventArgs e)
        {
            try
            {
                // ログが空でなければ保存を確認
                if (!string.IsNullOrWhiteSpace(txtLog?.Text))
                {
                    var res = MessageBox.Show("処理ログを保存しますか？", "ログの保存", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (res == DialogResult.Yes)
                    {
                        using var sfd = new SaveFileDialog();
                        sfd.Title = "ログを保存";
                        sfd.Filter = "テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*";
                        sfd.FileName = $"autobatch_log_{DateTime.Now:yyyyMMddHHmmss}.txt";
                        if (sfd.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                File.WriteAllText(sfd.FileName, txtLog.Text);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"ログの保存に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
            catch
            {
                // 閉じ処理中の例外は無視
            }
        }

        /// <summary>
        /// ステータスを更新
        /// </summary>
        public void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), message);
                return;
            }

            lblStatus.Text = message;
        }

        /// <summary>
        /// 進捗を更新
        /// </summary>
        public void UpdateProgress(int current, int total)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, int>(UpdateProgress), current, total);
                return;
            }

            int percentage = total > 0 ? (int)((double)current / total * 100) : 0;
            lblProgress.Text = $"{current} / {total} ({percentage}%)";
            progressBar.Value = Math.Min(percentage, 100);
        }

        /// <summary>
        /// 正解率を更新（新規追加）
        /// </summary>
        /// <param name="correctCount">正解数</param>
        /// <param name="labeledCount">正解ラベル付きの総数</param>
        public void UpdateAccuracy(int correctCount, int labeledCount)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, int>(UpdateAccuracy), correctCount, labeledCount);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[UpdateAccuracy] correctCount={correctCount}, labeledCount={labeledCount}");

            if (labeledCount == 0)
            {
                lblAccuracy.Text = "正解率: --- (0/0)";
                lblAccuracy.ForeColor = Color.Gray;
                System.Diagnostics.Debug.WriteLine($"[UpdateAccuracy] ラベルなし → グレー表示");
            }
            else
            {
                double accuracy = (correctCount * 100.0) / labeledCount;
                lblAccuracy.Text = $"正解率: {accuracy:F2}% ({correctCount}/{labeledCount})";
                
                // 精度に応じて色を変更
                if (accuracy >= 90)
                {
                    lblAccuracy.ForeColor = Color.Green;
                    System.Diagnostics.Debug.WriteLine($"[UpdateAccuracy] {accuracy:F2}% → 緑");
                }
                else if (accuracy >= 70)
                {
                    lblAccuracy.ForeColor = Color.DarkOrange;
                    System.Diagnostics.Debug.WriteLine($"[UpdateAccuracy] {accuracy:F2}% → オレンジ");
                }
                else
                {
                    lblAccuracy.ForeColor = Color.Red;
                    System.Diagnostics.Debug.WriteLine($"[UpdateAccuracy] {accuracy:F2}% → 赤");
                }
            }
            
            // ラベルを強制的に再描画
            lblAccuracy.Refresh();
        }

        /// <summary>
        /// ログを追加
        /// </summary>
        public void AddLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AddLog), message);
                return;
            }

            // タイムスタンプなしでシンプルに追加（比較レポート用）
            txtLog.AppendText($"{message}\r\n");
        }

        /// <summary>
        /// 完了状態に設定
        /// </summary>
        public void SetCompleted()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(SetCompleted));
                return;
            }

            isStopped = true; // 完了フラグを設定して確認ダイアログを抑制
            UpdateStatus("すべての処理が完了しました");
            btnPause.Enabled = false;
            btnStop.Enabled = true; // ボタンを有効化
            btnStop.Text = "閉じる";
            btnStop.Click -= BtnStop_Click;
            btnStop.Click += (s, e) => this.Close();
        }

        /// <summary>
        /// 停止状態に設定
        /// </summary>
        public void SetStopped()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(SetStopped));
                return;
            }

            System.Diagnostics.Debug.WriteLine("[SetStopped] 呼び出されました");
            
            isStopped = true; // 停止フラグを設定
            UpdateStatus("処理は中断されました");
            btnPause.Enabled = false;
            btnStop.Enabled = true; // ボタンを有効化
            btnStop.Text = "閉じる";
            
            System.Diagnostics.Debug.WriteLine($"[SetStopped] btnStop.Text='{btnStop.Text}', btnStop.Enabled={btnStop.Enabled}");
            
            btnStop.Click -= BtnStop_Click;
            btnStop.Click += (s, e) => this.Close();
            
            // 強制的にUIを更新
            btnStop.Refresh();
            
            System.Diagnostics.Debug.WriteLine("[SetStopped] 完了");
        }

        /// <summary>
        /// プログラムから強制的に閉じる（ユーザー確認ダイアログを抑制）
        /// </summary>
        public void CloseWithoutPrompt()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(CloseWithoutPrompt));
                return;
            }

            suppressClosePrompt = true;
            this.Close();
        }
    }
}
