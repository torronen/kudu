﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.SiteExtensions;
using Kudu.Core.Tracing;
using Newtonsoft.Json;

namespace Kudu.Core.Settings
{
    public class DiagnosticsSettingsManager
    {
        private readonly string _path;
        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;

        public DiagnosticsSettingsManager(string path, IEnvironment environment, ITracer tracer)
        {
            _path = path;
            _environment = environment;
            _tracer = tracer;
        }

        public object GetSetting(string key)
        {
            DiagnosticsSettings settings = ReadSettings();
            return settings.GetSetting(key);
        }

        public DiagnosticsSettings GetSettings()
        {
            return ReadSettings();
        }

        public void UpdateSettings(DiagnosticsSettings settings)
        {
            DiagnosticsSettings diagnosticsSettings = ReadSettings();
            foreach (KeyValuePair<string, object> pair in settings)
            {
                diagnosticsSettings.SetSetting(pair.Key, pair.Value);
            }

            SaveSettings(diagnosticsSettings);
        }

        public bool DeleteSetting(string key)
        {
            DiagnosticsSettings diagnosticsSettings = ReadSettings();
            if (diagnosticsSettings.RemoveSetting(key))
            {
                SaveSettings(diagnosticsSettings);
                return true;
            }

            return false;
        }

        private DiagnosticsSettings ReadSettings()
        {
            if (FileSystemHelpers.FileExists(_path))
            {
                try
                {
                    string fileContent = null;
                    OperationManager.Attempt(() => fileContent = FileSystemHelpers.ReadAllTextFromFile(_path));
                    return JsonConvert.DeserializeObject<DiagnosticsSettings>(fileContent);
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex);
                }
            }

            return new DiagnosticsSettings();
        }

        private void SaveSettings(DiagnosticsSettings diagnosticsSettings)
        {
            if (!FileSystemHelpers.FileExists(_path))
            {
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(_path));
            }

            string fileContent = JsonConvert.SerializeObject(diagnosticsSettings);
            FileSystemHelpers.WriteAllTextToFile(_path, fileContent);
        }

        private void ApplySettings(DiagnosticsSettings diagnosticsSettings)
        {
            SetNodeDebugging(diagnosticsSettings.GetSetting("NodeInspectorUrl"));
        }

        private void SetNodeDebugging(object nodeInspectorUrl)
        {
            if (nodeInspectorUrl is string)
            {
                var url = nodeInspectorUrl as string;
                
                string destinationDirectory = Path.Combine(_environment.SiteExtensionsRootPath, "EnableNodeDebugging");

                if (url == String.Empty)
                {
                    FileSystemHelpers.DeleteDirectorySafe(destinationDirectory);


                }
                else
                {
                    var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Kudu.Core.Scripts.EnableNodeDebugging.xml");

                    FileSystemHelpers.CreateDirectory(destinationDirectory);

                    using (Stream file = File.Create(Path.Combine(destinationDirectory, Constants.ApplicationHostFile)))
                    {
                        source.CopyTo(file);
                    }
                }
            }
        }
    }
}