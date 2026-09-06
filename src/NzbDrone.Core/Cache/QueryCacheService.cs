using System;
using System.IO.Compression;
using System.Threading.Tasks;
using Dapper;
using NLog;

namespace NzbDrone.Core.Cache
{
    public class QueryCacheService(ISqliteCacheDatabase cacheDatabase, Logger logger)
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(
            int.TryParse(Environment.GetEnvironmentVariable("CACHE_TTL_MINS"), out var mins) && mins > 0
                ? mins
                : 10);

        public async ValueTask<byte[]> GetAsync(string key)
        {
            var hash = CacheKeyHasher.Hash(key);

            try
            {
                using var connection = cacheDatabase.OpenConnection();

                const string selectSql = @"
                        SELECT Payload
                        FROM QueryCache
                        WHERE KeyHash = @hash
                        AND CreatedAt >= (datetime('now', '-' || @ttlMinutes || ' minutes'));
                    ";

                var entry = await connection.QueryFirstOrDefaultAsync<QueryCacheRecord>(selectSql, new { hash, ttlMinutes = CacheTtl.TotalMinutes });

                if (entry?.Payload == null || entry.Payload.Length == 0)
                {
                    return null;
                }

                return BrotliCompressionHelper.Decompress(entry.Payload);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to read QueryCache for key: {0}", key);
                return null;
            }
        }

        public async ValueTask SetAsync(string key, byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                return;
            }

            var hash = CacheKeyHasher.Hash(key);

            try
            {
                var compressed = BrotliCompressionHelper.Compress(value, CompressionLevel.Fastest);

                using var connection = cacheDatabase.OpenConnection();

                const string upsertSql = @"
                    INSERT INTO QueryCache (KeyHash, OriginalKey, Payload, CompressedSize, UncompressedSize, CreatedAt)
                    VALUES (@hash, @key, @compressed, @compressedSize, @uncompressedSize, datetime('now'))
                    ON CONFLICT(KeyHash) DO UPDATE SET
                        OriginalKey = @key,
                        Payload = @compressed,
                        CompressedSize = @compressedSize,
                        UncompressedSize = @uncompressedSize,
                        CreatedAt = datetime('now');
                ";

                await connection.ExecuteAsync(upsertSql, new
                {
                    hash,
                    key,
                    compressed,
                    compressedSize = compressed.Length,
                    uncompressedSize = value.Length
                });
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to store into QueryCache for key: {0}", key);
            }
        }

        public void Cleanup()
        {
            try
            {
                using var connection = cacheDatabase.OpenConnection();
                var expiredOutputCount =
                    connection.Execute("DELETE FROM QueryCache WHERE CreatedAt <= (datetime('now', '-' || @ttlMinutes || ' minutes'))",
                        new { ttlMinutes = CacheTtl.TotalMinutes });

                if (expiredOutputCount > 0)
                {
                    logger.Debug("Evicted {0} expired records from QueryCache", expiredOutputCount);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to evict expired records from QueryCache");
            }
        }

        private class QueryCacheRecord
        {
            public byte[] Payload { get; set; }
        }
    }
}
