using Moq;
using StackExchange.Redis;

namespace ES.FX.StackExchange.Redis.Tests;

public class DatabaseExtensionTests
{
    [Theory]
    [InlineData("pattern", 1000)]
    public void KeysDelete_Input_Output_check(string pattern, uint batchSize)
    {
        var database = new Mock<IDatabase>();
        database.Setup(database => database.ScriptEvaluate(It.IsAny<string>(),
                It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Returns(RedisResult.Create(new RedisValue(batchSize.ToString()), ResultType.SimpleString));

        Assert.Equal(database.Object.KeysDelete(pattern, batchSize),
            batchSize);

        database.Verify(database => database.ScriptEvaluate(It.IsAny<string>(),
            It.IsAny<RedisKey[]>(), It.Is<RedisValue[]>(x => x.Contains(pattern) && x.Contains(batchSize)),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Theory]
    [InlineData("pattern", 1000)]
    public async Task KeysDeleteAsync_Input_Output_checkAsync(string pattern, uint batchSize)
    {
        var database = new Mock<IDatabase>();
        database.Setup(database => database.ScriptEvaluateAsync(It.IsAny<string>(),
                It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Returns(
                Task.FromResult(RedisResult.Create(new RedisValue(batchSize.ToString()), ResultType.SimpleString)));

        Assert.Equal(await database.Object.KeysDeleteAsync(pattern, batchSize),
            batchSize);

        database.Verify(database => database.ScriptEvaluateAsync(It.IsAny<string>(),
            It.IsAny<RedisKey[]>(), It.Is<RedisValue[]>(x => x.Contains(pattern) && x.Contains(batchSize)),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}