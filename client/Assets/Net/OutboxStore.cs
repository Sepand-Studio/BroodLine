using System;
using System.IO;
using System.Text;

namespace Broodline.Net
{
    /// Durable persistence for an `Outbox` - `client_architecture` section 7:
    /// "The outbox: pending mutations, durable across a kill." A kill or
    /// crash between `Save` calls loses at most the mutation currently being
    /// queued; nothing already persisted is lost, and nothing is re-minted -
    /// `Load` restores keys, attempts and backoff exactly as saved.
    ///
    /// Deliberately not JSON: this file is written on nearly every mutation
    /// and needs no schema evolution story beyond this one client, so a
    /// small fixed binary layout keeps it a few lines and dependency-free
    /// (`System.IO` only - the ban on it is scoped to `engine/`, not client
    /// code).
    public sealed class OutboxStore
    {
        private readonly string _path;

        public OutboxStore(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("An outbox store needs a path.", nameof(path));
            _path = path;
        }

        /// Overwrites the file with the full contents of `box`, oldest
        /// first. Called after every `Enqueue`, `Ack` and `Fail` so a kill
        /// mid-flush never loses or double-persists an entry.
        public void Save(Outbox box)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Write to a temp file and replace atomically, so a kill mid-write
            // cannot leave a half-written, unreadable outbox behind.
            var tempPath = _path + ".tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                var entries = box.AllEntries;
                writer.Write(entries.Count);
                foreach (var entry in entries)
                {
                    writer.Write(entry.Key);
                    writer.Write(entry.Op);
                    writer.Write(entry.Body.Length);
                    if (entry.Body.Length > 0) writer.Write(entry.Body);
                    writer.Write(entry.CreatedAt.ToUniversalTime().Ticks);
                    writer.Write(entry.Attempts);
                    writer.Write(entry.NotBefore.ToUniversalTime().Ticks);
                }
            }

            // Replace atomically - there must be no window where `_path`
            // does not exist. The earlier `Delete` then `Move` left exactly
            // that window open: a kill between the two would have lost the
            // whole previously-persisted queue, not just the entry being
            // saved, which is what the class comment above promises.
            //
            // File.Move's 3-arg (overwrite:) overload is .NET Standard 2.1+
            // and this project's api compatibility level rejects it (CS1739
            // - confirmed by trying it). File.Replace is available instead
            // and is exactly this operation for the common case; it only
            // requires the destination to already exist, so the very first
            // save (no `_path` yet) falls back to a plain Move, which is
            // already atomic when there is nothing to replace.
            if (File.Exists(_path))
            {
                File.Replace(tempPath, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, _path);
            }
        }

        /// Rebuilds an `Outbox` from disk. An empty (or missing) file - a
        /// fresh install, or a player with nothing pending - loads as an
        /// empty outbox rather than an error.
        public Outbox Load()
        {
            var box = new Outbox();
            if (!File.Exists(_path)) return box;

            using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var key = reader.ReadString();
                    var op = reader.ReadString();
                    var bodyLength = reader.ReadInt32();
                    var body = bodyLength > 0 ? reader.ReadBytes(bodyLength) : Array.Empty<byte>();
                    var createdAt = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                    var attempts = reader.ReadInt32();
                    var notBefore = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);

                    box.Enqueue(OutboxEntry.Restore(key, op, body, createdAt, attempts, notBefore));
                }
            }
            return box;
        }
    }
}
