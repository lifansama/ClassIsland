﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClassIsland.Services;

public class PluginService : IPluginService
{
    private static readonly string PluginsRoot = @".\Plugins\";

    private static readonly string PluginManifestFileName = "manifest.yml";

    public static void InitializePlugins(HostBuilderContext context, IServiceCollection services)
    {
        if (!Directory.Exists(PluginsRoot))
        {
            Directory.CreateDirectory(PluginsRoot);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        foreach (var pluginDir in Directory.EnumerateDirectories(PluginsRoot))
        {
            var manifestPath = Path.Combine(pluginDir, PluginManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifestYaml = File.ReadAllText(manifestPath);
            var manifest = deserializer.Deserialize<PluginManifest>(manifestYaml);
            manifest.PluginFolderPath = Path.GetFullPath(pluginDir);
            IPluginService.LoadedPluginsInternal.Add(manifest);
            if (File.Exists(Path.Combine(pluginDir, ".disabled")))
            {
                continue;
            }

            try
            {
                var loadContext = new PluginLoadContext(Path.GetFullPath(pluginDir));
                var asm = loadContext.LoadFromAssemblyPath(
                    Path.GetFullPath(Path.Combine(pluginDir, manifest.EntranceAssembly)));
                var entrance = asm.ExportedTypes.FirstOrDefault(x =>
                    x.BaseType == typeof(PluginBase) ||
                    x.GetCustomAttributes().FirstOrDefault(a => a.GetType() == typeof(PluginEntrance)) != null);

                if (entrance == null)
                {
                    continue;
                }

                if (Activator.CreateInstance(entrance) is not PluginBase entranceObj)
                {
                    continue;
                }

                entranceObj.Initialize(context, services);
                services.AddSingleton(typeof(PluginBase), entranceObj);
                services.AddSingleton(entrance, entranceObj);
                Console.WriteLine($"Initialize plugin: {pluginDir}");
            }
            catch (Exception ex)
            {

            }
        }
    }
}