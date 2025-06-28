using System.Text.Json;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using ES.FX.TransactionalOutbox.Serialization;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Serialization;

public class OutboxSerializerTests
{
    private readonly IOutboxSerializer _serializer;
    private readonly IPayloadTypeProvider _typeProvider = new DefaultPayloadTypeResolver();

    public OutboxSerializerTests() => _serializer = new DefaultOutboxSerializer(_typeProvider);

    [Fact]
    public void Serialize_Should_Serialize_Simple_Object()
    {
        // Arrange
        var order = new TestOrder
        {
            Id = 1,
            OrderNumber = "ORD-001",
            Amount = 100.50m,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var headers = new Dictionary<string, string>
        {
            ["X-Custom-Header"] = "CustomValue"
        };

        // Act
        _serializer.Serialize(
            order,
            typeof(TestOrder),
            headers,
            out var payloadType,
            out var serializedPayload,
            out var serializedHeaders);

        // Assert
        Assert.NotNull(payloadType);
        Assert.Contains("TestOrder", payloadType);
        Assert.NotNull(serializedPayload);
        Assert.NotEmpty(serializedPayload);
        Assert.NotNull(serializedHeaders);

        // Verify payload can be deserialized back
        var deserializedOrder = JsonSerializer.Deserialize<TestOrder>(serializedPayload);
        Assert.NotNull(deserializedOrder);
        Assert.Equal(order.OrderNumber, deserializedOrder.OrderNumber);
        Assert.Equal(order.Amount, deserializedOrder.Amount);
    }

    [Fact]
    public void Deserialize_Should_Deserialize_Object_Correctly()
    {
        // Arrange
        var order = new TestOrder
        {
            Id = 2,
            OrderNumber = "ORD-002",
            Amount = 200.75m,
            CreatedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)
        };

        var serializedPayload = JsonSerializer.Serialize(order);
        var payloadType = typeof(TestOrder).AssemblyQualifiedName!;
        var headers = new Dictionary<string, string>
        {
            ["X-Test"] = "Value"
        };
        var serializedHeaders = JsonSerializer.Serialize(headers);

        // Act
        _serializer.Deserialize(
            serializedPayload,
            payloadType,
            serializedHeaders,
            out var result,
            out var resultType,
            out var deserializedHeaders);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TestOrder>(result);

        var deserializedOrder = (TestOrder)result;
        Assert.Equal(order.OrderNumber, deserializedOrder.OrderNumber);
        Assert.Equal(order.Amount, deserializedOrder.Amount);
        Assert.Equal(order.CreatedAt, deserializedOrder.CreatedAt);

        Assert.NotNull(deserializedHeaders);
        Assert.True(deserializedHeaders.ContainsKey("X-Test"));
        Assert.Equal("Value", deserializedHeaders["X-Test"]);
    }

    [Fact]
    public void Serialize_Should_Handle_Complex_Types()
    {
        // Arrange
        var complexObject = new
        {
            Id = Guid.NewGuid(),
            Name = "Test Object",
            Items = new List<string> { "Item1", "Item2", "Item3" },
            Metadata = new Dictionary<string, object>
            {
                ["Key1"] = 123,
                ["Key2"] = "Value2",
                ["Key3"] = true
            },
            CreatedAt = DateTimeOffset.UtcNow
        };

        var headers = new Dictionary<string, string>();

        // Act
        _serializer.Serialize(
            complexObject,
            complexObject.GetType(),
            headers,
            out var payloadType,
            out var serializedPayload,
            out var serializedHeaders);

        // Assert
        Assert.NotNull(payloadType);
        Assert.NotNull(serializedPayload);
        Assert.NotEmpty(serializedPayload);

        // Verify JSON structure
        using var doc = JsonDocument.Parse(serializedPayload);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("Name", out var nameProp));
        Assert.Equal("Test Object", nameProp.GetString());

        Assert.True(root.TryGetProperty("Items", out var itemsProp));
        Assert.Equal(3, itemsProp.GetArrayLength());
    }

    [Fact]
    public void Serialize_Should_Preserve_Headers()
    {
        // Arrange
        var payload = new { Message = "Test" };
        var headers = new Dictionary<string, string>
        {
            ["X-Correlation-Id"] = Guid.NewGuid().ToString(),
            ["X-User-Id"] = "12345",
            ["X-Timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        // Act
        _serializer.Serialize(
            payload,
            payload.GetType(),
            headers,
            out _,
            out _,
            out var serializedHeaders);

        // Assert
        var deserializedHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(serializedHeaders!);
        Assert.NotNull(deserializedHeaders);
        Assert.Equal(headers.Count, deserializedHeaders.Count);

        foreach (var kvp in headers)
        {
            Assert.True(deserializedHeaders.ContainsKey(kvp.Key));
            Assert.Equal(kvp.Value, deserializedHeaders[kvp.Key]);
        }
    }

    [Fact]
    public void PayloadTypeProvider_Should_Generate_Consistent_Type_Names()
    {
        // Arrange
        var orderType = typeof(TestOrder);

        // Act
        var typeName1 = _typeProvider.GetPayloadType(orderType);
        var typeName2 = _typeProvider.GetPayloadType(orderType);

        // Assert
        Assert.Equal(typeName1, typeName2);
        Assert.Contains("TestOrder", typeName1);
        Assert.Contains("ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests", typeName1);
    }

    [Fact]
    public void PayloadTypeProvider_Should_Resolve_Type_From_Name()
    {
        // Arrange
        var orderType = typeof(TestOrder);
        var typeName = _typeProvider.GetPayloadType(orderType);
        var headers = new Dictionary<string, string>();

        // Act
        var resolvedType = _typeProvider.GetType(typeName, headers);

        // Assert
        Assert.NotNull(resolvedType);
        Assert.Equal(orderType, resolvedType);
    }
}