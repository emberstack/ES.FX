using JetBrains.Annotations;
using StackExchange.Redis;

namespace ES.FX.StackExchange.Redis;

/// <summary>
///     Extensions for <see cref="IDatabase" />
/// </summary>
[PublicAPI]
public static class DatabaseExtensions
{
    /// <summary>
    ///     LUA script to delete all keys matching a pattern in batches
    /// </summary>
    private const string DeleteAllWithPatternBatchedScript = @"
            local cursor = '0'
            local batchSize = tonumber(ARGV[2])
            local totalDeleted = 0
            local keys
            repeat
                keys = {}
                local result = redis.call('SCAN', cursor, 'MATCH', ARGV[1], 'COUNT', batchSize)
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
        ";

    /// <summary>
    ///     Deletes all keys matching a pattern in batches
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    /// <param name="pattern">Pattern to MATCH keys</param>
    /// <param name="batchSize">The batch size</param>
    /// <returns>The deleted key count</returns>
    public static long KeysDelete(this IDatabase database, string pattern, uint batchSize = 1000)
    {
        var result = database.ScriptEvaluate(DeleteAllWithPatternBatchedScript, values: [pattern, batchSize]);
        return long.Parse(result.ToString());
    }

    /// <summary>
    ///     Deletes all keys matching a pattern in batches
    /// </summary>
    /// <param name="database">The <see cref="IDatabase" /></param>
    /// <param name="pattern">Pattern to MATCH keys</param>
    /// <param name="batchSize">The batch size</param>
    /// <returns>The deleted key count</returns>
    public static async Task<long> KeysDeleteAsync(this IDatabase database, string pattern, uint batchSize = 1000)
    {
        var result =
            await database.ScriptEvaluateAsync(DeleteAllWithPatternBatchedScript, values: [pattern, batchSize]);
        return long.Parse(result.ToString());
    }
}