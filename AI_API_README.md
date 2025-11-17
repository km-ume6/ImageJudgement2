# AI判定API クライアント使用方法

このドキュメントでは、PythonスクリプトをC#に変換した`AiModelClient`クラスの使用方法を説明します。

## 概要

`AiModelClient`は、AI画像判定APIに画像を送信して判定結果を取得するクライアントクラスです。

## 設定方法

### 1. 設定ダイアログからの設定

1. メインウィンドウの「設定」ボタンをクリック
2. 「AI判定API」タブを選択
3. 以下の項目を入力:
   - **APIキー**: API認証キー (例: `0250c4ef50104408883ec28fb2292664`)
   - **AIモデルID**: 使用するAIモデルのID (例: `506f30c4-687d-44f5-9d0a-4b8c64c0b588`)
   - **モデルタイプ**: モデルの種類 (デフォルト: `11`)
   - **API URL**: APIのエンドポイントURL (デフォルト: `https://us.adfi.karakurai.com/API/ap/vit/online/`)
4. 「OK」ボタンをクリックして保存

設定は自動的に`AppSettings.json`に保存されます。

### 2. プログラムからの使用

```csharp
// AI判定クライアントを作成
using var aiClient = CreateAiModelClient();

if (aiClient == null)
{
    MessageBox.Show("AI判定APIの設定が不完全です。", "エラー", 
        MessageBoxButtons.OK, MessageBoxIcon.Error);
    return;
}

// 画像を判定
try
{
    var result = await aiClient.PredictImageAsync("path/to/image.png");
    
    if (result.IsProcessing)
    {
        MessageBox.Show("まだ処理中です。しばらく待ってください。", "処理中");
    }
    else
    {
        MessageBox.Show($"判定結果: {result.TopClassResult}", "成功");
        
        // 詳細な結果を確認
        Debug.WriteLine($"生のレスポンス: {result.RawResponse}");
    }
}
catch (Exception ex)
{
    MessageBox.Show($"AI判定エラー:\n{ex.Message}", "エラー", 
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
```

## クラス構造

### AiModelClient

#### コンストラクタ

```csharp
public AiModelClient(string apiKey, string aiModelId, int modelType, string url)
```

- `apiKey`: API認証キー
- `aiModelId`: AIモデルID
- `modelType`: モデルタイプ (通常は11)
- `url`: APIエンドポイントURL

#### メソッド

```csharp
public async Task<AiModelResult> PredictImageAsync(string imagePath)
```

指定された画像ファイルをAI判定APIに送信し、判定結果を取得します。

**パラメータ:**
- `imagePath`: 判定する画像ファイルのパス

**戻り値:**
- `AiModelResult`: 判定結果を含むオブジェクト

**例外:**
- `ArgumentNullException`: 画像パスがnullまたは空の場合
- `FileNotFoundException`: 画像ファイルが見つからない場合
- `HttpRequestException`: APIリクエストが失敗した場合

### AiModelResult

判定結果を保持するクラス

#### プロパティ

- `IsProcessing` (bool): まだ処理中かどうか
- `TopClassResult` (string?): トップクラスの判定結果
- `RawResponse` (string?): APIからの生のJSONレスポンス

## 注意事項

1. **画像サイズ**: 画像は自動的に最大1200ピクセルにリサイズされます
2. **リトライ**: APIリクエストが失敗した場合、1秒後に自動的にリトライします
3. **タイムアウト**: HTTPリクエストのタイムアウトは30秒です
4. **処理待ち**: 判定処理中の場合、最大10回(10秒間)リトライします
5. **リソース管理**: `AiModelClient`は`IDisposable`を実装しているため、`using`ステートメントを使用してください

## トラブルシューティング

### 「AI判定APIの設定が不完全です」というエラーが表示される

以下の項目が正しく設定されているか確認してください:
- APIキー
- AIモデルID  
- API URL

### 接続エラーが発生する

1. インターネット接続を確認
2. API URLが正しいか確認
3. ファイアウォールでブロックされていないか確認

### 「トークンの取得に失敗しました」エラー

APIキーまたはAIモデルIDが正しくない可能性があります。設定を再確認してください。

## 元のPythonスクリプトとの対応

| Python | C# |
|--------|-----|
| `Image.open(filename).convert("RGB")` | `Image.FromFile(imagePath)` |
| `img.resize((width, height), Image.LANCZOS)` | `new Bitmap(image, width, height)` |
| `requests.post(url, files=files, data=data)` | `httpClient.PostAsync(url, content)` |
| `time.sleep(1)` | `await Task.Delay(1000)` |
| `response.json()` | `JsonSerializer.Deserialize<>()` |

## サンプルコード

完全な使用例:

```csharp
private async void BtnPredictImage_Click(object sender, EventArgs e)
{
    using var openDialog = new OpenFileDialog
    {
        Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp",
        Title = "判定する画像を選択"
    };

    if (openDialog.ShowDialog() != DialogResult.OK)
        return;

    using var aiClient = CreateAiModelClient();
    
    if (aiClient == null)
    {
        MessageBox.Show("AI設定が不完全です", "エラー");
        return;
    }

    try
    {
        var result = await aiClient.PredictImageAsync(openDialog.FileName);
        MessageBox.Show($"判定結果: {result.TopClassResult}");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"エラー: {ex.Message}");
    }
}
```
