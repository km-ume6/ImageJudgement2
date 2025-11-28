namespace ImageJudgement2
{
    /// <summary>
    /// 判定結果の評価ヘルパークラス
    /// </summary>
    public static class JudgementEvaluator
    {
        #region 定数
        private const string OK_LABEL = "OK";
        private const string NG_LABEL = "NG";
        private const string UNKNOWN_LABEL = "Unknown";
        private const string PASS_KEYWORD_JP = "合格";
        private const string FAIL_KEYWORD_JP = "不合格";
        #endregion

        #region 判定結果の検証
        /// <summary>
        /// 判定結果が正解かどうかを判定
        /// </summary>
        /// <param name="judgeResult">AI判定結果（OK/NG/Unknown）</param>
        /// <param name="groundTruth">正解ラベル（OK/NG）</param>
        /// <returns>正解の場合true、不正解または判定不可の場合false</returns>
        public static bool IsCorrect(string? judgeResult, string? groundTruth)
        {
            if (string.IsNullOrEmpty(judgeResult) || string.IsNullOrEmpty(groundTruth))
                return false;

            return judgeResult.Equals(groundTruth, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判定結果が有効かどうかをチェック
        /// </summary>
        /// <param name="judgeResult">判定結果</param>
        /// <returns>OK/NGの場合true、それ以外はfalse</returns>
        public static bool IsValidJudgeResult(string? judgeResult)
        {
            if (string.IsNullOrEmpty(judgeResult))
                return false;

            return judgeResult.Equals(OK_LABEL, StringComparison.OrdinalIgnoreCase) ||
                   judgeResult.Equals(NG_LABEL, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判定結果が「OK」かどうかをチェック
        /// </summary>
        public static bool IsOk(string? judgeResult)
        {
            return judgeResult?.Equals(OK_LABEL, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// 判定結果が「NG」かどうかをチェック
        /// </summary>
        public static bool IsNg(string? judgeResult)
        {
            return judgeResult?.Equals(NG_LABEL, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        /// <summary>
        /// 判定結果が「Unknown」かどうかをチェック
        /// </summary>
        public static bool IsUnknown(string? judgeResult)
        {
            return string.IsNullOrEmpty(judgeResult) ||
                   judgeResult.Equals(UNKNOWN_LABEL, StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region 正解ラベルの抽出
        /// <summary>
        /// フルパスから正解ラベル（OK/NG）を抽出
        /// </summary>
        /// <param name="fullPath">画像ファイルのフルパス</param>
        /// <returns>正解ラベル（OK/NG）、またはnull（ラベルなし）</returns>
        public static string? ExtractGroundTruthFromPath(string? fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return null;

            var pathDir = Path.GetDirectoryName(fullPath);
			if (string.IsNullOrEmpty(pathDir))
                return null;

			// 「不合格」が含まれていればNG（優先度高）
			if (pathDir.Contains(FAIL_KEYWORD_JP, StringComparison.OrdinalIgnoreCase))
                return NG_LABEL;

            // 「合格」が含まれていればOK
            if (pathDir.Contains(PASS_KEYWORD_JP, StringComparison.OrdinalIgnoreCase))
                return OK_LABEL;

            // "NG"が含まれていればNG
            if (pathDir.Contains(NG_LABEL, StringComparison.OrdinalIgnoreCase))
                return NG_LABEL;

            // "OK"が含まれていればOK
            if (pathDir.Contains(OK_LABEL, StringComparison.OrdinalIgnoreCase))
                return OK_LABEL;

            // どれも含まれていなければnull（判定不要）
            return null;
        }

        /// <summary>
        /// 正解ラベルが存在するかどうかをチェック
        /// </summary>
        /// <param name="fullPath">画像ファイルのフルパス</param>
        /// <returns>正解ラベルが存在する場合true</returns>
        public static bool HasGroundTruth(string? fullPath)
        {
            return !string.IsNullOrEmpty(ExtractGroundTruthFromPath(fullPath));
        }
        #endregion

        #region 精度計算
        /// <summary>
        /// 精度を計算
        /// </summary>
        /// <param name="correctCount">正解数</param>
        /// <param name="totalCount">総数</param>
        /// <returns>精度（パーセント）</returns>
        public static double CalculateAccuracy(int correctCount, int totalCount)
        {
            if (totalCount <= 0)
                return 0.0;

            return (correctCount * 100.0) / totalCount;
        }

        /// <summary>
        /// 精度情報をフォーマット
        /// </summary>
        /// <param name="correctCount">正解数</param>
        /// <param name="totalCount">総数</param>
        /// <returns>フォーマット済み文字列（例: "85.50% (17/20)"）</returns>
        public static string FormatAccuracy(int correctCount, int totalCount)
        {
            var accuracy = CalculateAccuracy(correctCount, totalCount);
            return $"{accuracy:F2}% ({correctCount}/{totalCount})";
        }
        #endregion

        #region 判定結果の比較と表示
        /// <summary>
        /// 判定結果と正解ラベルを比較して、表示用の文字列を生成
        /// </summary>
        /// <param name="judgeResult">AI判定結果</param>
        /// <param name="groundTruth">正解ラベル</param>
        /// <returns>比較結果の表示文字列</returns>
        public static string GetComparisonText(string? judgeResult, string? groundTruth)
        {
            if (string.IsNullOrEmpty(groundTruth))
                return string.Empty;

            if (string.IsNullOrEmpty(judgeResult))
                return $"✗ (正解: {groundTruth})";

            return IsCorrect(judgeResult, groundTruth)
                ? "✓ (正解)"
                : $"✗ (正解: {groundTruth})";
        }

        /// <summary>
        /// 判定結果の記号を取得（✓ または ✗）
        /// </summary>
        /// <param name="judgeResult">AI判定結果</param>
        /// <param name="groundTruth">正解ラベル</param>
        /// <returns>正解なら✓、不正解なら✗</returns>
        public static string GetComparisonSymbol(string? judgeResult, string? groundTruth)
        {
            if (string.IsNullOrEmpty(groundTruth))
                return string.Empty;

            return IsCorrect(judgeResult, groundTruth) ? "✓" : "✗";
        }
        #endregion

        #region 統計情報
        /// <summary>
        /// 判定統計情報
        /// </summary>
        public class JudgementStatistics
        {
            /// <summary>総処理件数</summary>
            public int TotalCount { get; set; }

            /// <summary>正解ラベル付き件数</summary>
            public int LabeledCount { get; set; }

            /// <summary>正解数</summary>
            public int CorrectCount { get; set; }

            /// <summary>不正解数</summary>
            public int IncorrectCount { get; set; }

            /// <summary>エラー数</summary>
            public int ErrorCount { get; set; }

            /// <summary>精度（パーセント）</summary>
            public double Accuracy => CalculateAccuracy(CorrectCount, LabeledCount);

            /// <summary>
            /// 統計情報をフォーマットして文字列として返す
            /// </summary>
            public string Format()
            {
                var result = $"総処理件数: {TotalCount}\n";

                if (LabeledCount > 0)
                {
                    result += $"正解ラベル付き: {LabeledCount}\n";
                    result += $"正解数: {CorrectCount}\n";
                    result += $"不正解数: {IncorrectCount}\n";
                    result += $"精度: {FormatAccuracy(CorrectCount, LabeledCount)}\n";
                }

                if (ErrorCount > 0)
                {
                    result += $"エラー数: {ErrorCount}\n";
                }

                return result;
            }

            /// <summary>
            /// 判定結果を追加
            /// </summary>
            public void AddResult(string? judgeResult, string? groundTruth, bool hasError = false)
            {
                TotalCount++;

                if (hasError)
                {
                    ErrorCount++;
                    return;
                }

                if (!string.IsNullOrEmpty(groundTruth))
                {
                    LabeledCount++;

                    if (IsCorrect(judgeResult, groundTruth))
                    {
                        CorrectCount++;
                    }
                    else
                    {
                        IncorrectCount++;
                    }
                }
            }
        }

        /// <summary>
        /// 新しい統計情報インスタンスを作成
        /// </summary>
        public static JudgementStatistics CreateStatistics()
        {
            return new JudgementStatistics();
        }
        #endregion

        #region ロギング用ヘルパー
        /// <summary>
        /// 判定結果のログメッセージを生成
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        /// <param name="judgeResult">判定結果</param>
        /// <param name="groundTruth">正解ラベル（オプション）</param>
        /// <param name="categoryName">カテゴリ名（オプション）</param>
        /// <param name="score">スコア（オプション）</param>
        /// <returns>フォーマット済みログメッセージ</returns>
        public static string FormatJudgementLog(
            string fileName,
            string? judgeResult,
            string? groundTruth = null,
            string? categoryName = null,
            double? score = null)
        {
            var message = $"{fileName}: {judgeResult ?? "結果なし"}";

            if (!string.IsNullOrEmpty(groundTruth))
            {
                message += $" {GetComparisonText(judgeResult, groundTruth)}";
            }

            if (!string.IsNullOrEmpty(categoryName))
            {
                message += $" (カテゴリ: {categoryName}";

                if (score.HasValue)
                {
                    message += $", スコア: {score.Value:F4}";
                }

                message += ")";
            }

            return message;
        }
        #endregion
    }
}
