using Json.Schema;
using System.Net;
using System.Text.Json;

namespace A2A.AspNetCore.Tests;

public sealed class ClientTests : IClassFixture<JsonSchemaFixture>, IDisposable
{
    private readonly JsonSchema a2aSchema;
    private readonly MockMessageHandler mockHandler;
    private readonly A2AClient client;
    private readonly HttpClient httpClient;

    public ClientTests(JsonSchemaFixture fixture)
    {
        a2aSchema = fixture.Schema;

        mockHandler = new MockMessageHandler();

        httpClient = new HttpClient(mockHandler);

        client = new A2AClient(new Uri("http://example.org"), httpClient);
    }

    [Fact]
    public async Task TestGetTask()
    {
        // Arrange
        var taskId = "test-task";

        // Act
        await client.GetTaskAsync(taskId);
        var message = mockHandler.Request?.Content != null
            ? await mockHandler.Request.Content.ReadAsStringAsync()
            : string.Empty;
        // Assert
        Assert.NotNull(message);

        // JSON Schema validation using JSONSchema.Net
        var json = JsonDocument.Parse(message);
        var validationResult = a2aSchema.Evaluate(json.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(validationResult.IsValid, $"JSON does not match schema: {validationResult.Details}");
    }

    [Fact]
    public async Task TestSendMessage()
    {
        // Arrange
        var taskSendParams = new MessageSendParams
        {
            Message = new Message()
            {
                Parts =
                [
                    new TextPart()
                    {
                        Text = "Hello, World!",
                    }
                ],
            },
        };

        // Act
        await client.SendMessageAsync(taskSendParams);
        var message = await mockHandler!.Request!.Content!.ReadAsStringAsync();

        // Assert
        Assert.NotNull(message);

        // JSON Schema validation using JSONSchema.Net
        var json = JsonDocument.Parse(message);
        var validationResult = a2aSchema.Evaluate(json.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(validationResult.IsValid, $"JSON does not match schema: {validationResult.Details}");
    }

    [Fact]
    public async Task TestCancelTask()
    {
        // Arrange
        var taskId = "test-task";

        // Act
        await client.CancelTaskAsync(new TaskIdParams { Id = taskId });
        var message = await mockHandler!.Request!.Content!.ReadAsStringAsync();

        // Assert
        Assert.NotNull(message);

        // JSON Schema validation using JSONSchema.Net
        var json = JsonDocument.Parse(message);
        var validationResult = a2aSchema.Evaluate(json.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(validationResult.IsValid, $"JSON does not match schema: {validationResult.Details}");
    }

    [Fact]
    public async Task TestSetPushNotification()
    {
        // Arrange

        // Set up the response provider for push notification
        mockHandler.ResponseProvider = request =>
        {
            var pushNotificationResponse = new TaskPushNotificationConfig
            {
                TaskId = "test-task",
                PushNotificationConfig = new PushNotificationConfig
                {
                    Url = "http://example.org/notify",
                    Id = "response-config-id",
                    Token = "test-token",
                    Authentication = new PushNotificationAuthenticationInfo
                    {
                        Schemes = ["Bearer"]
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new JsonRpcContent(JsonRpcResponse.CreateJsonRpcResponse<object>("test-id", pushNotificationResponse))
            };
        };

        var pushNotificationConfig = new TaskPushNotificationConfig
        {
            TaskId = "test-task",
            PushNotificationConfig = new PushNotificationConfig()
            {
                Url = "http://example.org/notify",
                Id = "request-config-id",
                Token = "test-token",
                Authentication = new PushNotificationAuthenticationInfo()
                {
                    Schemes = ["Bearer"],
                }
            }
        };

        // Act
        await client.SetPushNotificationAsync(pushNotificationConfig);
        var message = await mockHandler!.Request!.Content!.ReadAsStringAsync();

        // Assert
        Assert.NotNull(message);

        // JSON Schema validation using JSONSchema.Net
        var json = JsonDocument.Parse(message);
        var validationResult = a2aSchema.Evaluate(json.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(validationResult.IsValid, $"JSON does not match schema: {validationResult.Details}");
    }

    public void Dispose()
    {
        httpClient?.Dispose();
    }
}

public class JsonSchemaFixture
{
    public JsonSchema Schema { get; }

    public JsonSchemaFixture()
    {
        var schemaText = File.ReadAllText("a2a.json");
        Schema = JsonSchema.FromText(schemaText);
    }
}
public class MockMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public Func<HttpRequestMessage, HttpResponseMessage>? ResponseProvider { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;

        // Use custom response provider if available, otherwise create default
        var response = ResponseProvider?.Invoke(request) ?? CreateDefaultResponse(request);

        return Task.FromResult(response);
    }

    private static HttpResponseMessage CreateDefaultResponse(HttpRequestMessage request)
    {
        // Create a default successful response with AgentTask
        var defaultResult = new AgentTask
        {
            Id = "dummy-task-id",
            ContextId = "dummy-context-id",
            Status = new AgentTaskStatus
            {
                State = TaskState.Completed,
            }
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new JsonRpcContent(JsonRpcResponse.CreateJsonRpcResponse<object>("test-id", defaultResult))
        };
    }
}