using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImageJudgement2
{
    /// <summary>
    /// 判定モード
    /// </summary>
    public enum JudgeMode
    {
        /// <summary>
        /// トップクラスのカテゴリ名で判定
        /// </summary>
        TopClass = 1,

        /// <summary>
        /// OK/NGカテゴリのスコア平均値で判定
        /// </summary>
        ScoreAverage = 2,

        /// <summary>
        /// OK/NGカテゴリのランキング合計で判定（既定）
        /// </summary>
        ScoreRanking = 3
    }

    /// <summary>
    /// AI画像判定APIクライアント
    /// </summary>
    public class AiModelClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string apiKey;
        private readonly string aiModelId;
        private readonly int modelType;
        private readonly string url;
        private const int MaxImageSize = 1200;
        private const int MaxRetryCount = 10;
        private readonly CancellationTokenSource _disposeCts;
        private bool _disposed;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="apiKey">APIキー</param>
        /// <param name="aiModelId">AIモデルID</param>
        /// <param name="modelType">モデルタイプ</param>
        /// <param name="url">APIのURL</param>
        public AiModelClient(string apiKey, string aiModelId, int modelType, string url)
        {
            this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            this.aiModelId = aiModelId ?? throw new ArgumentNullException(nameof(aiModelId));
            this.modelType = modelType;
            this.url = url ?? throw new ArgumentNullException(nameof(url));

            _disposeCts = new CancellationTokenSource();

            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60) // タイムアウトを60秒に延長
            };
        }

        /// <summary>
        /// 画像を判定する
        /// </summary>
        /// <param name="imagePath">画像ファイルパス</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>判定結果</returns>
        public async Task<AiModelResult> PredictImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(imagePath))
                throw new ArgumentNullException(nameof(imagePath));

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("画像ファイルが見つかりません", imagePath);

            // アプリケーション終了とユーザーキャンセルを統合
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);

            try
            {
                Debug.WriteLine($"[AI判定開始] {imagePath}");

                // 画像を読み込んでリサイズ
                byte[] imageBytes;
                using (var originalImage = Image.FromFile(imagePath))
                {
                    imageBytes = ResizeAndConvertToBytes(originalImage);
                }

                // 画像を送信
                var token = await SendImageAsync(imagePath, imageBytes, linkedCts.Token);

                // 結果を取得
                var result = await GetPredictionResultAsync(token, linkedCts.Token);

                Debug.WriteLine($"[AI判定完了] {imagePath}");
                return result;
            }
            catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
            {
                Debug.WriteLine($"[AI判定キャンセル] アプリケーション終了中: {imagePath}");
                throw new ObjectDisposedException(nameof(AiModelClient), "AIクライアントは破棄されました");
            }
        }

        /// <summary>
        /// 画像をリサイズしてバイト配列に変換
        /// </summary>
        private byte[] ResizeAndConvertToBytes(Image image)
        {
            int width = image.Width;
            int height = image.Height;

            // 最大サイズを超える場合はリサイズ
            if (width > MaxImageSize || height > MaxImageSize)
            {
                if (width > height)
                {
                    height = (int)(height * (MaxImageSize / (double)width));
                    width = MaxImageSize;
                }
                else
                {
                    width = (int)(width * (MaxImageSize / (double)height));
                    height = MaxImageSize;
                }
            }

            using (var resizedImage = new Bitmap(image, width, height))
            using (var ms = new MemoryStream())
            {
                resizedImage.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 画像をAPIに送信
        /// </summary>
        private async Task<string> SendImageAsync(string filename, byte[] imageBytes, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            // Build form content factory so we can recreate it for retries
            Func<MultipartFormDataContent> makeContent = () =>
            {
                var content = new MultipartFormDataContent();

                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                content.Add(imageContent, "image_data", Path.GetFileName(filename));

                content.Add(new StringContent(apiKey), "api_key");
                content.Add(new StringContent(aiModelId), "aimodel_id");
                content.Add(new StringContent(modelType.ToString()), "model_type");
                content.Add(new StringContent("image"), "method");

                return content;
            };

            HttpResponseMessage response = null!;
            string responseJson = string.Empty;

            for (int attempt = 0; attempt <= 1; attempt++) // one retry on network error
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var content = makeContent())
                {
                    try
                    {
                        response = await httpClient.PostAsync(url, content, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (attempt == 0)
                    {
                        Debug.WriteLine($"SendImageAsync first attempt failed: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                        continue; // retry
                    }
                }

                if (response == null)
                    continue;

                responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"SendImageAsync failed: {(int)response.StatusCode} {responseJson}");
                    response.EnsureSuccessStatusCode();
                }

                var responseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseJson);

                if (responseData == null || !responseData.ContainsKey("token"))
                    throw new Exception("トークンの取得に失敗しました");

                return responseData["token"].GetString() ?? throw new Exception("トークンが空です");
            }

            throw new Exception("画像送信に失敗しました");
        }

        /// <summary>
        /// 予測結果を取得
        /// </summary>
        private async Task<AiModelResult> GetPredictionResultAsync(string token, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            Dictionary<string, string> MakeForm() => new Dictionary<string, string>
            {
                { "api_key", apiKey },
                { "aimodel_id", aiModelId },
                { "model_type", modelType.ToString() },
                { "method", "result" },
                { "token", token }
            };

            string responseJson = string.Empty;
            Dictionary<string, JsonElement>? responseData = null;

            for (int attempt = 0; attempt <= MaxRetryCount; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var content = new FormUrlEncodedContent(MakeForm()))
                {
                    HttpResponseMessage response;
                    try
                    {
                        response = await httpClient.PostAsync(url, content, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"GetPredictionResultAsync HTTP request failed (attempt {attempt}): {ex.Message}");
                        if (attempt == MaxRetryCount) throw;
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"GetPredictionResultAsync non-success status (attempt {attempt}): {(int)response.StatusCode} {responseJson}");

                        // For server errors, retry up to MaxRetryCount
                        if ((int)response.StatusCode >= 500)
                        {
                            if (attempt == MaxRetryCount)
                                response.EnsureSuccessStatusCode();

                            await Task.Delay(1000, cancellationToken);
                            continue;
                        }

                        // For client errors, throw to caller
                        response.EnsureSuccessStatusCode();
                    }

                    responseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseJson);

                    if (responseData == null)
                        throw new Exception("結果の取得に失敗しました");

                    // If server reports processing, wait and retry (recreate content each loop)
                    if (responseData.ContainsKey("is_processing") && responseData["is_processing"].GetBoolean())
                    {
                        Debug.WriteLine($"Prediction is processing (attempt {attempt}).");
                        if (attempt == MaxRetryCount)
                            break;

                        await Task.Delay(1000, cancellationToken);
                        continue; // retry
                    }

                    // successful and not processing -> break loop
                    break;
                }
            }

            if (responseData == null)
                throw new Exception("結果の取得に失敗しました");

            // 結果を抽出
            var result = new AiModelResult
            {
                IsProcessing = responseData.ContainsKey("is_processing") && responseData["is_processing"].GetBoolean(),
                TopClassResult = responseData.ContainsKey("top_class_result")
                    ? JsonSerializer.Deserialize<ClassificationResult>(responseData["top_class_result"].GetRawText())
                    : null,
                RawResponse = responseJson
            };

            // all_class_result があればパースしてリストに格納
            if (responseData.ContainsKey("all_class_result"))
            {
                try
                {
                    var all = JsonSerializer.Deserialize<List<ClassificationResult>>(responseData["all_class_result"].GetRawText());
                    if (all != null)
                    {
                        // Score で降順ソートして格納
                        all.Sort((a, b) => b.Score.CompareTo(a.Score));
                        result.AllClassResults = all;
                    }
                }
                catch
                {
                    // パース失敗しても処理を継続（後で RawResponse から手動パース可能）
                }
            }

            return result;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AiModelClient));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // 進行中のリクエストをキャンセル
            _disposeCts?.Cancel();

            try
            {
                // HttpClientのDisposeを呼び出す
                httpClient?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HttpClient disposal error: {ex.Message}");
            }
            finally
            {
                _disposeCts?.Dispose();
            }
        }
    }

    /// <summary>
    /// 分類結果
    /// </summary>
    public class ClassificationResult
    {
        /// <summary>
        /// クラス番号
        /// </summary>
        [JsonPropertyName("class")]
        public int Class { get; set; }

        /// <summary>
        /// スコア
        /// </summary>
        [JsonPropertyName("score")]
        public double Score { get; set; }

        /// <summary>
        /// カテゴリ名
        /// </summary>
        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// カテゴリID
        /// </summary>
        [JsonPropertyName("category_id")]
        public string CategoryId { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI判定結果
    /// </summary>
    public class AiModelResult
    {
        /// <summary>
        /// 処理中かどうか
        /// </summary>
        public bool IsProcessing { get; set; }

        /// <summary>
        /// トップクラスの判定結果
        /// </summary>
        public ClassificationResult TopClassResult { get; set; } = new ClassificationResult();

        /// <summary>
        /// 全クラスの判定結果リスト
        /// </summary>
        public List<ClassificationResult> AllClassResults { get; set; } = new List<ClassificationResult>();

        /// <summary>
        /// 生のレスポンスJSON
        /// </summary>
        public string? RawResponse { get; set; }

        /// <summary>
        /// OKカテゴリのランキングスコアを計算する。
        /// AllClassResults の順序に対して、OK を含むカテゴリのインデックス(1ベース)を合計して返す。
        /// 要素がなければ 0 を返す。
        /// </summary>
        public int ComputeOkRankingScore()
        {
            return ComputeRankingScoreByKeyword("OK");
        }

        /// <summary>
        /// NGカテゴリのランキングスコアを計算する。
        /// AllClassResults の順序に対して、NG を含むカテゴリのインデックス(1ベース)を合計して返す。
        /// 要素がなければ 0 を返す。
        /// </summary>
        public int ComputeNgRankingScore()
        {
            return ComputeRankingScoreByKeyword("NG");
        }

        private int ComputeRankingScoreByKeyword(string keyword)
        {
            if (AllClassResults == null || AllClassResults.Count == 0) return 0;

            int sum = 0;
            int index = 1; // 1-based ranking
            foreach (var r in AllClassResults)
            {
                if (!string.IsNullOrEmpty(r.CategoryName) && r.CategoryName.ToUpper().Contains(keyword.ToUpper()))
                {
                    sum += index;
                }
                index++;
            }

            return sum;
        }

        /// <summary>
        /// 既定モード（ScoreRanking）で判定を行う
        /// </summary>
        public string Judge()
        {
            return Judge(JudgeMode.ScoreRanking);
        }

        /// <summary>
        /// 指定された検出モードで判定を行う
        /// </summary>
        /// <param name="mode">判定モード</param>
        public string Judge(JudgeMode mode)
        {
            return mode switch
            {
                JudgeMode.TopClass => JudgeByTopClass(),
                JudgeMode.ScoreAverage => JudgeByScoreAverage(),
                JudgeMode.ScoreRanking => JudgeByScoreRanking(),
                _ => "Unknown"
            };
        }

        /// <summary>
        /// 数値モード指定（後方互換性のため残す）
        /// </summary>
        [Obsolete("Use Judge(JudgeMode) instead")]
        public string Judge(int detectionMode)
        {
            return detectionMode switch
            {
                1 => JudgeByTopClass(),
                2 => JudgeByScoreAverage(),
                3 => JudgeByScoreRanking(),
                _ => "Unknown"
            };
        }

        private string JudgeByTopClass()
        {
            if (TopClassResult == null || string.IsNullOrEmpty(TopClassResult.CategoryName))
                return "Unknown";

            var prefix = TopClassResult.CategoryName.Length >= 2
                ? TopClassResult.CategoryName.Substring(0, 2).ToUpper()
                : TopClassResult.CategoryName.ToUpper();

            if (prefix.StartsWith("OK")) return "OK";
            if (prefix.StartsWith("NG")) return "NG";
            return "Unknown";
        }

        private string JudgeByScoreAverage()
        {
            if (AllClassResults == null || AllClassResults.Count == 0)
            {
                // フォールバック: TopClass を採用
                return JudgeByTopClass();
            }

            double okSum = 0; int okCount = 0;
            double ngSum = 0; int ngCount = 0;

            foreach (var r in AllClassResults)
            {
                if (string.IsNullOrEmpty(r.CategoryName)) continue;
                var name = r.CategoryName.ToUpper();
                if (name.Contains("OK")) { okSum += r.Score; okCount++; }
                if (name.Contains("NG")) { ngSum += r.Score; ngCount++; }
            }

            if (okCount == 0 && ngCount == 0)
            {
                return JudgeByTopClass();
            }

            double okAvg = okCount > 0 ? okSum / okCount : double.MinValue;
            double ngAvg = ngCount > 0 ? ngSum / ngCount : double.MinValue;

            if (okAvg == double.MinValue && ngAvg != double.MinValue) return "NG";
            if (ngAvg == double.MinValue && okAvg != double.MinValue) return "OK";

            if (okAvg > ngAvg) return "OK";
            if (ngAvg > okAvg) return "NG";

            return "Unknown";
        }

        private string JudgeByScoreRanking()
        {
            if (TopClassResult == null || string.IsNullOrEmpty(TopClassResult.CategoryName))
                return "Unknown";

            if (TopClassResult.CategoryId == "")
                return TopClassResult.CategoryName.Length >= 2
                    ? TopClassResult.CategoryName.Substring(0, 2)
                    : TopClassResult.CategoryName;

            int okScore = ComputeOkRankingScore();
            int ngScore = ComputeNgRankingScore();

            if (okScore < ngScore)
                return "OK";
            else if (ngScore < okScore)
                return "NG";
            else
                return "Unknown";
        }

        /// <summary>
        /// AllClassResults を Score の降順にソートする。
        /// </summary>
        public void SortAllByScoreDescending()
        {
            if (AllClassResults == null) return;
            AllClassResults.Sort((a, b) => b.Score.CompareTo(a.Score));
        }
    }
}