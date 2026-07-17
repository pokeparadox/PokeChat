using System.Net.Http.Json;

var baseUrl = Environment.GetEnvironmentVariable("POKECHAT_API_URL") ?? "http://localhost:5000";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

// Check API health
try
{
    var health = await http.GetAsync("/health");
    if (!health.IsSuccessStatusCode)
    {
        Console.WriteLine("Error: API is not reachable. Make sure the API is running.");
        Console.WriteLine($"  Expected at: {baseUrl}");
        return;
    }
}
catch (Exception ex)
{
    Console.WriteLine("Error: Cannot connect to the API.");
    Console.WriteLine($"  Expected at: {baseUrl}");
    Console.WriteLine($"  {ex.Message}");
    Console.WriteLine("\nStart the API with: dotnet run --project Api/");
    return;
}

// Create session
var sessionResponse = await http.PostAsJsonAsync("/sessions", new { user_name = (string?)null });
if (!sessionResponse.IsSuccessStatusCode)
{
    var errorBody = await sessionResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"Error: Session creation failed ({(int)sessionResponse.StatusCode} {sessionResponse.ReasonPhrase}).");
    if (!string.IsNullOrWhiteSpace(errorBody))
        Console.WriteLine($"  {errorBody}");
    return;
}
var session = await sessionResponse.Content.ReadFromJsonAsync<SessionCreated>();
var sessionId = session?.session_id ?? Guid.NewGuid().ToString();

Console.WriteLine("Welcome to PokeChat!");
Console.WriteLine("A chat bot that learns from you!");
Console.WriteLine("Type 'quit' or 'exit' to leave.");
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) continue;
    if (input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    try
    {
        var response = await http.PostAsJsonAsync($"/sessions/{sessionId}/chat", new { message = input, working_directory = Environment.CurrentDirectory });
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error: {errorBody}");
            continue;
        }
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        if (!string.IsNullOrEmpty(result?.response))
            Console.WriteLine(result.response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

// End session
try
{
    await http.DeleteAsync($"/sessions/{sessionId}");
}
catch { /* best effort */ }

Console.WriteLine("Goodbye!");

record SessionCreated(string session_id);
record ChatResponse(string response, string session_id);
