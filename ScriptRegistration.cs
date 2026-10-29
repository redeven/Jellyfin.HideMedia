using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyfinHideMedia;

public class ScriptRegistration : IHostedService
{
    private const string ResourceName = "JellyfinHideMedia.Web.hideMedia.js";

    private readonly ILogger<ScriptRegistration> _logger;

    public ScriptRegistration(ILogger<ScriptRegistration> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return Task.CompletedTask;
        }

        if (!JavaScriptInjectorBridge.IsAvailable)
        {
            _logger.LogWarning(
                "JavaScript Injector plugin not found. Install it to get the \"Hide from Home\" context-menu entry; Home filtering still works.");
            return Task.CompletedTask;
        }

        try
        {
            var scriptId = $"{plugin.Id}-hide-from-home";
            var payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["id"] = scriptId,
                ["name"] = "Hide from Home",
                ["script"] = ReadScript(),
                ["enabled"] = true,
                ["requiresAuthentication"] = true,
                ["pluginId"] = plugin.Id.ToString(),
                ["pluginName"] = plugin.Name,
                ["pluginVersion"] = plugin.Version.ToString()
            });

            JavaScriptInjectorBridge.UnregisterScript(scriptId);
            if (JavaScriptInjectorBridge.RegisterScript(payload))
            {
                _logger.LogInformation("Registered \"Hide from Home\" script with JavaScript Injector.");
            }
            else
            {
                _logger.LogWarning("JavaScript Injector rejected the \"Hide from Home\" script.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register the \"Hide from Home\" script with JavaScript Injector.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string ReadScript()
    {
        using var stream = typeof(ScriptRegistration).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
