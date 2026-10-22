using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Order.Voice;
using UnityEngine;

namespace Order.Bootstrap
{
    // Phrasebook file in a per-profile folder. With Steam, the profile becomes the SteamID and Steam Auto-Cloud
    // syncs "profiles/<id>/phrasebook.json"; the exchange folder is for manual import/export and is not synced.
    public sealed class FilePhrasebookStore : IPhrasebookStore
    {
        [Serializable]
        private sealed class Document
        {
            public int Version = 1;
            public List<PhraseEntry> Entries = new List<PhraseEntry>();
        }

        private readonly string folder;
        private readonly string file;
        private readonly string exchange;

        public FilePhrasebookStore(string root, string profile = "local")
        {
            folder = Path.Combine(root, "profiles", profile);
            file = Path.Combine(folder, "phrasebook.json");
            exchange = Path.Combine(root, "phrasebook-exchange");
        }

        public string Location => file;
        public string ImportFolder => Path.Combine(exchange, "import");

        public List<PhraseEntry> Load()
        {
            if (!File.Exists(file)) return new List<PhraseEntry>();
            try { return Read(file); }
            catch (Exception)
            {
                // Keep a damaged file for inspection instead of overwriting it with an empty phrasebook.
                File.Move(file, Path.Combine(folder, $"phrasebook.damaged-{DateTime.Now:yyyyMMdd-HHmmss}.json"));
                return new List<PhraseEntry>();
            }
        }

        public void Save(IReadOnlyList<PhraseEntry> entries)
        {
            Directory.CreateDirectory(folder);
            var temporary = file + ".tmp";
            File.WriteAllText(temporary, Write(entries));
            if (File.Exists(file)) File.Replace(temporary, file, null);
            else File.Move(temporary, file);
        }

        public string Export(IReadOnlyList<PhraseEntry> entries)
        {
            Directory.CreateDirectory(exchange);
            var path = Path.Combine(exchange, $"phrasebook-{DateTime.Now:yyyy-MM-dd-HHmmss}.json");
            File.WriteAllText(path, Write(entries));
            return path;
        }

        public List<PhraseEntry> TakeImports(out int files)
        {
            files = 0;
            var result = new List<PhraseEntry>();
            Directory.CreateDirectory(ImportFolder);
            var done = Path.Combine(ImportFolder, "imported");
            foreach (var path in Directory.GetFiles(ImportFolder, "*.json").OrderBy(p => p))
            {
                try { result.AddRange(Read(path)); }
                catch (Exception) { continue; }
                files++;
                Directory.CreateDirectory(done);
                var target = Path.Combine(done, $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(path, target);
            }
            return result;
        }

        private static List<PhraseEntry> Read(string path)
        {
            var document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
            if (document == null || document.Entries == null) throw new InvalidDataException("Not a phrasebook file.");
            return document.Entries;
        }

        private static string Write(IReadOnlyList<PhraseEntry> entries) =>
            JsonUtility.ToJson(new Document { Entries = entries.ToList() }, true);
    }
}
