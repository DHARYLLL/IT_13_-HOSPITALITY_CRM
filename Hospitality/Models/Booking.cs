namespace Hospitality.Models;

public class Booking
{
    public int booking_id { get; set; }
    public int client_id { get; set; }
    public DateTime check_in_date { get; set; }
    public DateTime check_out_date { get; set; }
    public int person_count { get; set; }
    public string? client_request { get; set; }
    
    // Add time fields to match database schema
    public TimeOnly? check_in_time { get; set; }
    public TimeOnly? check_out_time { get; set; }
    
    // Additional fields that might be needed
    public string booking_status { get; set; } = "Confirmed";
    public int guest_count { get; set; } // Alias for person_count
    
    // Related room data (from joins)
    public string? room_name { get; set; }
    public int? room_number { get; set; }
    public int? room_floor { get; set; }
    public decimal? room_price { get; set; }

    // PascalCase aliases for convenience
    public int BookingId => booking_id;
    public int ClientId => client_id;
    public DateTime CheckInDate => check_in_date;
    public DateTime CheckOutDate => check_out_date;
    public int PersonCount => person_count;
    public string? ClientRequest => client_request;
    public TimeOnly CheckInTime => check_in_time ?? new TimeOnly(15, 0);
    public TimeOnly CheckOutTime => check_out_time ?? new TimeOnly(11, 0);
    public string RoomName => room_name ?? string.Empty;
    public int RoomNumber => room_number ?? 0;
    public int RoomFloor => room_floor ?? 0;
    public decimal RoomPrice => room_price ?? 0;
}
