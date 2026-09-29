namespace SrSimplifyRoutine.Web.Services;

public static class PlannerClock
{
    public static TimeZoneInfo Zone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { throw new PlannerException("Choose a valid time zone."); }
        catch (InvalidTimeZoneException) { throw new PlannerException("Choose a valid time zone."); }
    }

    public static DateTime ToUtc(DateTime local, string zoneId)
    {
        var zone = Zone(zoneId);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new PlannerException("This time falls within a daylight-saving clock change. Choose a different time.");
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public static DateTime Local(DateTime utc, string zoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(zoneId));

    public static DateTime WeekStart(DateTime local) => local.Date.AddDays(-((int)local.DayOfWeek + 6) % 7);
}
