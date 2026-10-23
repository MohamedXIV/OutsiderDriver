using NUnit.Framework;
using OutsiderDriver.Core.Time;

namespace OutsiderDriver.Tests.EditMode
{
    public class TimeServiceTests
    {
        [Test]
        public void WorldTime_NormalizesMinutesAndHoursAcrossDays()
        {
            var start = new WorldTime(day: 1, hour: 23, minute: 50);
            var next = start.AddMinutes(15);

            Assert.AreEqual(2, next.Day);
            Assert.AreEqual(0, next.Hour);
            Assert.AreEqual(5, next.Minute);
            Assert.AreEqual(TimeOfDayPeriod.Night, next.Period);
        }

        [Test]
        public void TimeService_AdvancesContinuouslyAndFiresCoarseMinuteAndHourTicks()
        {
            var service = new TimeService(null); // Default: Day 1, 18:00, 1.0s per minute
            service.SetTime(new WorldTime(1, 18, 58));

            int minuteTicks = 0;
            int hourTicks = 0;
            service.OnMinuteTicked += _ => minuteTicks++;
            service.OnHourTicked += _ => hourTicks++;

            // Advance 2.5 seconds -> should fire 2 minute ticks (18:59, 19:00) and 1 hour tick (19:00)
            service.AdvanceTime(2.5f);

            Assert.AreEqual(2, minuteTicks);
            Assert.AreEqual(1, hourTicks);
            Assert.AreEqual(new WorldTime(1, 19, 0), service.CurrentTime);
        }

        [Test]
        public void TimeService_RespectsPauseAndResume()
        {
            var service = new TimeService(null);
            WorldTime initial = service.CurrentTime;

            service.Pause();
            Assert.IsTrue(service.IsPaused);

            service.AdvanceTime(10f);
            Assert.AreEqual(initial, service.CurrentTime);

            service.Resume();
            Assert.IsFalse(service.IsPaused);

            service.AdvanceTime(1f);
            Assert.AreEqual(initial.AddMinutes(1), service.CurrentTime);
        }
    }
}
