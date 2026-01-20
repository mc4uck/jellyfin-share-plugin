using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Share;

/// <summary>
/// Patches Jellyfin's index.html on startup to inject the share plugin script.
/// This ensures the plugin works automatically after Jellyfin updates.
/// </summary>
public class IndexPatcher : IServerEntryPoint
{
    private readonly ILogger<IndexPatcher> _logger;
    private readonly IApplicationPaths _appPaths;
    private const string ScriptTag = "<script src=\"/plugins/share/loader.js\"></script>";
    private const string BodyEndTag = "</body>";

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexPatcher"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="appPaths">Application paths.</param>
    public IndexPatcher(ILogger<IndexPatcher> logger, IApplicationPaths appPaths)
    {
        _logger = logger;
        _appPaths = appPaths;
    }

    /// <inheritdoc />
    public Task RunAsync()
    {
        try
        {
            PatchIndexHtml();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to patch index.html for Jellyfin Share plugin");
        }

        return Task.CompletedTask;
    }

    private void PatchIndexHtml()
    {
        // Try common paths for index.html
        var possiblePaths = new[]
        {
            Path.Combine(_appPaths.WebPath, "index.html"),  // Primary: use Jellyfin's configured web path
            "/usr/share/jellyfin/web/index.html",           // Linux package / Docker LinuxServer
            "/jellyfin/jellyfin-web/index.html",            // Docker official
        };

        foreach (var indexPath in possiblePaths)
        {
            if (string.IsNullOrEmpty(indexPath) || !File.Exists(indexPath))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(indexPath);

                // Check if already patched
                if (content.Contains("plugins/share/loader.js"))
                {
                    _logger.LogInformation("Jellyfin Share: index.html already patched at {Path}", indexPath);
                    return;
                }

                // Check if we can find the </body> tag
                if (!content.Contains(BodyEndTag))
                {
                    _logger.LogWarning("Jellyfin Share: Could not find </body> tag in {Path}", indexPath);
                    continue;
                }

                // Inject script before </body>
                var patchedContent = content.Replace(BodyEndTag, ScriptTag + BodyEndTag);
                File.WriteAllText(indexPath, patchedContent);

                _logger.LogInformation("Jellyfin Share: Successfully patched index.html at {Path}", indexPath);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Jellyfin Share: No write permission for {Path}", indexPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Jellyfin Share: Error patching {Path}", indexPath);
            }
        }

        _logger.LogWarning("Jellyfin Share: Could not find or patch index.html. " +
            "Add the script manually via Dashboard → General → Branding, or ensure the web directory is writable.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
