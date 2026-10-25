using Npgsql;
using NpgsqlTypes;

public static class NamedPlaceMileageReportEndpoints
{
    public static void MapNamedPlaceMileageReportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/reports/named-place-mileage", GetAsync).WithName("GetNamedPlaceMileageReport");
    }

    private static async Task<IResult> GetAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? vehicleId,
        Guid? tagId,
        Guid[]? placeIds,
        string? units,
        NpgsqlDataSource dataSource,
        TraccarScopeApiAuthOptions authOptions,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var start = from ?? DateTimeOffset.UtcNow.AddMonths(-1);
        var end = to ?? DateTimeOffset.UtcNow;
        if (end < start)
            return Results.BadRequest(new { title = "Invalid report range", detail = "The through date must be on or after the from date." });
        var reportUnits = units?.Trim().ToLowerInvariant() switch
        {
            null or "" or "imperial" => "imperial",
            "metric" => "metric",
            _ => null
        };
        if (reportUnits is null)
            return Results.BadRequest(new { title = "Invalid report units", detail = "Units must be imperial or metric." });
        var selectedPlaceIds = placeIds?.Distinct().ToArray() ?? [];
        if (selectedPlaceIds.Length == 0)
            return Results.BadRequest(new { title = "Select named places", detail = "Choose at least one named place to run this report." });

        var isAdmin = context.User.IsInRole("admin");
        var username = context.User.Identity?.Name ?? string.Empty;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"""
            with filtered_trips as (
                select t.id, t.vehicle_id, v.display_name as vehicle_name,
                       t.started_at, t.ended_at, t.distance_meters, t.duration_seconds,
                       t.start_latitude, t.start_longitude, t.end_latitude, t.end_longitude,
                       t.start_address, t.end_address, t.start_traccar_position_id, t.end_traccar_position_id
                from trips t
                join vehicles v on v.id = t.vehicle_id
                where t.started_at >= @from and t.started_at <= @to
                  and (cast(@vehicleId as uuid) is null or t.vehicle_id = cast(@vehicleId as uuid))
                  and {VehicleAccess.TripReadAccessPredicate("t")}
                  and (cast(@tagId as uuid) is null or exists (
                      select 1 from trip_tag_map tm where tm.trip_id = t.id and tm.tag_id = cast(@tagId as uuid)
                  ))
            )
            select t.id, t.vehicle_id, t.vehicle_name,
                   t.started_at, t.ended_at, t.distance_meters, t.duration_seconds,
                   coalesce(sp.name, round(t.start_latitude::numeric, 5)::text || ', ' || round(t.start_longitude::numeric, 5)::text) as start_place_name,
                   coalesce(ep.name, round(t.end_latitude::numeric, 5)::text || ', ' || round(t.end_longitude::numeric, 5)::text) as end_place_name,
                   t.start_latitude, t.start_longitude, t.end_latitude, t.end_longitude,
                   t.start_traccar_position_id, t.end_traccar_position_id,
                   sp.id as start_place_id, ep.id as end_place_id
            from filtered_trips t
            left join lateral (
                select np.id, np.name
                from named_places np
                where (np.vehicle_id = t.vehicle_id or np.vehicle_id is null)
                  and t.start_latitude is not null and t.start_longitude is not null
                  and (2 * 6371000 * asin(sqrt(
                        sin(radians(t.start_latitude - np.latitude) / 2)^2
                        + cos(radians(t.start_latitude)) * cos(radians(np.latitude))
                        * sin(radians(t.start_longitude - np.longitude) / 2)^2
                      ))) <= np.radius_meters
                order by (np.vehicle_id = t.vehicle_id) desc, np.radius_meters, np.id
                limit 1
            ) sp on true
            left join lateral (
                select np.id, np.name
                from named_places np
                where (np.vehicle_id = t.vehicle_id or np.vehicle_id is null)
                  and t.end_latitude is not null and t.end_longitude is not null
                  and (2 * 6371000 * asin(sqrt(
                        sin(radians(t.end_latitude - np.latitude) / 2)^2
                        + cos(radians(t.end_latitude)) * cos(radians(np.latitude))
                        * sin(radians(t.end_longitude - np.longitude) / 2)^2
                      ))) <= np.radius_meters
                order by (np.vehicle_id = t.vehicle_id) desc, np.radius_meters, np.id
                limit 1
            ) ep on true
            where exists (
                select 1 from named_places selected_start
                where selected_start.id = any(@placeIds)
                  and (selected_start.vehicle_id = t.vehicle_id or selected_start.vehicle_id is null)
                  and t.start_latitude is not null and t.start_longitude is not null
                  and (2 * 6371000 * asin(sqrt(
                        sin(radians(t.start_latitude - selected_start.latitude) / 2)^2
                        + cos(radians(t.start_latitude)) * cos(radians(selected_start.latitude))
                        * sin(radians(t.start_longitude - selected_start.longitude) / 2)^2
                      ))) <= selected_start.radius_meters
            ) or exists (
                select 1 from named_places selected_end
                where selected_end.id = any(@placeIds)
                  and (selected_end.vehicle_id = t.vehicle_id or selected_end.vehicle_id is null)
                  and t.end_latitude is not null and t.end_longitude is not null
                  and (2 * 6371000 * asin(sqrt(
                        sin(radians(t.end_latitude - selected_end.latitude) / 2)^2
                        + cos(radians(t.end_latitude)) * cos(radians(selected_end.latitude))
                        * sin(radians(t.end_longitude - selected_end.longitude) / 2)^2
                      ))) <= selected_end.radius_meters
            )
            order by t.started_at, t.vehicle_name
            """, connection);
        command.Parameters.AddWithValue("from", start.UtcDateTime);
        command.Parameters.AddWithValue("to", end.UtcDateTime);
        command.Parameters.AddWithValue("vehicleId", (object?)vehicleId ?? DBNull.Value);
        command.Parameters.AddWithValue("tagId", (object?)tagId ?? DBNull.Value);
        command.Parameters.Add(new NpgsqlParameter<Guid[]>("placeIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { TypedValue = selectedPlaceIds });
        command.Parameters.AddWithValue("authEnabled", authOptions.Enabled);
        command.Parameters.AddWithValue("isAdmin", isAdmin);
        command.Parameters.AddWithValue("username", username);

        var rows = new List<NamedPlaceMileageRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                rows.Add(new NamedPlaceMileageRow(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                    reader.GetDateTime(3), reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    reader.IsDBNull(5) ? 0 : reader.GetDouble(5), reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetDouble(9), reader.IsDBNull(10) ? null : reader.GetDouble(10),
                    reader.IsDBNull(11) ? null : reader.GetDouble(11), reader.IsDBNull(12) ? null : reader.GetDouble(12),
                    reader.IsDBNull(13) ? null : reader.GetInt64(13), reader.IsDBNull(14) ? null : reader.GetInt64(14),
                    reader.IsDBNull(15) ? null : reader.GetGuid(15), reader.IsDBNull(16) ? null : reader.GetGuid(16)));
        }

        return Results.Ok(new
        {
            reportType = "named-place-mileage",
            generatedAtUtc = DateTimeOffset.UtcNow,
            units = reportUnits,
            storageUnits = new { distance = "m", duration = "s", odometer = "m" },
            filters = new { from = start, to = end, units = reportUnits, vehicleId, tagId, placeIds = selectedPlaceIds },
            summary = new { tripCount = rows.Count, distanceMeters = rows.Sum(row => row.DistanceMeters), durationSeconds = rows.Sum(row => row.DurationSeconds) },
            warnings = rows.Count == 0 ? new[] { "No trips matched the selected named places and filters." } : Array.Empty<string>(),
            rows
        });
    }

    private sealed record NamedPlaceMileageRow(
        Guid TripId, Guid VehicleId, string VehicleName,
        DateTime StartedAt, DateTime? EndedAt, double DistanceMeters, long DurationSeconds,
        string? StartPlaceName, string? EndPlaceName,
        double? StartLatitude, double? StartLongitude, double? EndLatitude, double? EndLongitude,
        long? StartTraccarPositionId, long? EndTraccarPositionId, Guid? StartPlaceId, Guid? EndPlaceId);
}
