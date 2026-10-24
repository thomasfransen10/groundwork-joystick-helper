using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace GroundworkJoystickHelper
{
    public sealed class Vdf
    {
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string Text(string key) { object v; return Values.TryGetValue(key, out v) ? v as string : null; }
        public Vdf Node(string key) { object v; return Values.TryGetValue(key, out v) ? v as Vdf : null; }
        public static Vdf Parse(string text)
        {
            int p = 0;
            return ReadNode(text, ref p, false);
        }
        private static void Skip(string s, ref int p)
        {
            while (p < s.Length)
            {
                if (Char.IsWhiteSpace(s[p]) || s[p] == '\ufeff') { p++; continue; }
                if (p + 1 < s.Length && s[p] == '/' && s[p+1] == '/')
                { while (p < s.Length && s[p] != '\n') p++; continue; }
                break;
            }
        }
        private static string Token(string s, ref int p)
        {
            Skip(s, ref p);
            if (p >= s.Length) throw new FormatException("Incomplete Steam library file.");
            if (s[p] != '"')
            {
                int start = p;
                while (p < s.Length && !Char.IsWhiteSpace(s[p]) && s[p] != '{' && s[p] != '}') p++;
                if (start == p) throw new FormatException("Unexpected Steam library token.");
                return s.Substring(start, p-start);
            }
            p++;
            StringBuilder result = new StringBuilder();
            while (p < s.Length)
            {
                char c = s[p++];
                if (c == '"') return result.ToString();
                if (c == '\\' && p < s.Length)
                {
                    char n = s[p];
                    if (n == '\\' || n == '"') { result.Append(n); p++; continue; }
                }
                result.Append(c);
            }
            throw new FormatException("Unterminated Steam library string.");
        }
        private static Vdf ReadNode(string s, ref int p, bool nested)
        {
            Vdf node = new Vdf();
            while (true)
            {
                Skip(s, ref p);
                if (p >= s.Length)
                { if (nested) throw new FormatException("Unterminated Steam library section."); return node; }
                if (s[p] == '}')
                { if (!nested) throw new FormatException("Unexpected Steam section end."); p++; return node; }
                string key = Token(s, ref p);
                Skip(s, ref p);
                if (p < s.Length && s[p] == '{') { p++; node.Values[key] = ReadNode(s, ref p, true); }
                else node.Values[key] = Token(s, ref p);
            }
        }
    }

    public static class SteamFinder
    {
        public const string AppId = "5306540";
        public static List<string> KnownRoots()
        {
            List<string> roots = new List<string>();
            Add(roots, Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string);
            Add(roots, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string);
            Add(roots, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string);
            Add(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            return roots;
        }
        private static void Add(List<string> paths, string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                path = Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));
                if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase)) paths.Add(path);
            }
            catch (ArgumentException) { } catch (NotSupportedException) { }
        }
        public static List<string> Find(IEnumerable<string> roots)
        {
            List<string> libraries = new List<string>();
            foreach (string root in roots)
            {
                Add(libraries, root);
                string libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFile)) continue;
                try
                {
                    Vdf data = Vdf.Parse(File.ReadAllText(libraryFile)).Node("libraryfolders");
                    if (data == null) continue;
                    foreach (KeyValuePair<string,object> pair in data.Values)
                    {
                        Vdf item = pair.Value as Vdf;
                        if (item != null) Add(libraries, item.Text("path"));
                        else { int number; if (Int32.TryParse(pair.Key, out number)) Add(libraries, pair.Value as string); }
                    }
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { } catch (FormatException) { }
            }
            List<string> games = new List<string>();
            foreach (string library in libraries)
            {
                string manifest = Path.Combine(library, "steamapps", "appmanifest_" + AppId + ".acf");
                if (!File.Exists(manifest)) continue;
                try
                {
                    Vdf state = Vdf.Parse(File.ReadAllText(manifest)).Node("AppState");
                    if (state == null || state.Text("appid") != AppId) continue;
                    string dir = state.Text("installdir");
                    if (String.IsNullOrWhiteSpace(dir) || Path.IsPathRooted(dir)) continue;
                    string common = Path.GetFullPath(Path.Combine(library, "steamapps", "common")) + Path.DirectorySeparatorChar;
                    string exe = Path.GetFullPath(Path.Combine(common, dir, "game.exe"));
                    if (!exe.StartsWith(common, StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(exe)) Add(games, exe);
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { } catch (FormatException) { }
                catch (ArgumentException) { } catch (NotSupportedException) { }
            }
            return games;
        }
    }

    public static class Compatibility
    {
        public const string Version = "0.4.0";
        public const string Build = "26171350 / 0.1.1";
        public const string OriginalHash = "6B3703C22C8B0F67A2D35089A22E9B202921B1F466821D3DB16097D8A8EC5898";
        public const int PatchOffset = 0x04405BE4;
        public const string CopyName = "game-joystickhelper.exe";
        public static string DataRoot
        { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroundworkJoystickHelper"); } }
        public static string Hash(byte[] data)
        { using (SHA256 algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(data)).Replace("-", ""); }
        public static bool Supported(string path)
        { return File.Exists(path) && Hash(File.ReadAllBytes(path)) == OriginalHash; }
        public static byte[] Patch(byte[] original)
        {
            if (Hash(original) != OriginalHash)
                throw new InvalidOperationException("This game version is not supported. No game file has been changed.\nSupported build: " + Build);
            if (original[PatchOffset] != 0x89 || original[PatchOffset+1] != 0xC3)
                throw new InvalidOperationException("The expected controller-name filter was not found.");
            byte[] copy = (byte[])original.Clone();
            // Last unsuccessful name check: MOV EBX,EAX -> MOV BL,1.
            // Successful checks already set BL=1. Keep all cleanup and unwind code.
            // This accepts arbitrary names, including localized and non-ASCII names,
            // only within this game's existing Windows gaming-controller pipeline.
            copy[PatchOffset] = 0xB3;
            copy[PatchOffset+1] = 0x01;
            return copy;
        }
        public static string Prepare(string originalPath)
        {
            originalPath = Path.GetFullPath(originalPath);
            byte[] original = File.ReadAllBytes(originalPath);
            byte[] patched = Patch(original);
            string destination = Path.Combine(Path.GetDirectoryName(originalPath), CopyName);
            string expected = Hash(patched);
            WriteVerifiedCopy(patched,destination);
            if (!Supported(originalPath)) throw new IOException("The original game file changed during preparation.");
            Log("Prepared build " + Build + "; original=" + OriginalHash + "; copy=" + expected);
            return destination;
        }
        internal static void WriteVerifiedCopy(byte[] patched,string destination)
        {
            string expected=Hash(patched);
            if (File.Exists(destination))
            {
                if (Hash(File.ReadAllBytes(destination)) != expected)
                    throw new IOException("A different file already exists at " + destination + ". It has not been overwritten.");
            }
            else
            {
                using (FileStream stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                    stream.Write(patched, 0, patched.Length);
                if (Hash(File.ReadAllBytes(destination)) != expected)
                    throw new IOException("The compatible copy did not pass verification.");
            }
        }
        public static bool IsRunning(string originalPath)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(originalPath));
            foreach (string name in new[] { "game", "game-joystickfix", "game-joystickhelper" })
                foreach (Process p in Process.GetProcessesByName(name))
                    using (p)
                    {
                        try
                        {
                            if (String.Equals(Path.GetDirectoryName(p.MainModule.FileName), folder, StringComparison.OrdinalIgnoreCase)) return true;
                        }
                        catch (System.ComponentModel.Win32Exception) { }
                        catch (InvalidOperationException) { }
                    }
            return false;
        }
        public static ProcessStartInfo StartInfo(string executable)
        {
            ProcessStartInfo info = new ProcessStartInfo(executable);
            info.WorkingDirectory = Path.GetDirectoryName(executable);
            info.UseShellExecute = false;
            info.EnvironmentVariables["SteamAppId"] = SteamFinder.AppId;
            info.EnvironmentVariables["SteamGameId"] = SteamFinder.AppId;
            return info;
        }
        public static string BackupSettings()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"groundwork-simulator\groundwork-simulator\config\settings.ron");
            if (!File.Exists(path)) return null;
            string folder = Path.Combine(DataRoot, "backups");
            Directory.CreateDirectory(folder);
            string destination = Path.Combine(folder, "settings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".ron");
            File.Copy(path, destination, false);
            return destination;
        }
        public static void Log(string message)
        {
            Directory.CreateDirectory(DataRoot);
            File.AppendAllText(Path.Combine(DataRoot, "helper.log"), DateTimeOffset.Now.ToString("o") + " " + message + Environment.NewLine, Encoding.UTF8);
        }
    }
}

