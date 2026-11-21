using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;

namespace BadNorth.ModMenu
{
    public static class ModMenuBootstrap
    {
        public const string MenuId = "local.badnorth.mod-menu";
        private const string DisabledSuffix = ".disabled";
        private const string SaveCompatibilityId = "local.badnorth.save-compatibility";
        private static ManualLogSource log;

        public static bool CatalogueReady { get; private set; }
        public static IEnumerable<LocalMod> Plugins { get; private set; } = new LocalMod[0];

        // This patcher processes local plugin files before the chainloader discovers them.
        public static IEnumerable<string> TargetDLLs => new string[0];
        public static void Patch(AssemblyDefinition assembly) { }

        public static void Finish()
        {
            log = Logger.CreateLogSource("Bad North Mod Menu");
            try
            {
                string root = Path.GetFullPath(Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (!Directory.Exists(root)) return;
                if (IsLink(File.GetAttributes(root)))
                {
                    log.LogWarning("Skipping plugin selection because the plugin directory is a link.");
                    return;
                }

                var selection = PluginSelection.Read();
                var mods = FindMods(root).OrderBy(mod => mod.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
                foreach (LocalMod mod in mods)
                    ApplySelection(mod, selection);

                Plugins = mods;
                CatalogueReady = true;
                log.LogInfo("Local plugin catalogue ready: " + mods.Length + " files.");
            }
            catch (Exception error)
            {
                log.LogError("Could not prepare the plugin selection: " + error);
            }
        }

        private static IEnumerable<LocalMod> FindMods(string root)
        {
            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in EnumeratePluginFiles(root))
            {
                bool disabled = file.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                string activePath = disabled ? file.Substring(0, file.Length - DisabledSuffix.Length) : file;
                if (!disabled || !candidates.ContainsKey(activePath))
                    candidates[activePath] = file;
            }

            foreach (var candidate in candidates)
            {
                var mod = ReadMod(root, candidate.Key, candidate.Value);
                if (mod != null) yield return mod;
            }
        }

        private static IEnumerable<string> EnumeratePluginFiles(string root)
        {
            var directories = new Stack<string>();
            directories.Push(root);
            while (directories.Count != 0)
            {
                foreach (string entry in Directory.GetFileSystemEntries(directories.Pop()))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if (IsLink(attributes) || !entry.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    if ((attributes & FileAttributes.Directory) != 0)
                        directories.Push(entry);
                    else if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        || entry.EndsWith(".dll" + DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                        yield return entry;
                }
            }
        }

        private static bool IsLink(FileAttributes attributes) => (attributes & FileAttributes.ReparsePoint) != 0;

        private static LocalMod ReadMod(string root, string activePath, string file)
        {
            try
            {
                using (var module = ModuleDefinition.ReadModule(file))
                {
                    var metadata = module.Types.SelectMany(type => type.CustomAttributes)
                        .Where(attribute => attribute.AttributeType.FullName == "BepInEx.BepInPlugin")
                        .ToArray();
                    if (metadata.Length == 0) return null;

                    string key = activePath.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
                    var ids = metadata.Select(attribute => (string)attribute.ConstructorArguments[0].Value).ToArray();
                    string name = string.Join(" / ", metadata.Select(attribute =>
                        attribute.ConstructorArguments[1].Value + " " + attribute.ConstructorArguments[2].Value).ToArray());
                    return new LocalMod(key, activePath, name, ids.Contains(MenuId), ids.Contains(SaveCompatibilityId),
                        !file.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase),
                        File.Exists(activePath) && File.Exists(activePath + DisabledSuffix));
                }
            }
            catch (BadImageFormatException)
            {
                return null;
            }
            catch (Exception error)
            {
                log.LogWarning("Could not inspect " + Path.GetFileName(file) + ": " + error.Message);
                return null;
            }
        }

        private static void ApplySelection(LocalMod mod, Dictionary<string, bool> selection)
        {
            if (mod.ConflictingFiles || !selection.TryGetValue(mod.Key, out bool enable) || mod.IsEnabled == enable
                || mod.IsMenu) return;

            string disabledPath = mod.ActivePath + DisabledSuffix;
            string source = enable ? disabledPath : mod.ActivePath;
            string destination = enable ? mod.ActivePath : disabledPath;
            try
            {
                File.Move(source, destination);
                mod.IsEnabled = enable;
                log.LogInfo((enable ? "Enabled for this launch: " : "Disabled for this launch: ") + mod.Key);
            }
            catch (Exception error)
            {
                mod.ChangeFailed = true;
                log.LogWarning("Could not change " + mod.Key + ": " + error.Message);
            }
        }
    }

    public sealed class LocalMod
    {
        internal LocalMod(string key, string activePath, string name, bool isMenu, bool isSaveCompatibility,
            bool isEnabled, bool conflictingFiles)
        {
            Key = key;
            ActivePath = activePath;
            Name = name;
            IsMenu = isMenu;
            IsSaveCompatibility = isSaveCompatibility;
            IsEnabled = isEnabled;
            ConflictingFiles = conflictingFiles;
        }

        public string Key { get; }
        public string ActivePath { get; }
        public string Name { get; }
        public bool IsMenu { get; }
        public bool IsSaveCompatibility { get; }
        public bool IsEnabled { get; internal set; }
        public bool ConflictingFiles { get; }
        public bool ChangeFailed { get; internal set; }
    }

    public static class PluginSelection
    {
        private static string SelectionPath =>
            Path.Combine(Paths.ConfigPath, ModMenuBootstrap.MenuId + ".selection.txt");

        public static Dictionary<string, bool> Read()
        {
            var selection = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(SelectionPath)) return selection;

            foreach (string line in File.ReadAllLines(SelectionPath))
            {
                if (line.Length >= 2 && (line[0] == '+' || line[0] == '-'))
                    selection[line.Substring(1)] = line[0] == '+';
            }
            return selection;
        }

        public static void Save(Dictionary<string, bool> selection)
        {
            string path = SelectionPath;
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var lines = selection.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => (entry.Value ? "+" : "-") + entry.Key).ToArray();
                File.WriteAllLines(temporaryPath, lines);
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
