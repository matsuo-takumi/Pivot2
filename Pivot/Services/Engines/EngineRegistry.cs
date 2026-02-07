using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Pivot.Services.Engines
{
    /// <summary>
    /// Registry for managing asset engines.
    /// Provides methods to register engines and retrieve the appropriate engine for a given file extension.
    /// </summary>
    public class EngineRegistry
    {
        private readonly ILogger<EngineRegistry> _logger;
        // Map: Extension -> Engine
        private readonly ConcurrentDictionary<string, IAssetEngine> _extensionMap = new();
        private readonly List<IAssetEngine> _engines = new();

        public EngineRegistry(IEnumerable<IAssetEngine> engines, ILogger<EngineRegistry> logger)
        {
            _logger = logger;
            foreach (var engine in engines)
            {
                RegisterEngine(engine);
            }
        }

        /// <summary>
        /// Register an engine and map its supported extensions.
        /// If multiple engines support the same extension, the one with higher priority wins.
        /// </summary>
        public void RegisterEngine(IAssetEngine engine)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            _engines.Add(engine);

            foreach (var ext in engine.SupportedExtensions)
            {
                var lowerExt = ext.ToLowerInvariant();
                
                // Check for conflict
                if (_extensionMap.TryGetValue(lowerExt, out var existing))
                {
                    if (engine.Priority > existing.Priority)
                    {
                        _extensionMap[lowerExt] = engine;
                        _logger.LogInformation("Overriding engine for {Extension}: {OldEngine} -> {NewEngine}", 
                            ext, existing.GetType().Name, engine.GetType().Name);
                    }
                }
                else
                {
                    _extensionMap[lowerExt] = engine;
                }
            }
            
            _logger.LogInformation("Registered engine: {EngineType} (Extensions: {Count})", 
                engine.GetType().Name, engine.SupportedExtensions.Length);
        }

        /// <summary>
        /// Get the appropriate engine for a given file extension.
        /// </summary>
        /// <param name="extension">File extension (including dot).</param>
        /// <returns>Matching engine, or null if no engine supports this extension.</returns>
        public IAssetEngine? GetEngine(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return null;
            
            _extensionMap.TryGetValue(extension.ToLowerInvariant(), out var engine);
            return engine;
        }

        /// <summary>
        /// Get all registered engines.
        /// </summary>
        public IReadOnlyList<IAssetEngine> GetAllEngines() => _engines.AsReadOnly();
    }
}
