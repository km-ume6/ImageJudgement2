using System;
using System.Drawing;
using System.Windows.Forms;

namespace ImageJudgement2
{
    /// <summary>
    /// フォルダ復元の進捗を表示するダイアログ
    /// </summary>
    public class FolderRestoreProgressDialog : Form
    {
        private Label lblMessage;
        private Label lblProgress;
        private ProgressBar progressBar;
        private Label lblDetail;

        public FolderRestoreProgressDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "フォルダを復元中...";
            this.Size = new Size(515, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false; // 閉じるボタンを無効化
            this.TopMost = true; // 最前面に表示

            // メインメッセージ
            lblMessage = new Label
            {
                Text = "前回開いていたフォルダを復元しています...",
                Location = new Point(20, 20),
                Size = new Size(460, 30),
                Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 進捗表示
            lblProgress = new Label
            {
                Text = "準備中...",
                Location = new Point(20, 60),
                Size = new Size(460, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // プログレスバー
            progressBar = new ProgressBar
            {
                Location = new Point(20, 90),
                Size = new Size(460, 25),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            // 詳細情報
            lblDetail = new Label
            {
                Text = "しばらくお待ちください...",
                Location = new Point(20, 125),
                Size = new Size(460, 20),
                ForeColor = SystemColors.GrayText,
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.Controls.Add(lblMessage);
            this.Controls.Add(lblProgress);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblDetail);
        }

        /// <summary>
        /// 進捗メッセージを更新
        /// </summary>
        public void UpdateProgress(string message)
        {
            if (lblProgress.InvokeRequired)
            {
                lblProgress.Invoke(new Action(() => lblProgress.Text = message));
            }
            else
            {
                lblProgress.Text = message;
            }
        }

        /// <summary>
        /// 進捗率を更新
        /// </summary>
        public void UpdateProgressBar(int current, int total)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() =>
                {
                    if (total > 0)
                    {
                        int percentage = (int)((current / (double)total) * 100);
                        progressBar.Value = Math.Min(percentage, 100);
                    }
                }));
            }
            else
            {
                if (total > 0)
                {
                    int percentage = (int)((current / (double)total) * 100);
                    progressBar.Value = Math.Min(percentage, 100);
                }
            }
        }

        /// <summary>
        /// 詳細情報を更新
        /// </summary>
        public void UpdateDetail(string detail)
        {
            if (lblDetail.InvokeRequired)
            {
                lblDetail.Invoke(new Action(() => lblDetail.Text = detail));
            }
            else
            {
                lblDetail.Text = detail;
            }
        }

        /// <summary>
        /// 完了メッセージを表示
        /// </summary>
        public void SetCompleted()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    lblProgress.Text = "復元が完了しました";
                    progressBar.Value = 100;
                    lblDetail.Text = "";
                }));
            }
            else
            {
                lblProgress.Text = "復元が完了しました";
                progressBar.Value = 100;
                lblDetail.Text = "";
            }
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        public void SetError(string error)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    lblProgress.Text = "復元に失敗しました";
                    lblProgress.ForeColor = Color.Red;
                    lblDetail.Text = error;
                    lblDetail.ForeColor = Color.Red;
                }));
            }
            else
            {
                lblProgress.Text = "復元に失敗しました";
                lblProgress.ForeColor = Color.Red;
                lblDetail.Text = error;
                lblDetail.ForeColor = Color.Red;
            }
        }

        /// <summary>
        /// ダイアログを安全に閉じる
        /// </summary>
        public void CloseDialog()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => this.Close()));
            }
            else
            {
                this.Close();
            }
        }
    }
}
