using Microsoft.Data.SqlClient;
using System.Data;

namespace ImageJudgement2
{
    /// <summary>
    /// SQL Server データベースアクセスヘルパークラス
    /// </summary>
    public class DatabaseHelper : IDisposable
    {
        #region フィールド
        private SqlConnection? _connection;
        private readonly string _connectionString;
        private bool _disposed;
        #endregion

        #region コンストラクタ
        /// <summary>
        /// コンストラクタ（個別パラメータ）
        /// </summary>
        /// <param name="server">サーバー名</param>
        /// <param name="database">データベース名</param>
        /// <param name="userId">ユーザーID</param>
        /// <param name="password">パスワード</param>
        public DatabaseHelper(string server, string database, string userId, string password)
        {
            ValidateParameter(server, nameof(server), "サーバー");
            ValidateParameter(database, nameof(database), "データベース");
            ValidateParameter(userId, nameof(userId), "ユーザーID");

            _connectionString = BuildConnectionString(server, database, userId, password);
        }

        /// <summary>
        /// コンストラクタ（接続文字列）
        /// </summary>
        /// <param name="connectionString">接続文字列</param>
        public DatabaseHelper(string connectionString)
        {
            ValidateParameter(connectionString, nameof(connectionString), "接続文字列");
            _connectionString = EnhanceConnectionString(connectionString);
        }
        #endregion

        #region パブリックメソッド
        /// <summary>
        /// データベース接続を開く
        /// </summary>
        public void Open()
        {
            ThrowIfDisposed();

            try
            {
                _connection ??= new SqlConnection(_connectionString);

                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                    Logger.Debug("データベース接続を開きました");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("データベース接続エラー", ex);
                throw new InvalidOperationException("データベースに接続できませんでした。", ex);
            }
        }

        /// <summary>
        /// データベース接続を閉じる
        /// </summary>
        public void Close()
        {
            if (_connection?.State == ConnectionState.Open)
            {
                try
                {
                    _connection.Close();
                    Logger.Debug("データベース接続を閉じました");
                }
                catch (Exception ex)
                {
                    Logger.Error("データベース切断エラー", ex);
                }
            }
        }

        /// <summary>
        /// 接続テスト
        /// </summary>
        /// <returns>接続できたらtrue</returns>
        public bool TestConnection()
        {
            try
            {
                using var testConnection = new SqlConnection(_connectionString);
                testConnection.Open();
                Logger.Info("接続テスト成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"接続テスト失敗: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// SELECTクエリを実行してDataTableを返す
        /// </summary>
        /// <param name="query">SQLクエリ</param>
        /// <param name="parameters">パラメータ</param>
        /// <param name="commandTimeout">コマンドタイムアウト（秒）</param>
        /// <returns>結果のDataTable</returns>
        public DataTable ExecuteQuery(
            string query,
            Dictionary<string, object>? parameters = null,
            int commandTimeout = AppConstants.Database.DefaultCommandTimeout)
        {
            ThrowIfDisposed();

            try
            {
                Open();

                using var command = CreateCommand(query, parameters, commandTimeout);
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();
                adapter.Fill(dataTable);

                Logger.Info($"クエリ実行完了: {dataTable.Rows.Count}行取得");
                return dataTable;
            }
            catch (Exception ex)
            {
                Logger.Error($"クエリ実行エラー: {query}", ex);
                throw new InvalidOperationException("クエリの実行に失敗しました。", ex);
            }
        }

        /// <summary>
        /// INSERT/UPDATE/DELETEクエリを実行
        /// </summary>
        /// <param name="query">SQLクエリ</param>
        /// <param name="parameters">パラメータ</param>
        /// <param name="commandTimeout">コマンドタイムアウト（秒）</param>
        /// <returns>影響を受けた行数</returns>
        public int ExecuteNonQuery(
            string query,
            Dictionary<string, object>? parameters = null,
            int commandTimeout = AppConstants.Database.DefaultCommandTimeout)
        {
            ThrowIfDisposed();

            SqlConnection? localConnection = null;
            try
            {
                // 新しい接続を使用（接続の状態を確実にする）
                localConnection = new SqlConnection(_connectionString);
                localConnection.Open();

                using var command = CreateCommand(query, parameters, commandTimeout, localConnection);
                int affectedRows = command.ExecuteNonQuery();

                Logger.Info($"NonQuery実行完了: {affectedRows}行影響");
                return affectedRows;
            }
            catch (SqlException ex) when (ex.Number == -2) // Timeout
            {
                Logger.Error($"NonQuery実行タイムアウト: {query}", ex);
                throw new InvalidOperationException("クエリの実行がタイムアウトしました。", ex);
            }
            catch (SqlException ex) when (ex.Number is 64 or -2146893055) // Connection broken
            {
                Logger.Error($"接続エラー: {query}", ex);
                throw new InvalidOperationException("データベース接続が切断されました。", ex);
            }
            catch (Exception ex)
            {
                Logger.Error($"NonQuery実行エラー: {query}", ex);
                throw new InvalidOperationException("クエリの実行に失敗しました。", ex);
            }
            finally
            {
                localConnection?.Close();
                localConnection?.Dispose();
            }
        }

        /// <summary>
        /// スカラー値を取得
        /// </summary>
        /// <param name="query">SQLクエリ</param>
        /// <param name="parameters">パラメータ</param>
        /// <param name="commandTimeout">コマンドタイムアウト（秒）</param>
        /// <returns>スカラー値</returns>
        public object? ExecuteScalar(
            string query,
            Dictionary<string, object>? parameters = null,
            int commandTimeout = AppConstants.Database.DefaultCommandTimeout)
        {
            ThrowIfDisposed();

            try
            {
                Open();

                using var command = CreateCommand(query, parameters, commandTimeout);
                var result = command.ExecuteScalar();

                Logger.Info($"Scalar実行完了: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Scalar実行エラー: {query}", ex);
                throw new InvalidOperationException("クエリの実行に失敗しました。", ex);
            }
        }

        /// <summary>
        /// トランザクション内でクエリを実行
        /// </summary>
        /// <param name="action">実行するアクション</param>
        public void ExecuteTransaction(Action<SqlCommand, SqlTransaction> action)
        {
            ThrowIfDisposed();

            SqlTransaction? transaction = null;
            try
            {
                Open();

                transaction = _connection!.BeginTransaction();
                using var command = new SqlCommand
                {
                    Connection = _connection,
                    Transaction = transaction,
                    CommandTimeout = AppConstants.Database.DefaultCommandTimeout
                };

                action(command, transaction);

                transaction.Commit();
                Logger.Info("トランザクション完了");
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                Logger.Error("トランザクションエラー", ex);
                throw new InvalidOperationException("トランザクションの実行に失敗しました。", ex);
            }
        }
        #endregion

        #region プライベートメソッド
        /// <summary>
        /// SQLコマンドを作成
        /// </summary>
        private SqlCommand CreateCommand(
            string query,
            Dictionary<string, object>? parameters,
            int commandTimeout,
            SqlConnection? connection = null)
        {
            var command = new SqlCommand(query, connection ?? _connection!)
            {
                CommandTimeout = commandTimeout
            };

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            return command;
        }

        /// <summary>
        /// 接続文字列を構築
        /// </summary>
        public static string BuildConnectionString(string server, string database, string userId, string password)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                UserID = userId,
                Password = password,
                TrustServerCertificate = false,
                Encrypt = false,
                ConnectTimeout = AppConstants.Database.DefaultConnectionTimeout,
                Pooling = true,
                MaxPoolSize = AppConstants.Database.DefaultMaxPoolSize,
                MinPoolSize = AppConstants.Database.DefaultMinPoolSize
            };

            return builder.ConnectionString;
        }

        /// <summary>
        /// 接続文字列を拡張（タイムアウトとプーリング設定を追加）
        /// </summary>
        private static string EnhanceConnectionString(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            // デフォルト値の場合は推奨値を設定
            if (builder.ConnectTimeout == 15)
                builder.ConnectTimeout = AppConstants.Database.DefaultConnectionTimeout;

            if (!builder.Pooling)
                builder.Pooling = true;

            if (builder.MaxPoolSize == 100)
                builder.MaxPoolSize = AppConstants.Database.DefaultMaxPoolSize;

            if (builder.MinPoolSize == 0)
                builder.MinPoolSize = AppConstants.Database.DefaultMinPoolSize;

            return builder.ConnectionString;
        }

        /// <summary>
        /// パラメータの検証
        /// </summary>
        private static void ValidateParameter(string value, string paramName, string displayName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{displayName}が指定されていません。", paramName);
        }

        /// <summary>
        /// 破棄済みチェック
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DatabaseHelper));
        }
        #endregion

        #region IDisposable実装
        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Close();
            _connection?.Dispose();
            _connection = null;
            _disposed = true;

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
