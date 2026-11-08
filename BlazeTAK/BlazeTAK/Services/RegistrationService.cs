using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazeTAK.Services;

public interface IRegistrationService
{
    bool IsRegistrationEnabled { get; }
    void SetRegistrationEnabled(bool enabled);
}

public class RegistrationService : IRegistrationService
{
    private const string ConfigFileName = "register-state.conf";
    private readonly string _filePath;
    private readonly ILogger<RegistrationService>? _logger;
    private readonly object _lock = new();
    private volatile bool _isRegistrationEnabled = true;

    public RegistrationService(
        IHostEnvironment? env = null,
        ILogger<RegistrationService>? logger = null,
        string? configFilePath = null)
    {
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(configFilePath))
        {
            _filePath = configFilePath;
        }
        else if (env != null && !string.IsNullOrWhiteSpace(env.ContentRootPath))
        {
            _filePath = Path.Combine(env.ContentRootPath, ConfigFileName);
        }
        else
        {
            _filePath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        }

        LoadState();
    }

    public bool IsRegistrationEnabled => _isRegistrationEnabled;

    public void SetRegistrationEnabled(bool enabled)
    {
        lock (_lock)
        {
            _isRegistrationEnabled = enabled;
            SaveState(enabled);
        }
    }

    private void LoadState()
    {
        lock (_lock)
        {
            try
            {
                var pathToRead = _filePath;
                if (!File.Exists(pathToRead))
                {
                    if (File.Exists(ConfigFileName))
                    {
                        pathToRead = ConfigFileName;
                    }
                    else if (File.Exists(Path.Combine(AppContext.BaseDirectory, ConfigFileName)))
                    {
                        pathToRead = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
                    }
                }

                if (File.Exists(pathToRead))
                {
                    var text = File.ReadAllText(pathToRead).Trim();
                    if (bool.TryParse(text, out var parsed))
                    {
                        _isRegistrationEnabled = parsed;
                        _logger?.LogInformation("Loaded registration state '{State}' from '{Path}'.", parsed, pathToRead);
                        return;
                    }
                    else
                    {
                        _logger?.LogWarning("Invalid boolean value '{Text}' in '{Path}'. Defaulting to true.", text, pathToRead);
                    }
                }
                else
                {
                    _isRegistrationEnabled = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load registration state from '{Path}'. Defaulting to true.", _filePath);
                _isRegistrationEnabled = true;
            }
        }
    }

    private void SaveState(bool enabled)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_filePath, enabled ? "true" : "false");
            _logger?.LogInformation("Saved registration state '{State}' to '{Path}'.", enabled, _filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save registration state to '{Path}'.", _filePath);
        }
    }
}
