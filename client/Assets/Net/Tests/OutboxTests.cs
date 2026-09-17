using System;
using System.Collections.Generic;
using System.IO;
using Broodline.Net;
using NUnit.Framework;

namespace Broodline.Net.Tests
{
    /// Pure, EditMode, no network - `Outbox` and `OutboxStore` only.
    /// `OutboxClient` (the networking half) is exercised against the real
    /// server per the brief's Step 4, not here.
    public class OutboxTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly byte[] body = { 9 };
        private static readonly byte[] b = { 8 };
        private static readonly byte[] b1 = { 1 };
        private static readonly byte[] b2 = { 2 };

        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "outbox-tests-" + Guid.NewGuid().ToString("N") + ".bin");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            var tempTemp = _tempPath + ".tmp";
            if (File.Exists(tempTemp)) File.Delete(tempTemp);
        }

        [Test]
        public void TheKeyIsGeneratedWhenTheActionIsTaken_AndNeverChanges()
        {
            var box = new Outbox();
            var e = OutboxEntry.For("wave/submit", body, now: T0);
            box.Enqueue(e);
            Assert.AreEqual(e.Key, box.Next(T0).Key);
            box.Fail(e.Key, T0);
            Assert.AreEqual(e.Key, box.Next(T0.AddMinutes(5)).Key); // a retry sends the IDENTICAL key
        }

        [Test]
        public void EntriesDrainOldestFirst_AndALaterOneNeverOvertakesAFailedEarlierOne()
        {
            // "A splice that depends on a wave reward must not overtake it."
            var box = new Outbox();
            box.Enqueue(OutboxEntry.For("wave/submit", b1, T0));
            box.Enqueue(OutboxEntry.For("splice/commit", b2, T0.AddSeconds(1)));

            var first = box.Next(T0.AddSeconds(2));
            Assert.IsNotNull(first, "expected the wave/submit head to be due");
            box.Fail(first.Key, T0.AddSeconds(2));

            Assert.IsNull(box.Next(T0.AddSeconds(3))); // head is backing off; nothing behind it goes
            Assert.AreEqual(first.Key, box.Next(T0.AddSeconds(60)).Key);
        }

        // The brief's own version of this test calls `box.Next(T0.AddHours(1))`
        // with the SAME `now` on every iteration, then `Fail`s whatever comes
        // back. After the very first `Fail` sets `NotBefore = now + 2s`, the
        // next `Next(T0.AddHours(1))` call returns null - by design, per
        // `EntriesDrainOldestFirst...`'s `Assert.IsNull(box.Next(T0.AddSeconds(3)))`,
        // which requires exactly that: `Next` must not return a head that is
        // still backing off. So the brief's loop dereferences null on its
        // second iteration and cannot run at all.
        //
        // The property it is reaching for is real - successive failures back
        // off 2, 4, 8, 16, 32, 60 seconds (2^Attempts, capped at 60) - so this
        // version advances `now` to exactly the entry's new `NotBefore` after
        // each `Fail`, instead of holding it fixed. That makes `Next` return
        // the (still-only) entry again each iteration - due to a proof, not
        // an empty queue - so all six failures are actually exercised.
        [Test]
        public void BackoffIsExponentialAndCapped()
        {
            var e = OutboxEntry.For("x", b, T0);
            var box = new Outbox();
            box.Enqueue(e);

            var delays = new List<double>();
            var now = T0;
            for (var i = 0; i < 6; i++)
            {
                var n = box.Next(now);
                Assert.IsNotNull(n, $"expected the entry to still be due at iteration {i} - a null here would mean it went missing, not that it backed off");
                Assert.AreEqual(e.Key, n.Key);

                box.Fail(n.Key, now);
                delays.Add((box.Peek().NotBefore - now).TotalSeconds);
                now = box.Peek().NotBefore; // advance to exactly when the backoff elapses, then fail again
            }

            CollectionAssert.AreEqual(new double[] { 2, 4, 8, 16, 32, 60 }, delays); // 2^Attempts seconds, capped at 60
        }

        [Test]
        public void EntriesOlderThanTheIdempotencyWindow_AreDroppedWithANotice_NotReplayed()
        {
            var box = new Outbox();
            box.Enqueue(OutboxEntry.For("wave/submit", b, T0));

            var dropped = box.Expire(T0.AddHours(24).AddSeconds(1));
            Assert.AreEqual(1, dropped.Count);
            Assert.IsNull(box.Next(T0.AddHours(25)));
            StringAssert.Contains("did not happen", dropped[0].Notice);
        }

        [Test]
        public void PersistenceRoundTrips_SoAKillLosesNothing()
        {
            var box = new Outbox();
            var first = OutboxEntry.For("wave/submit", b1, T0);
            box.Enqueue(first);
            var second = OutboxEntry.For("splice/commit", b2, T0.AddSeconds(1));
            box.Enqueue(second);
            // Give the head some non-default Attempts/NotBefore too, so the
            // round trip has to carry more than an untouched default across
            // the kill to pass.
            box.Fail(first.Key, T0.AddSeconds(2));

            var store = new OutboxStore(_tempPath);
            store.Save(box);
            var back = store.Load();

            var restoredFirst = back.Peek();
            Assert.IsNotNull(restoredFirst, "expected the saved head to come back, not an empty outbox");
            Assert.AreEqual(first.Key, restoredFirst.Key);
            Assert.AreEqual(first.Op, restoredFirst.Op);
            CollectionAssert.AreEqual(first.Body, restoredFirst.Body);
            Assert.AreEqual(first.CreatedAt, restoredFirst.CreatedAt);
            Assert.AreEqual(1, restoredFirst.Attempts); // proves Fail's state survived, not just the key
            Assert.AreEqual(first.NotBefore, restoredFirst.NotBefore);

            back.Ack(restoredFirst.Key);
            var restoredSecond = back.Peek();
            Assert.IsNotNull(restoredSecond, "expected the second entry to still be behind the first after the round trip");
            Assert.AreEqual(second.Key, restoredSecond.Key);
            Assert.AreEqual(second.Op, restoredSecond.Op);
            CollectionAssert.AreEqual(second.Body, restoredSecond.Body);
        }
    }
}
