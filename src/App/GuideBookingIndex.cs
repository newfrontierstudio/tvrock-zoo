using TsRecommend;
namespace TsScheduler;

// SID narrows candidates only. SameBroadcast remains the final authority,
// including legacy unknown-SID bookings and recording time adjustments.
internal sealed class GuideBookingIndex
{
    readonly List<Booking> all;
    readonly Dictionary<int,List<Booking>> services;
    readonly List<Booking> unknown;
    internal GuideBookingIndex(IEnumerable<Booking> bookings)
    {
        all=bookings.ToList();services=all.GroupBy(b=>b.ServiceId).ToDictionary(g=>g.Key,g=>g.ToList());
        unknown=services.GetValueOrDefault(0)??[];
    }
    internal IEnumerable<Booking> Matches(Show show)
    {
        IEnumerable<Booking> candidates=show.ServiceId==0?all:
            (services.GetValueOrDefault(show.ServiceId)??[]).Concat(unknown);
        return candidates.Where(b=>Planner.SameBroadcast(show,b));
    }
}
