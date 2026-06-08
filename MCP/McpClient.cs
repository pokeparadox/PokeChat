using System.Diagnostics;
using System.Text.Json;
using PokeChat.Tools;

namespace PokeChat.Mcp;

public class McpClient : IDisposable
{
    private readonly string _command;
    private readonly string[] _args;
    private readonly int _timeoutMs;
    private Process? _process;
    private StreamWriter? _stdinWriter;
    private StreamReader? _stdoutReader;
    private int _requestId;
    private bool _connected;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public McpClient(string command, string[] args, int timeoutMs = 10000)
    {
        _command = command;
        _args = args;
        _timeoutMs = timeoutMs;
    }

    public bool Connect()
    {
        if (_connected) return true;

        try
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _command,
                    Arguments = string.Join(" ", _args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            _process.Start();
            _stdinWriter = _process.StandardInput;
            _stdoutReader = _process.StandardOutput;

            var result = SendRequest("initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "PokeChat", version = "1.0" }
            });

            if (result == null || result.Error != null)
                return false;

            _connected = true;
            return true;
        }
        catch
        {
            Cleanup();
            return false;
        }
    }

    public List<McpToolAdapter> DiscoverTools()
    {
        var tools = new List<McpToolAdapter>();

        if (!_connected) return tools;

        try
        {
            var response = SendRequest("tools/list", new { });
            if (response?.Result == null) return tools;

            var result = response.Result.Value;
            if (!result.TryGetProperty("tools", out var toolsElement))
                return tools;

            foreach (var toolElement in toolsElement.EnumerateArray())
            {
                var schema = JsonSerializer.Deserialize<McpToolSchema>(toolElement.GetRawText(), JsonOptions);
                if (schema == null || string.IsNullOrEmpty(schema.Name)) continue;

                tools.Add(new McpToolAdapter(this, schema.Name, schema.Description));
            }
        }
        catch
        {
            // Connection may be broken
        }

        return tools;
    }

    public ToolResult ExecuteTool(string toolName, string[] args)
    {
        if (!_connected)
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = "not connected"
            };
        }

        try
        {
            var response = SendRequest("tools/call", new
            {
                name = toolName,
                arguments = BuildArguments(args),
            });

            if (response == null)
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorMessage = "no response"
                };
            }

            if (response.Error != null)
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorMessage = $"MCP error: {response.Error.Message}",
                };
            }

            if (response.Result == null)
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorMessage = "empty result",
                };
            }

            var result = response.Result.Value;
            var isError = result.TryGetProperty("isError", out var isErr) && isErr.GetBoolean();

            var content = "";
            if (result.TryGetProperty("content", out var contentElement))
            {
                foreach (var item in contentElement.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeEl) &&
                        typeEl.GetString() == "text" &&
                        item.TryGetProperty("text", out var textEl))
                    {
                        content += textEl.GetString();
                    }
                }
            }

            return new ToolResult
            {
                Success = !isError,
                Output = content ?? "",
                ErrorMessage = isError ? "tool returned error" : "",
            };
        }
        catch (OperationCanceledException)
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = "timeout",
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    private Dictionary<string, object>? BuildArguments(string[] args)
    {
        if (args.Length == 0) return null;

        var result = new Dictionary<string, object>();
        if (args.Length == 1 && !args[0].Contains('='))
        {
            result["query"] = args[0];
        }
        else
        {
            for (var i = 0; i < args.Length; i++)
            {
                result[$"arg{i}"] = args[i];
            }
        }
        return result;
    }

    private JsonRpcResponse? SendRequest(string method, object? parameters)
    {
        if (_process?.HasExited == true)
        {
            _connected = false;
            return null;
        }

        var id = Interlocked.Increment(ref _requestId);
        var request = new JsonRpcRequest
        {
            Id = id,
            Method = method,
            Params = parameters,
        };

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        using var cts = new CancellationTokenSource(_timeoutMs);

        try
        {
            _stdinWriter?.WriteLine(requestJson);
            _stdinWriter?.Flush();

            var responseTask = _stdoutReader?.ReadLineAsync();
            if (responseTask == null) return null;

            if (!responseTask.Wait(_timeoutMs, cts.Token))
                return null;

            var responseLine = responseTask.Result;
            if (string.IsNullOrEmpty(responseLine)) return null;

            var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseLine, JsonOptions);
            return response;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            _connected = false;
            return null;
        }
    }

    private void Cleanup()
    {
        try
        {
            _stdinWriter?.Close();
        }
        catch { }

        try
        {
            _stdoutReader?.Close();
        }
        catch { }

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch { }
            _process.WaitForExit(2000);
            _process.Close();
        }

        _stdinWriter = null;
        _stdoutReader = null;
        _process = null;
        _connected = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
    }
}
