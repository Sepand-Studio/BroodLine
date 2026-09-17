using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// screen_inventory_v2 11: "regeneration, transit, charge regen, weekly
    /// tick, build timers" - one chip, five callers. It renders whatever
    /// `TimeSpan` it is handed; it does not own a clock or a tick loop, so
    /// re-binding on every tick is the caller's job, same as every other
    /// component here.
    [UxmlElement]
    public partial class TimerChip : VisualElement
    {
        public const string UssClassName = "timer-chip";
        public const string ExpiredUssClassName = "expired";

        readonly Label _time;

        public TimerChip()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("TimerChip");
            tree.CloneTree(this);

            _time = this.Q<Label>("time");
        }

        /// "1:02:03" once the remaining time reaches an hour, "02:05"
        /// before it. A remaining time at or below zero reads as "00:00" and
        /// marks the chip expired rather than showing a negative timer.
        public void Bind(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            EnableInClassList(ExpiredUssClassName, remaining <= TimeSpan.Zero);

            _time.text = remaining.TotalHours >= 1
                ? string.Format(
                    CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}",
                    (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds)
                : string.Format(
                    CultureInfo.InvariantCulture, "{0:00}:{1:00}",
                    remaining.Minutes, remaining.Seconds);
        }
    }
}
