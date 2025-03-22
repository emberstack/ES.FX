using JetBrains.Annotations;
using StackExchange.Redis;

namespace ES.FX.Extensions.StackExchange.Redis;

/// <summary>
///     Extensions for <see cref="IDatabase" />
/// </summary>
[PublicAPI]
public static class DatabaseExtensions
{
    /// <summary>
    ///     LUA script to delete all keys matching a pattern in batches
    /// </summary>
    private const string DeleteAllWithPatternBatchedScript = """
                                                             
                                                                         local cursor = '0'
                                                                         local batchSize = tonumber(ARGV[1])
                                                                         local totalDeleted = 0
                                                                         local keys
                                                                         repeat
                                                                             keys = {}
                                                                             local result = redis.call('SCAN', cursor, 'MATCH', KEYS[1], 'COUNT', batchSize)
                                                                             cursor = result[1]
                                                                             for i, key in ipairs(result[2]) do
                                                                                 table.insert(keys, key)
                                                                             end
                                                                             if #keys > 0 then
                                                                                 redis.call('DEL', unpack(keys))
                                                                                 totalDeleted = totalDeleted + #keys
                                                                             end
                                                                         until cursor == '0'
                                                                         return totalDeleted
                                                                     
                                                             """;

    private const string DeterminePrefixScript =
        "return string.sub(KEYS[1],1,string.len(KEYS[1])-string.len(ARGV[1]))";


    /// <summary>
    ///     Attempts to get the key prefix.
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    public static RedisResult GetKeyPrefix(this IDatabase database) =>
        database.ScriptEvaluateReadOnly(DeterminePrefixScript, [nameof(Redis)], [nameof(Redis)]);

    /// <summary>
    ///     Attempts to get the key prefix.
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    public static async Task<RedisResult> GetKeyPrefixAsync(this IDatabase database) =>
        await database.ScriptEvaluateReadOnlyAsync(DeterminePrefixScript, [nameof(Redis)], [nameof(Redis)]);


    /// <summary>
    ///     Deletes all keys matching a pattern in batches
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    /// <param name="pattern">Pattern to MATCH keys</param>
    /// <param name="batchSize">The batch size</param>
    /// <returns>The deleted key count</returns>
    public static long KeysDelete(this IDatabase database, string pattern, int batchSize = 1000)
    {
        var result = database.ScriptEvaluate(
            DeleteAllWithPatternBatchedScript,
            [pattern], [batchSize]);
        return long.Parse(result.ToString());
    }


    /// <summary>
    ///     Deletes all keys
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    /// <param name="batchSize">The batch size</param>
    /// <returns>The deleted key count</returns>
    public static long KeysDeleteAll(this IDatabase database, int batchSize = 1000) =>
        database.KeysDelete("*", batchSize);


    /// <summary>
    ///     Deletes all keys
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    /// <param name="batchSize">The batch size</param>
    /// <returns>The deleted key count</returns>
    public static async Task<long> KeysDeleteAllAsync(this IDatabase database, int batchSize = 1000) =>
        await database.KeysDeleteAsync("*", batchSize);

    /// <summary>
    ///     Deletes all keys matching a pattern in batches
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    /// <param name="pattern">Pattern to MATCH keys</param>
    /// <param name="batchSize">The batch size</param>
    /// <returns>The deleted key count</returns>
    public static async Task<long> KeysDeleteAsync(this IDatabase database, string pattern, int batchSize = 1000)
    {
        var result = await database.ScriptEvaluateAsync(
                DeleteAllWithPatternBatchedScript,
                [pattern], [batchSize])
            .ConfigureAwait(false);
        return long.Parse(result.ToString());
    }
}